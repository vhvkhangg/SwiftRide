using Moq;
using MongoDB.Driver.GeoJsonObjectModel;
using SwiftRide.Contracts.Matching;
using SwiftRide.MatchingService.Domain.Interfaces;
using SwiftRide.MatchingService.Domain.Models;
using Matching = SwiftRide.MatchingService.Application.Services.MatchingService;

namespace SwiftRide.MatchingService.Tests.Application;

public sealed class ReservationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LostRace_TriesNextCandidateOrReturnsSameAttemptWinner(bool sameAttempt)
    {
        var repository = new Mock<IDriverRepository>();
        var first = Driver();
        var second = Driver();
        var request = new FindDriverForTripV1
        {
            TripId = Guid.NewGuid(), Attempt = 1, PickupLatitude = 10.77, PickupLongitude = 106.7, RadiusKm = 5,
            ExcludedDriverIds = []
        };
        repository.SetupSequence(item => item.FindReservationAsync(request.TripId, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DriverLocation?)null).ReturnsAsync(sameAttempt ? second : null);
        repository.SetupSequence(item => item.FindNearestAvailableAsync(10.77, 106.7, 5,
                It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((first, 0.1)).ReturnsAsync((second, 0.2));
        repository.Setup(item => item.TryReserveAsync(second.DriverId, request.TripId, 1,
            It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var result = await new Matching(repository.Object).FindNextDriverAsync(request, TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal(second.DriverId, result.DriverId);
        repository.Verify(item => item.FindNearestAvailableAsync(10.77, 106.7, 5,
            It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()), Times.Exactly(sameAttempt ? 1 : 2));
    }

    [Fact]
    public async Task Replay_ReturnsExistingReservationWithoutReservingAgain()
    {
        var repository = new Mock<IDriverRepository>();
        var driver = Driver();
        var request = new FindDriverForTripV1 { TripId = Guid.NewGuid(), Attempt = 1 };
        repository.Setup(item => item.FindReservationAsync(request.TripId, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(driver);

        var result = await new Matching(repository.Object).FindNextDriverAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(driver.DriverId, result!.DriverId);
        repository.Verify(item => item.TryReserveAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<int>(),
            It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Contention_IsBoundedAndDoesNotPublishFalseNoDriverResult()
    {
        var repository = new Mock<IDriverRepository>();
        var request = new FindDriverForTripV1 { TripId = Guid.NewGuid(), Attempt = 1 };
        repository.Setup(item => item.FindNearestAvailableAsync(0, 0, 5,
                It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => (Driver(), 0.1));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new Matching(repository.Object).FindNextDriverAsync(request, TestContext.Current.CancellationToken));

        repository.Verify(item => item.TryReserveAsync(It.IsAny<string>(), request.TripId, 1,
            It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Exactly(16));
    }

    [Fact]
    public async Task InvalidExclusions_AreRejectedBeforeExistingReservationLookup()
    {
        var repository = new Mock<IDriverRepository>();
        var request = new FindDriverForTripV1
        {
            TripId = Guid.NewGuid(), Attempt = 1, ExcludedDriverIds = [Guid.Empty]
        };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            new Matching(repository.Object).FindNextDriverAsync(request, TestContext.Current.CancellationToken));

        repository.VerifyNoOtherCalls();
    }

    private static DriverLocation Driver() => new()
    {
        DriverId = Guid.NewGuid().ToString("D"), Location = GeoJson.Point(GeoJson.Geographic(106.7, 10.77)),
        IsAvailable = true, UpdatedAt = DateTime.UtcNow
    };
}
