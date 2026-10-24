using Moq;
using SwiftRide.TripService.Application.DTOs;
using SwiftRide.TripService.Application.Interfaces;
using SwiftRide.TripService.Application.Services;
using SwiftRide.TripService.Domain.Entities;
using SwiftRide.TripService.Domain.Enums;
using SwiftRide.TripService.Domain.Interfaces;
using SwiftRide.Messaging;
using Xunit;

namespace SwiftRide.TripService.Tests;

public sealed class TripApplicationServiceTests
{
    private readonly Mock<ITripWriter> _writer = new();
    private readonly Mock<ITripReader> _reader = new();
    private readonly Mock<IMatchingPricingClient> _pricing = new();
    private readonly Mock<ITripWorkflowRepository> _workflow = new();
    private readonly Mock<ITripNotifier> _notifier = new();
    private readonly Mock<ITripMessageBus> _messageBus = new();

    private TripApplicationService CreateService() =>
        new(
            _writer.Object,
            _reader.Object,
            _workflow.Object,
            new ImmediateTripUnitOfWork(),
            _notifier.Object,
            _pricing.Object,
            _messageBus.Object,
            Microsoft.Extensions.Options.Options.Create(new TripMatchingOptions()));

    private static CreateTripRequest ValidRequest() =>
        new(Guid.NewGuid(), "Bến Thành", "Sân bay", 10.7769, 106.7009, 8, 25);

    [Fact]
    public async Task Create_UsesMatchingQuoteAndSavesTripAsRequested()
    {
        Trip? saved = null;
        MatchingQuoteRequest? quoteRequest = null;
        _pricing.Setup(client => client.GetQuoteAsync(
                It.IsAny<MatchingQuoteRequest>(), It.IsAny<CancellationToken>()))
            .Callback<MatchingQuoteRequest, CancellationToken>((request, _) => quoteRequest = request)
            .ReturnsAsync(new MatchingQuoteResponse(0, 96, 50, 10, 0, 0, 15.6m, 171.6m));
        _writer.Setup(writer => writer.AddAsync(It.IsAny<Trip>(), It.IsAny<CancellationToken>()))
            .Callback<Trip, CancellationToken>((trip, _) => saved = trip)
            .Returns(Task.CompletedTask);

        var request = ValidRequest() with { Surge = 1.2m, PromoCode = "RIDE10" };
        var result = await CreateService().CreateAsync(request, TestContext.Current.CancellationToken);

        Assert.NotNull(quoteRequest);
        Assert.Equal(request.Distance, quoteRequest.Distance);
        Assert.Equal(request.EstimatedTime, quoteRequest.EstimatedTime);
        Assert.Equal(request.Surge, quoteRequest.Surge);
        Assert.Equal(request.PromoCode, quoteRequest.PromoCode);
        Assert.NotNull(saved);
        Assert.Equal(171.6m, saved.FareEstimate);
        Assert.Equal(TripStatus.Requested, saved.Status);
        Assert.Equal(saved.Id, result.Id);
        Assert.Equal(saved.FareEstimate, result.FareEstimate);
    }

    [Theory]
    [InlineData(-1, 1, 1)]
    [InlineData(1, -1, 1)]
    [InlineData(1, 1, 0)]
    public async Task Create_RejectsInvalidPricingInputsBeforeCallingMatching(
        decimal distance, decimal estimatedTime, decimal surge)
    {
        var request = ValidRequest() with
        {
            Distance = distance,
            EstimatedTime = estimatedTime,
            Surge = surge
        };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            CreateService().CreateAsync(request, TestContext.Current.CancellationToken));

        _pricing.Verify(client => client.GetQuoteAsync(
            It.IsAny<MatchingQuoteRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        _writer.Verify(writer => writer.AddAsync(
            It.IsAny<Trip>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_DoesNotSaveTripWhenMatchingQuoteFails()
    {
        _pricing.Setup(client => client.GetQuoteAsync(
                It.IsAny<MatchingQuoteRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Matching unavailable"));

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            CreateService().CreateAsync(ValidRequest(), TestContext.Current.CancellationToken));

        _writer.Verify(writer => writer.AddAsync(
            It.IsAny<Trip>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ConfirmPrice_ChangesStateAndPersistsTrip()
    {
        var trip = Trip.Create(Guid.NewGuid(), "A", "B", 100, 10.7, 106.7);
        _reader.Setup(reader => reader.GetByIdAsync(trip.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trip);
        _workflow.Setup(workflow => workflow.GetInvitedDriverIdsAsync(
                trip.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Guid>());

        await CreateService().ConfirmPriceAsync(trip.Id, TestContext.Current.CancellationToken);

        Assert.Equal(TripStatus.PriceConfirmed, trip.Status);
        _writer.Verify(writer => writer.SaveAsync(trip, It.IsAny<CancellationToken>()), Times.Once);
        _messageBus.Verify(bus => bus.SendFindDriverAsync(
            It.Is<SwiftRide.Contracts.Matching.FindDriverForTripV1>(message =>
                message.TripId == trip.Id && message.Attempt == 1),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DriverCannotAcceptBeforeRiderConfirmsPrice()
    {
        var trip = Trip.Create(Guid.NewGuid(), "A", "B", 100);
        _reader.Setup(reader => reader.GetByIdAsync(trip.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trip);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService().DriverAcceptAsync(
                trip.Id,
                new DriverAcceptRequest(Guid.NewGuid()),
                TestContext.Current.CancellationToken));

        _writer.Verify(writer => writer.SaveAsync(
            It.IsAny<Trip>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private sealed class ImmediateTripUnitOfWork : ITripUnitOfWork
    {
        public Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken ct) =>
            operation(ct);

        public Task<T> ExecuteAsync<T>(
            Func<CancellationToken, Task<T>> operation,
            CancellationToken ct) =>
            operation(ct);
    }
}
