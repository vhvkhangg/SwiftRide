using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Moq;
using SwiftRide.Contracts.Matching;
using SwiftRide.Messaging;
using SwiftRide.TripService.Application.DTOs;
using SwiftRide.TripService.Application.Interfaces;
using SwiftRide.TripService.Application.Services;
using SwiftRide.TripService.Domain.Entities;
using SwiftRide.TripService.Domain.Interfaces;
using Xunit;

namespace SwiftRide.TripService.Tests;

public class TripApplicationServiceTests
{
    private readonly Mock<ITripReader> _readerMock = new();
    private readonly Mock<ITripWriter> _writerMock = new();
    private readonly Mock<ITripWorkflowRepository> _workflowMock = new();
    private readonly Mock<ITripUnitOfWork> _uowMock = new();
    private readonly Mock<ITripNotifier> _notifierMock = new();
    private readonly Mock<IMatchingPricingClient> _pricingMock = new();
    private readonly Mock<ITripMessageBus> _messageBusMock = new();
    
    private readonly TripApplicationService _service;

    public TripApplicationServiceTests()
    {
        var options = Options.Create(new TripMatchingOptions 
        { 
            MaxAttempts = 3, InvitationTimeoutSeconds = 30, SearchRadiusKm = 5 
        });

        _service = new TripApplicationService(
            _writerMock.Object, _readerMock.Object, _workflowMock.Object,
            _uowMock.Object, _notifierMock.Object, _pricingMock.Object,
            _messageBusMock.Object, options);
    }

    // --- KỊCH BẢN 1: TEST IDEMPOTENCY ---
    
    private string ComputeHash(object payload)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload));
        return Convert.ToBase64String(sha256.ComputeHash(bytes));
    }

    [Fact]
    public async Task CreateAsync_SameIdempotencyKey_SamePayload_ReturnsExistingTrip()
    {
        // Arrange
        var idempotencyKey = "key-test-123";
        var request = new CreateTripRequest(Guid.NewGuid(), "A", "B", 10.0, 106.0, 5m, 10m, "Moto", 1m, null, idempotencyKey);
        
        var payloadForHash = new { request.RiderId, request.PickupAddress, request.DropoffAddress, request.Distance };
        var hash = ComputeHash(payloadForHash);

        var existingTrip = Trip.Create(request.RiderId, "A", "B", 50000m, 10.0, 106.0, idempotencyKey, hash);
        
        // Mock CSDL trả về chuyến đi cũ khi tìm theo IdempotencyKey
        _readerMock.Setup(x => x.GetByIdempotencyKeyAsync(idempotencyKey, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(existingTrip);

        // Act
        var response = await _service.CreateAsync(request);

        // Assert
        Assert.Equal(existingTrip.Id, response.Id);
        _writerMock.Verify(x => x.AddAsync(It.IsAny<Trip>(), It.IsAny<CancellationToken>()), Times.Never); // Chắc chắn không lưu DB thêm dòng mới
    }

    [Fact]
    public async Task CreateAsync_SameIdempotencyKey_DifferentPayload_ThrowsConflict()
    {
        // Arrange
        var idempotencyKey = "key-test-123";
        // SỬA CS1503: Truyền đầy đủ 10.0, 106.0 cho Latitude/Longitude để không bị lệch tham số
        var request = new CreateTripRequest(Guid.NewGuid(), "A", "B", 10.0, 106.0, 5m, 10m, "Moto", 1m, null, idempotencyKey);
        
        // Cố tình tạo hash cũ khác với hash của request hiện tại
        var oldHash = "some-different-old-hash"; 
        var existingTrip = Trip.Create(request.RiderId, "A", "B", 50000m, 10.0, 106.0, idempotencyKey, oldHash);

        _readerMock.Setup(x => x.GetByIdempotencyKeyAsync(idempotencyKey, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(existingTrip);

        // Act & Assert
        // SỬA xUnit1051: Truyền thêm CancellationToken.None
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CreateAsync(request, CancellationToken.None));
            
        Assert.Contains("IdempotencyKey đã được sử dụng", ex.Message);
    }

    // --- KỊCH BẢN 2: TEST PHỤC HỒI PHIÊN ---

    [Fact]
    public async Task GetActiveTripAsync_WhenExists_ReturnsTripResponse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var trip = Trip.Create(userId, "A", "B", 50000m);
        _readerMock.Setup(x => x.GetActiveTripAsync(userId, "Rider", It.IsAny<CancellationToken>()))
                   .ReturnsAsync(trip);

        // Act
        // SỬA xUnit1051: Truyền thêm CancellationToken.None
        var response = await _service.GetActiveTripAsync(userId, "Rider", CancellationToken.None);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(trip.Id, response.Id);
    }
}