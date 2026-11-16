using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using SwiftRide.Contracts.Payments;
using SwiftRide.Contracts.Trips;
using SwiftRide.TripService.Application.Interfaces;
using SwiftRide.TripService.Application.Sagas;
using Xunit;

namespace SwiftRide.TripService.Tests;

public sealed class TripPaymentSagaTests
{
    private sealed record Context(
        ServiceProvider Provider,
        ITestHarness Harness,
        ISagaStateMachineTestHarness<TripPaymentStateMachine, TripPaymentSagaState> Saga,
        TripPaymentStateMachine Machine,
        Mock<ITripApplicationService> Trips) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await Harness.Stop();
            await Provider.DisposeAsync();
        }
    }

    private static async Task<Context> StartAsync(bool tripPaidAtTimeout = false)
    {
        var trips = new Mock<ITripApplicationService>();
        trips.Setup(x => x.AbandonPaymentAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(tripPaidAtTimeout);
        var provider = new ServiceCollection()
            .AddLogging()
            .AddSingleton(trips.Object)
            .AddScoped<AbandonTripPaymentActivity>()
            .Configure<TripPaymentSagaOptions>(o => o.TimeoutSeconds = 60)
            .AddMassTransitTestHarness(x => x.AddSagaStateMachine<TripPaymentStateMachine, TripPaymentSagaState>().InMemoryRepository())
            .BuildServiceProvider(true);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        var saga = harness.GetSagaStateMachineHarness<TripPaymentStateMachine, TripPaymentSagaState>();
        return new Context(provider, harness, saga, saga.StateMachine, trips);
    }

    private static TripDroppedOffV1 DroppedOff(Guid tripId) => new()
    {
        CorrelationId = tripId,
        TripId = tripId,
        Amount = 34_000m,
        IdempotencyKey = $"trip-{tripId:N}-payment-v1",
        PayerAccount = $"rider-{Guid.NewGuid():D}",
        PayeeAccount = $"driver-{Guid.NewGuid():D}",
        OccurredAtUtc = DateTimeOffset.UtcNow
    };

    private static PaymentSucceededV1 Succeeded(Guid tripId, Guid paymentId) => new()
    {
        CorrelationId = tripId, TripId = tripId, PaymentId = paymentId, Amount = 34_000m, OccurredAtUtc = DateTimeOffset.UtcNow
    };

    [Fact]
    public async Task Dropoff_SendsPaymentCommand_AndSuccessCompletes()
    {
        await using var c = await StartAsync();
        var tripId = Guid.NewGuid();

        await c.Harness.Bus.Publish(DroppedOff(tripId));
        Assert.NotNull(await c.Saga.Exists(tripId, x => x.AwaitingPayment));
        Assert.True(await c.Harness.Sent.Any<ProcessPaymentV1>(x =>
            x.Context.Message.TripId == tripId &&
            x.Context.Message.Amount == 34_000m &&
            x.Context.Message.IdempotencyKey == $"trip-{tripId:N}-payment-v1"));

        await c.Harness.Bus.Publish(Succeeded(tripId, Guid.NewGuid()));
        Assert.NotNull(await c.Saga.Exists(tripId, x => x.Completed));
        Assert.False(await c.Harness.Sent.Any<RefundPaymentV1>());
    }

    [Fact]
    public async Task Decline_EndsInDeclined()
    {
        await using var c = await StartAsync();
        var tripId = Guid.NewGuid();

        await c.Harness.Bus.Publish(DroppedOff(tripId));
        Assert.NotNull(await c.Saga.Exists(tripId, x => x.AwaitingPayment));
        await c.Harness.Bus.Publish(new PaymentFailedV1
        {
            CorrelationId = tripId, TripId = tripId, PaymentId = Guid.NewGuid(), Amount = 34_000m,
            FailureCode = "PAYMENT_DECLINED", Reason = "declined", OccurredAtUtc = DateTimeOffset.UtcNow
        });

        Assert.NotNull(await c.Saga.Exists(tripId, x => x.Declined));
    }

    [Fact]
    public async Task Timeout_AbandonsTrip_ThenLatePaymentIsRefunded()
    {
        await using var c = await StartAsync();
        var tripId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        await c.Harness.Bus.Publish(DroppedOff(tripId));
        Assert.NotNull(await c.Saga.Exists(tripId, x => x.AwaitingPayment));
        await c.Harness.Bus.Publish(new TripPaymentTimedOutV1 { CorrelationId = tripId, TripId = tripId, DeadlineUtc = DateTimeOffset.UtcNow });
        Assert.NotNull(await c.Saga.Exists(tripId, x => x.Abandoned));
        c.Trips.Verify(x => x.AbandonPaymentAsync(tripId, AbandonTripPaymentActivity.Reason, It.IsAny<CancellationToken>()), Times.Once);

        // Compensation: money was taken after the trip already failed ⇒ refund.
        await c.Harness.Bus.Publish(Succeeded(tripId, paymentId));
        Assert.NotNull(await c.Saga.Exists(tripId, x => x.Compensating));
        Assert.True(await c.Harness.Sent.Any<RefundPaymentV1>(x =>
            x.Context.Message.TripId == tripId && x.Context.Message.PaymentId == paymentId));

        await c.Harness.Bus.Publish(new PaymentRefundedV1
        {
            CorrelationId = tripId, TripId = tripId, PaymentId = paymentId, Amount = 34_000m, OccurredAtUtc = DateTimeOffset.UtcNow
        });
        Assert.NotNull(await c.Saga.Exists(tripId, x => x.Refunded));
    }

    [Fact]
    public async Task Timeout_WhenTripAlreadyPaid_CompletesWithoutRefund()
    {
        await using var c = await StartAsync(tripPaidAtTimeout: true);
        var tripId = Guid.NewGuid();

        await c.Harness.Bus.Publish(DroppedOff(tripId));
        Assert.NotNull(await c.Saga.Exists(tripId, x => x.AwaitingPayment));
        await c.Harness.Bus.Publish(new TripPaymentTimedOutV1 { CorrelationId = tripId, TripId = tripId, DeadlineUtc = DateTimeOffset.UtcNow });

        Assert.NotNull(await c.Saga.Exists(tripId, x => x.Completed));
        await c.Harness.Bus.Publish(Succeeded(tripId, Guid.NewGuid()));
        Assert.False(await c.Harness.Sent.Any<RefundPaymentV1>());
    }
}
