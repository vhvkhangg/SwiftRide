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
using SwiftRide.Contracts.Payments;

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
            MaxAttempts = 3,
            InvitationTimeoutSeconds = 30,
            SearchRadiusKm = 5
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
        var request = new CreateTripRequest(Guid.NewGuid(), "A", "B", 10.0, 106.0, 5m, 10m, "Moto", null, idempotencyKey);

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
        var request = new CreateTripRequest(Guid.NewGuid(), "A", "B", 10.0, 106.0, 5m, 10m, "Moto", null, idempotencyKey);

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

    [Theory] // Yêu cầu 3: Test Validation Event sai lệch
    [InlineData(true, 2000, "VND", "PAYMENT_DECLINED", "Lỗi thẻ", true)] // Sai TripId (Rỗng)
    [InlineData(false, 0, "VND", "PAYMENT_DECLINED", "Lỗi thẻ", true)]    // Sai Amount
    [InlineData(false, 2000, "USD", "PAYMENT_DECLINED", "Lỗi thẻ", true)] // Sai Currency
    [InlineData(false, 2000, "VND", "WRONG_CODE", "Lỗi thẻ", true)]       // Sai FailureCode
    [InlineData(false, 2000, "VND", "PAYMENT_DECLINED", "", true)]        // Thiếu Reason
    [InlineData(false, 2000, "VND", "PAYMENT_DECLINED", "Lỗi thẻ", false)]// Thiếu OccurredAtUtc
    public async Task HandlePaymentFailedAsync_InvalidData_ThrowsException(
        bool emptyTripId, decimal amount, string currency, string failCode, string reason, bool hasDate)
    {
        var tripId = emptyTripId ? Guid.Empty : Guid.NewGuid();
        var invalidEvent = new PaymentFailedV1
        {
            TripId = tripId,
            CorrelationId = tripId,
            PaymentId = Guid.NewGuid(),
            Amount = amount,
            Currency = currency,
            FailureCode = failCode,
            Reason = reason == "QUASIZE" ? new string('x', 300) : reason,
            OccurredAtUtc = hasDate ? DateTimeOffset.UtcNow : default,
            IdempotencyKey = $"trip-{tripId:N}-payment-v1"
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.HandlePaymentFailedAsync(invalidEvent, CancellationToken.None));
    }

    [Fact]
    public async Task HandlePaymentFailedAsync_ReasonExceeds256Chars_ThrowsException()
    {
        var tripId = Guid.NewGuid();
        var invalidEvent = new PaymentFailedV1
        {
            TripId = tripId,
            CorrelationId = tripId,
            PaymentId = Guid.NewGuid(),
            Amount = 50000,
            Currency = "VND",
            FailureCode = "PAYMENT_DECLINED",
            Reason = new string('x', 300), // Vượt 256 ký tự
            OccurredAtUtc = DateTimeOffset.UtcNow,
            IdempotencyKey = $"trip-{tripId:N}-payment-v1"
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.HandlePaymentFailedAsync(invalidEvent, CancellationToken.None));
    }

    [Fact] // Yêu cầu 2: Duplicate Event khi đã Failed
    public async Task HandlePaymentFailedAsync_AlreadyFailed_IgnoresAndDoesNotSave()
    {
        var tripId = Guid.NewGuid();
        var trip = Trip.Create(Guid.NewGuid(), "A", "B", 50000);
        trip.ConfirmPrice(); trip.AcceptByDriver(Guid.NewGuid());
        trip.StartEnRoute(); trip.Pickup(); trip.Dropoff(); trip.MarkPaymentPending();
        trip.MarkFailed(); // Trạng thái đã Failed

        _readerMock.Setup(x => x.GetByIdAsync(tripId, It.IsAny<CancellationToken>())).ReturnsAsync(trip);

        var failEvent = new PaymentFailedV1
        {
            TripId = tripId,
            CorrelationId = tripId,
            PaymentId = Guid.NewGuid(),
            Amount = 50000,
            Currency = "VND",
            FailureCode = "PAYMENT_DECLINED",
            Reason = "Lỗi",
            OccurredAtUtc = DateTimeOffset.UtcNow,
            IdempotencyKey = $"trip-{tripId:N}-payment-v1"
        };

        await _service.HandlePaymentFailedAsync(failEvent, CancellationToken.None);

        // Verify không bao giờ gọi hàm Save
        _writerMock.Verify(x => x.SaveAsync(It.IsAny<Trip>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact] // Yêu cầu 4: Event muộn, không cho lùi từ Paid về Failed
    public async Task HandlePaymentFailedAsync_AlreadyPaid_IgnoresAndDoesNotSave()
    {
        var tripId = Guid.NewGuid();
        var trip = Trip.Create(Guid.NewGuid(), "A", "B", 50000);
        trip.ConfirmPrice(); trip.AcceptByDriver(Guid.NewGuid());
        trip.StartEnRoute(); trip.Pickup(); trip.Dropoff(); trip.MarkPaymentPending();
        trip.MarkPaid(); // Trạng thái đã Paid

        _readerMock.Setup(x => x.GetByIdAsync(tripId, It.IsAny<CancellationToken>())).ReturnsAsync(trip);

        var lateFailEvent = new Contracts.Payments.PaymentFailedV1
        {
            TripId = tripId,
            CorrelationId = tripId,
            PaymentId = Guid.NewGuid(),
            Amount = 50000,
            Currency = "VND",
            FailureCode = "PAYMENT_DECLINED",
            Reason = "Lỗi muộn",
            OccurredAtUtc = DateTimeOffset.UtcNow,
            IdempotencyKey = $"trip-{tripId:N}-payment-v1"
        };

        await _service.HandlePaymentFailedAsync(lateFailEvent, CancellationToken.None);
        _writerMock.Verify(x => x.SaveAsync(It.IsAny<Trip>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact] // Yêu cầu 4: Không cho lùi từ Failed sang Paid
    public async Task HandlePaymentSucceededAsync_AlreadyFailed_IgnoresAndDoesNotSave()
    {
        var tripId = Guid.NewGuid();
        var trip = Trip.Create(Guid.NewGuid(), "A", "B", 50000);
        trip.ConfirmPrice(); trip.AcceptByDriver(Guid.NewGuid());
        trip.StartEnRoute(); trip.Pickup(); trip.Dropoff(); trip.MarkPaymentPending();
        trip.MarkFailed(); // Trạng thái đã Failed

        _readerMock.Setup(x => x.GetByIdAsync(tripId, It.IsAny<CancellationToken>())).ReturnsAsync(trip);

        var lateSuccessEvent = new PaymentSucceededV1
        {
            TripId = tripId,
            CorrelationId = tripId,
            PaymentId = Guid.NewGuid(),
            Amount = 50000,
            Currency = "VND"
        };

        await _service.HandlePaymentSucceededAsync(lateSuccessEvent, CancellationToken.None);
        _writerMock.Verify(x => x.SaveAsync(It.IsAny<Trip>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}