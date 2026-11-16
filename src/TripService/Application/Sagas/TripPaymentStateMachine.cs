using MassTransit;
using Microsoft.Extensions.Options;
using SwiftRide.Contracts.Payments;
using SwiftRide.Contracts.Trips;
using SwiftRide.Messaging;

namespace SwiftRide.TripService.Application.Sagas;

/// <summary>
/// Saga thanh toán dạng orchestration (MassTransit State Machine).
///
///   Dropoff ──► AwaitingPayment ──PaymentSucceeded──► Completed
///                     │  ├─PaymentFailed────────────► Declined
///                     │  ├─Fault (lỗi kỹ thuật)─────► ở lại, đếm FaultCount
///                     │  └─TimedOut──► Activity: Trip Failed (PAYMENT_TIMEOUT) ──► Abandoned
///                     │                (nếu Trip đã kịp Paid thì ──► Completed)
///   Abandoned ──PaymentSucceeded đến muộn──► gửi RefundPaymentV1 (bù trừ) ──► Compensating
///   Compensating ──PaymentRefunded──► Refunded
///
/// Trip consumer vẫn là nơi đổi trạng thái Trip khi có kết quả thanh toán; saga là nơi duy nhất
/// gửi lệnh thanh toán và lệnh bù trừ (hoàn tiền).
/// </summary>
public sealed class TripPaymentStateMachine : MassTransitStateMachine<TripPaymentSagaState>
{
    public State AwaitingPayment { get; private set; } = null!;
    public State Completed { get; private set; } = null!;
    public State Declined { get; private set; } = null!;
    public State Abandoned { get; private set; } = null!;
    public State Compensating { get; private set; } = null!;
    public State Refunded { get; private set; } = null!;

    public Event<TripDroppedOffV1> DroppedOff { get; private set; } = null!;
    public Event<PaymentSucceededV1> PaymentSucceeded { get; private set; } = null!;
    public Event<PaymentFailedV1> PaymentFailed { get; private set; } = null!;
    public Event<Fault<ProcessPaymentV1>> PaymentFaulted { get; private set; } = null!;
    public Event<TripPaymentTimedOutV1> TimedOut { get; private set; } = null!;
    public Event<PaymentRefundedV1> Refund { get; private set; } = null!;

    private static readonly Uri PaymentQueue = new("queue:" + MessagingEndpoints.PaymentProcessPayment);
    private static readonly Uri RefundQueue = new("queue:" + MessagingEndpoints.PaymentRefundPayment);

