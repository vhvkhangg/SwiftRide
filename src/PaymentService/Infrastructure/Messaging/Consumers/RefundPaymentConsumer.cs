using MassTransit;
using SwiftRide.Contracts.Payments;
using SwiftRide.PaymentService.Application.Services;
using SwiftRide.PaymentService.Domain.Enums;

namespace SwiftRide.PaymentService.Infrastructure.Messaging.Consumers;

/// <summary>
/// Lệnh bù trừ từ Saga thanh toán: hoàn tiền một payment đã thành công.
/// Idempotent: payment đã Refunded thì chỉ phát lại PaymentRefundedV1, không ghi ledger lần hai.
/// </summary>
public sealed class RefundPaymentConsumer(
    IPaymentApplicationService payments,
    ILogger<RefundPaymentConsumer> logger) : IConsumer<RefundPaymentV1>
{
    public async Task Consume(ConsumeContext<RefundPaymentV1> context)
    {
        var message = context.Message;
        if (message.TripId == Guid.Empty || message.CorrelationId != message.TripId || message.PaymentId == Guid.Empty)
            throw new ArgumentException("RefundPaymentV1 không hợp lệ.");

        var payment = await payments.GetPaymentByIdAsync(message.PaymentId, context.CancellationToken)
            ?? throw new ArgumentException("Không tìm thấy Payment cần hoàn tiền.");
        if (payment.TripId != message.TripId)
            throw new ArgumentException("Payment không thuộc Trip trong lệnh hoàn tiền.");

        if (payment.Status == nameof(PaymentStatus.Succeeded))
        {
            await payments.RefundPaymentAsync(message.PaymentId, context.CancellationToken);
            logger.LogInformation("Saga compensation refunded payment. TripId={TripId}, PaymentId={PaymentId}, Reason={Reason}",
                message.TripId, message.PaymentId, message.Reason);
        }
        else if (payment.Status != nameof(PaymentStatus.Refunded))
        {
            throw new ArgumentException($"Không thể hoàn tiền Payment ở trạng thái {payment.Status}.");
        }

        await context.Publish(new PaymentRefundedV1
        {
            CorrelationId = message.CorrelationId,
            TripId = message.TripId,
            PaymentId = message.PaymentId,
            Amount = payment.Amount,
            OccurredAtUtc = DateTimeOffset.UtcNow
        });
    }
}
