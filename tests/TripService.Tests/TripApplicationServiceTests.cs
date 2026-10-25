using Moq;
using SwiftRide.Contracts.Payments;
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

    // ---------------------------------------------------------------------------
    // CreateAsync
    // ---------------------------------------------------------------------------

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

    // ---------------------------------------------------------------------------
    // ConfirmPriceAsync
    // ---------------------------------------------------------------------------

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

    // ---------------------------------------------------------------------------
    // DropoffAsync – ProcessPaymentV1 contract
    // ---------------------------------------------------------------------------

    /// <summary>
    /// DropoffAsync must publish ProcessPaymentV1 with the correct Amount,
    /// Rider/Driver accounts and a stable IdempotencyKey.
    /// </summary>
    [Fact]
    public async Task Dropoff_PublishesProcessPaymentV1WithCorrectFields()
    {
        var riderId = Guid.NewGuid();
        var driverId = Guid.NewGuid();
        var trip = CreatePickedUpTrip(riderId, driverId, fareEstimate: 150_000m);

        _reader.Setup(r => r.GetByIdAsync(trip.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trip);

        ProcessPaymentV1? captured = null;
        _messageBus
            .Setup(b => b.SendPaymentAsync(It.IsAny<ProcessPaymentV1>(), It.IsAny<CancellationToken>()))
            .Callback<ProcessPaymentV1, CancellationToken>((cmd, _) => captured = cmd)
            .Returns(Task.CompletedTask);

        await CreateService().DropoffAsync(trip.Id, TestContext.Current.CancellationToken);

        Assert.NotNull(captured);
        Assert.Equal(trip.Id, captured.CorrelationId);
        Assert.Equal(trip.Id, captured.TripId);
        Assert.Equal(150_000m, captured.Amount);
        Assert.Equal("VND", captured.Currency);
        Assert.Equal($"trip-{trip.Id:N}-payment-v1", captured.IdempotencyKey);
        Assert.Equal($"rider-{riderId:D}", captured.PayerAccount);
        Assert.Equal($"driver-{driverId:D}", captured.PayeeAccount);
    }

    [Fact]
    public async Task Dropoff_IdempotencyKey_IsStableAcrossRetries()
    {
        // Two separate DropoffAsync calls for the same Trip (PaymentPending retry path)
        // must produce the identical IdempotencyKey.
        var riderId = Guid.NewGuid();
        var driverId = Guid.NewGuid();
        var trip = CreatePickedUpTrip(riderId, driverId, fareEstimate: 50_000m);

        _reader.Setup(r => r.GetByIdAsync(trip.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trip);

        var keys = new List<string>();
        _messageBus
            .Setup(b => b.SendPaymentAsync(It.IsAny<ProcessPaymentV1>(), It.IsAny<CancellationToken>()))
            .Callback<ProcessPaymentV1, CancellationToken>((cmd, _) => keys.Add(cmd.IdempotencyKey))
            .Returns(Task.CompletedTask);

        await CreateService().DropoffAsync(trip.Id, TestContext.Current.CancellationToken);
        // Trip is now PaymentPending; calling again re-sends the payment command
        await CreateService().DropoffAsync(trip.Id, TestContext.Current.CancellationToken);

        Assert.Equal(2, keys.Count);
        Assert.Equal(keys[0], keys[1]);
    }

    // ---------------------------------------------------------------------------
    // HandlePaymentSucceededAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task HandlePaymentSucceeded_ValidEvent_TransitionsPaymentPendingToPaid()
    {
        var trip = CreatePaymentPendingTrip(fareEstimate: 80_000m);
        SetupReader(trip);

        var evt = ValidSucceededEvent(trip);
        await CreateService().HandlePaymentSucceededAsync(evt, TestContext.Current.CancellationToken);

        Assert.Equal(TripStatus.Paid, trip.Status);
        _writer.Verify(w => w.SaveAsync(trip, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandlePaymentSucceeded_DuplicateEvent_IsIdempotentWhenAmountMatches()
    {
        // Trip is already Paid; duplicate event with matching amount must be silently accepted.
        var trip = CreatePaidTrip(fareEstimate: 80_000m);
        SetupReader(trip);

        var evt = ValidSucceededEvent(trip);
        await CreateService().HandlePaymentSucceededAsync(evt, TestContext.Current.CancellationToken);

        // Should NOT persist or notify again
        _writer.Verify(w => w.SaveAsync(It.IsAny<Trip>(), It.IsAny<CancellationToken>()), Times.Never);
        _notifier.Verify(n => n.NotifyTripChangedAsync(It.IsAny<TripResponse>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandlePaymentSucceeded_DuplicateEvent_ThrowsWhenAmountMismatches()
    {
        var trip = CreatePaidTrip(fareEstimate: 80_000m);
        SetupReader(trip);

        var evt = new PaymentSucceededV1
        {
            CorrelationId = trip.Id,
            TripId = trip.Id,
            PaymentId = Guid.NewGuid(),
            Amount = 99_000m, // wrong amount
            Currency = "VND",
            OccurredAtUtc = DateTimeOffset.UtcNow
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService().HandlePaymentSucceededAsync(evt, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task HandlePaymentSucceeded_WrongCorrelationId_Throws()
    {
        var trip = CreatePaymentPendingTrip(fareEstimate: 80_000m);
        SetupReader(trip);

        var evt = new PaymentSucceededV1
        {
            CorrelationId = Guid.NewGuid(), // wrong: must equal TripId
            TripId = trip.Id,
            PaymentId = Guid.NewGuid(),
            Amount = trip.FareEstimate!.Value,
            Currency = "VND",
            OccurredAtUtc = DateTimeOffset.UtcNow
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService().HandlePaymentSucceededAsync(evt, TestContext.Current.CancellationToken));
        _writer.Verify(w => w.SaveAsync(It.IsAny<Trip>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandlePaymentSucceeded_EmptyPaymentId_Throws()
    {
        var trip = CreatePaymentPendingTrip(fareEstimate: 80_000m);
        SetupReader(trip);

        var evt = new PaymentSucceededV1
        {
            CorrelationId = trip.Id,
            TripId = trip.Id,
            PaymentId = Guid.Empty, // invalid
            Amount = trip.FareEstimate!.Value,
            Currency = "VND",
            OccurredAtUtc = DateTimeOffset.UtcNow
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService().HandlePaymentSucceededAsync(evt, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task HandlePaymentSucceeded_WrongAmount_Throws()
    {
        var trip = CreatePaymentPendingTrip(fareEstimate: 80_000m);
        SetupReader(trip);

        var evt = new PaymentSucceededV1
        {
            CorrelationId = trip.Id,
            TripId = trip.Id,
            PaymentId = Guid.NewGuid(),
            Amount = 50_000m, // wrong amount
            Currency = "VND",
            OccurredAtUtc = DateTimeOffset.UtcNow
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService().HandlePaymentSucceededAsync(evt, TestContext.Current.CancellationToken));
        _writer.Verify(w => w.SaveAsync(It.IsAny<Trip>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandlePaymentSucceeded_WrongCurrency_Throws()
    {
        var trip = CreatePaymentPendingTrip(fareEstimate: 80_000m);
        SetupReader(trip);

        var evt = new PaymentSucceededV1
        {
            CorrelationId = trip.Id,
            TripId = trip.Id,
            PaymentId = Guid.NewGuid(),
            Amount = trip.FareEstimate!.Value,
            Currency = "USD", // wrong currency
            OccurredAtUtc = DateTimeOffset.UtcNow
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService().HandlePaymentSucceededAsync(evt, TestContext.Current.CancellationToken));
    }

    // ---------------------------------------------------------------------------
    // HandlePaymentFailedAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task HandlePaymentFailed_ValidEvent_RecordsFailureWithoutChangingState()
    {
        var trip = CreatePaymentPendingTrip(fareEstimate: 80_000m);
        SetupReader(trip);

        var evt = new PaymentFailedV1
        {
            TripId = trip.Id,
            CorrelationId = trip.Id,
            IdempotencyKey = $"trip-{trip.Id:N}-payment-v1",
            Reason = "Insufficient funds",
            OccurredAtUtc = DateTimeOffset.UtcNow
        };

        await CreateService().HandlePaymentFailedAsync(evt, TestContext.Current.CancellationToken);

        // RecordPaymentFailure does not change Status, just logs reason
        Assert.Equal(TripStatus.PaymentPending, trip.Status);
        _writer.Verify(w => w.SaveAsync(trip, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandlePaymentFailed_StaleIdempotencyKey_IsIgnored()
    {
        var trip = CreatePaymentPendingTrip(fareEstimate: 80_000m);
        SetupReader(trip);

        var staleEvt = new PaymentFailedV1
        {
            TripId = trip.Id,
            CorrelationId = trip.Id,
            // Key does not match the pattern TripService sends, so event is stale
            IdempotencyKey = $"trip-{trip.Id:N}-payment-v2",
            Reason = "Old failure",
            OccurredAtUtc = DateTimeOffset.UtcNow
        };

        await CreateService().HandlePaymentFailedAsync(staleEvt, TestContext.Current.CancellationToken);

        _writer.Verify(w => w.SaveAsync(It.IsAny<Trip>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandlePaymentFailed_TripNotPaymentPending_IsIgnored()
    {
        // Trip is already Paid; a late FailedV1 must not alter state.
        var trip = CreatePaidTrip(fareEstimate: 80_000m);
        SetupReader(trip);

        var evt = new PaymentFailedV1
        {
            TripId = trip.Id,
            CorrelationId = trip.Id,
            IdempotencyKey = $"trip-{trip.Id:N}-payment-v1",
            Reason = "Too late",
            OccurredAtUtc = DateTimeOffset.UtcNow
        };

        await CreateService().HandlePaymentFailedAsync(evt, TestContext.Current.CancellationToken);

        Assert.Equal(TripStatus.Paid, trip.Status);
        _writer.Verify(w => w.SaveAsync(It.IsAny<Trip>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static Trip CreatePickedUpTrip(Guid riderId, Guid driverId, decimal fareEstimate)
    {
        var trip = Trip.Create(riderId, "A", "B", fareEstimate, 10.7, 106.7);
        trip.ConfirmPrice();
        trip.BeginMatchingAttempt(3);
        trip.AcceptByDriver(driverId);
        trip.StartEnRoute();
        trip.Pickup();
        return trip;
    }

    private static Trip CreatePaymentPendingTrip(decimal fareEstimate)
    {
        var trip = CreatePickedUpTrip(Guid.NewGuid(), Guid.NewGuid(), fareEstimate);
        trip.Dropoff();
        trip.MarkPaymentPending();
        return trip;
    }

    private static Trip CreatePaidTrip(decimal fareEstimate)
    {
        var trip = CreatePaymentPendingTrip(fareEstimate);
        trip.MarkPaid();
        return trip;
    }

    private void SetupReader(Trip trip) =>
        _reader.Setup(r => r.GetByIdAsync(trip.Id, It.IsAny<CancellationToken>()))
               .ReturnsAsync(trip);

    private static PaymentSucceededV1 ValidSucceededEvent(Trip trip) => new()
    {
        CorrelationId = trip.Id,
        TripId = trip.Id,
        PaymentId = Guid.NewGuid(),
        Amount = trip.FareEstimate!.Value,
        Currency = "VND",
        OccurredAtUtc = DateTimeOffset.UtcNow
    };

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
