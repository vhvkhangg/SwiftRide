using MassTransit;
using SwiftRide.Contracts.Trips;
using SwiftRide.TripService.Application.Interfaces;

namespace SwiftRide.TripService.Application.Sagas;

/// <summary>
/// Saga quá hạn chờ kết quả thanh toán: chuyển chuyến sang Failed nếu vẫn còn PaymentPending.
/// Nếu chuyến đã kịp Paid (kết quả và timeout chạy đua nhau) thì ghi nhận để saga kết thúc Completed.
/// </summary>
public sealed class AbandonTripPaymentActivity(ITripApplicationService trips)
    : IStateMachineActivity<TripPaymentSagaState, TripPaymentTimedOutV1>
{
    public const string Reason = "PAYMENT_TIMEOUT: no payment result before the saga deadline.";

    public async Task Execute(
        BehaviorContext<TripPaymentSagaState, TripPaymentTimedOutV1> context,
        IBehavior<TripPaymentSagaState, TripPaymentTimedOutV1> next)
    {
        context.Saga.TripAlreadyPaid = await trips.AbandonPaymentAsync(
            context.Saga.CorrelationId, Reason, context.CancellationToken);
        await next.Execute(context);
    }

    public Task Faulted<TException>(
        BehaviorExceptionContext<TripPaymentSagaState, TripPaymentTimedOutV1, TException> context,
        IBehavior<TripPaymentSagaState, TripPaymentTimedOutV1> next)
        where TException : Exception => next.Faulted(context);

    public void Probe(ProbeContext context) => context.CreateScope("abandon-trip-payment");

    public void Accept(StateMachineVisitor visitor) => visitor.Visit(this);
}