    public TripPaymentStateMachine(IOptions<TripPaymentSagaOptions> options)
    {
        var timeout = TimeSpan.FromSeconds(options.Value.TimeoutSeconds);
        InstanceState(x => x.CurrentState);

        Event(() => DroppedOff, e => e.CorrelateById(m => m.Message.TripId));
        // Kết quả cho chuyến không có saga (ví dụ command gửi tay khi vận hành) thì bỏ qua.
        Event(() => PaymentSucceeded, e => { e.CorrelateById(m => m.Message.TripId); e.OnMissingInstance(m => m.Discard()); });
        Event(() => PaymentFailed, e => { e.CorrelateById(m => m.Message.TripId); e.OnMissingInstance(m => m.Discard()); });
        Event(() => PaymentFaulted, e => { e.CorrelateById(m => m.Message.Message.TripId); e.OnMissingInstance(m => m.Discard()); });
        Event(() => TimedOut, e => { e.CorrelateById(m => m.Message.TripId); e.OnMissingInstance(m => m.Discard()); });
        Event(() => Refund, e => { e.CorrelateById(m => m.Message.TripId); e.OnMissingInstance(m => m.Discard()); });

        // Message trùng hoặc đến muộn ở trạng thái không xử lý: bỏ qua, không đưa vào _error.
        OnUnhandledEvent(x => x.Ignore());

        Initially(
            When(DroppedOff)
                .Then(ctx =>
                {
                    var m = ctx.Message;
                    var now = DateTime.UtcNow;
                    ctx.Saga.Amount = m.Amount;
                    ctx.Saga.Currency = m.Currency;
                    ctx.Saga.IdempotencyKey = m.IdempotencyKey;
                    ctx.Saga.PayerAccount = m.PayerAccount;
                    ctx.Saga.PayeeAccount = m.PayeeAccount;
                    ctx.Saga.PaymentMethod = m.PaymentMethod;
                    ctx.Saga.StartedAtUtc = now;
                    ctx.Saga.DeadlineUtc = now.Add(timeout);
                    Touch(ctx.Saga);
                })
                .Send(PaymentQueue, ctx => ToCommand(ctx.Saga))
                .Then(ctx => ctx.Saga.CommandsSent++)
                .TransitionTo(AwaitingPayment));

        During(AwaitingPayment,
            // Dropoff lặp lại (client retry): gửi lại cùng idempotency key, Payment không trừ tiền hai lần.
            When(DroppedOff)
                .Send(PaymentQueue, ctx => ToCommand(ctx.Saga))
                .Then(ctx => { ctx.Saga.CommandsSent++; Touch(ctx.Saga); }),
            When(PaymentSucceeded)
                .Then(ctx => { ctx.Saga.PaymentId = ctx.Message.PaymentId; Finish(ctx.Saga); })
                .TransitionTo(Completed),
            When(PaymentFailed)
                .Then(ctx =>
                {
                    ctx.Saga.PaymentId = ctx.Message.PaymentId;
                    ctx.Saga.FailureReason = ctx.Message.Reason;
                    Finish(ctx.Saga);
                })
                .TransitionTo(Declined),
            When(PaymentFaulted)
                .Then(ctx =>
                {
                    ctx.Saga.FaultCount++;
                    ctx.Saga.FailureReason = "Technical payment failure; waiting for replay or timeout.";
                    Touch(ctx.Saga);
                }),
            When(TimedOut)
                .Activity(x => x.OfType<AbandonTripPaymentActivity>())
                .IfElse(ctx => ctx.Saga.TripAlreadyPaid,
                    paid => paid.Then(ctx => Finish(ctx.Saga)).TransitionTo(Completed),
                    abandoned => abandoned
                        .Then(ctx => { ctx.Saga.FailureReason = AbandonTripPaymentActivity.Reason; Touch(ctx.Saga); })
                        .TransitionTo(Abandoned)));

        During(Abandoned,
            // Bù trừ: tiền đã bị trừ nhưng chuyến đã thất bại do quá hạn ⇒ hoàn tiền.
            When(PaymentSucceeded)
                .Then(ctx => { ctx.Saga.PaymentId = ctx.Message.PaymentId; Touch(ctx.Saga); })
                .Send(RefundQueue, ctx => new RefundPaymentV1
                {
                    CorrelationId = ctx.Saga.CorrelationId,
                    TripId = ctx.Saga.CorrelationId,
                    PaymentId = ctx.Message.PaymentId,
                    Reason = "Saga compensation: payment succeeded after the trip payment timed out.",
                    OccurredAtUtc = DateTimeOffset.UtcNow
                })
                .TransitionTo(Compensating));

        During(Compensating,
            When(Refund)
                .Then(ctx => Finish(ctx.Saga))
                .TransitionTo(Refunded));
    }

    private static ProcessPaymentV1 ToCommand(TripPaymentSagaState saga) => new()
    {
        CorrelationId = saga.CorrelationId,
        TripId = saga.CorrelationId,
        Amount = saga.Amount,
        Currency = saga.Currency,
        IdempotencyKey = saga.IdempotencyKey,
        PayerAccount = saga.PayerAccount,
        PayeeAccount = saga.PayeeAccount,
        PaymentMethod = saga.PaymentMethod,
        OccurredAtUtc = DateTimeOffset.UtcNow
    };

    private static void Touch(TripPaymentSagaState saga) => saga.UpdatedAtUtc = DateTime.UtcNow;

    private static void Finish(TripPaymentSagaState saga)
    {
        Touch(saga);
        saga.CompletedAtUtc = saga.UpdatedAtUtc;
    }
}
