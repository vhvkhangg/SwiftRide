using SwiftRide.PaymentService.Application.DTOs;
using SwiftRide.PaymentService.Application.Integrations;
using SwiftRide.PaymentService.Application.Services;
using SwiftRide.PaymentService.Domain.Enums;

namespace SwiftRide.PaymentService.Application.PaymentMethods;

/// <summary>
/// Thẻ (mô phỏng cổng thanh toán). Hệ thống thật chỉ giữ token do cổng cấp, không lưu số thẻ/CVV.
/// Mô phỏng hạn mức mỗi giao dịch để thấy được nhánh bị từ chối.
/// </summary>
public sealed class CardPaymentMethod : IPaymentMethod
{
    public const decimal PerTransactionLimit = 5_000_000m;

    public PaymentMethodType Type => PaymentMethodType.Card;

    public Task<PaymentDecision> AuthorizeAsync(
        TripSnapshot trip, CreatePaymentRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(request.Amount > PerTransactionLimit
            ? new PaymentDecision(false, "PAYMENT_DECLINED",
                $"Thẻ vượt hạn mức {PerTransactionLimit:0} VND cho một giao dịch.")
            : new PaymentDecision(true));
}
