using MongoDB.Driver.GeoJsonObjectModel;
using Moq;
using SwiftRide.MatchingService.Domain.Interfaces;
using SwiftRide.MatchingService.Domain.Models;
using SwiftRide.MatchingService.Application.DTOs;
using MatchingApplicationService = SwiftRide.MatchingService.Application.Services.MatchingService;

namespace SwiftRide.MatchingService.Tests.Application;

public sealed class MatchingServiceTests
{
    [Fact]
    public async Task MatchAsync_ReturnsDriversInRepositoryDistanceOrder()
    {
        var repository = new Mock<IDriverRepository>();
        var updatedAt = DateTime.UtcNow;
        repository
            .Setup(item => item.FindNearbyAvailableAsync(10.7769, 106.7009, 5, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                (Driver("driver-near", 106.7010, 10.7770, updatedAt), 0.02),
                (Driver("driver-far", 106.7100, 10.7800, updatedAt), 1.25)
            ]);

        var result = await new MatchingApplicationService(repository.Object).MatchAsync(
            new MatchRequest("trip-001", 10.7769, 106.7009, 5), CancellationToken.None);

        Assert.Equal("trip-001", result.TripId);
        Assert.Collection(
            result.Drivers,
            driver => Assert.Equal("driver-near", driver.DriverId),
            driver => Assert.Equal("driver-far", driver.DriverId));
        Assert.Equal(0.02, result.Drivers[0].DistanceKm);
        repository.Verify(item => item.FindNearbyAvailableAsync(
            10.7769, 106.7009, 5, 10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(91, 106)]
    [InlineData(-91, 106)]
    [InlineData(10, 181)]
    [InlineData(10, -181)]
    public async Task MatchAsync_ThrowsForInvalidCoordinates(double latitude, double longitude)
    {
        var repository = new Mock<IDriverRepository>();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            new MatchingApplicationService(repository.Object).MatchAsync(
                new MatchRequest("trip-001", latitude, longitude, 5), CancellationToken.None));

        repository.Verify(item => item.FindNearbyAvailableAsync(
            It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(),
            It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task MatchAsync_ThrowsForNonPositiveRadius()
    {
        var repository = new Mock<IDriverRepository>();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            new MatchingApplicationService(repository.Object).MatchAsync(
                new MatchRequest("trip-001", 10, 106, 0), CancellationToken.None));
    }

    private static DriverLocation Driver(string id, double longitude, double latitude, DateTime updatedAt) =>
        new()
        {
            DriverId = id,
            Location = GeoJson.Point(GeoJson.Geographic(longitude, latitude)),
            IsAvailable = true,
            UpdatedAt = updatedAt
        };
}
