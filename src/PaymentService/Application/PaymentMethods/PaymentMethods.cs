using SwiftRide.PaymentService.Application.DTOs;
using SwiftRide.PaymentService.Application.Integrations;
using SwiftRide.PaymentService.Application.Services;
using SwiftRide.PaymentService.Domain.Enums;

namespace SwiftRide.PaymentService.Application.PaymentMethods;

/// <summary>
/// Một phương thức thanh toán: tự quyết định duyệt hay từ chối khoản thu cho chuyến đi.
/// Sổ cái (debit/credit) vẫn do PaymentApplicationService ghi, không phụ thuộc phương thức.
/// </summary>
public interface IPaymentMethod
{
    PaymentMethodType Type { get; }

    Task<PaymentDecision> AuthorizeAsync(
        TripSnapshot trip,
        CreatePaymentRequest request,
        CancellationToken cancellationToken);
}

/// <summary>Ví SwiftRide (mô phỏng): luôn đủ số dư trong bài tập.</summary>
public sealed class WalletPaymentMethod : IPaymentMethod
{
    public PaymentMethodType Type => PaymentMethodType.Wallet;

    public Task<PaymentDecision> AuthorizeAsync(
        TripSnapshot trip, CreatePaymentRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(new PaymentDecision(true));
}

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

/// <summary>Factory pattern: tạo đúng đối tượng phương thức thanh toán theo loại.</summary>
public interface IPaymentMethodFactory
{
    IPaymentMethod Create(PaymentMethodType type);
}

public sealed class PaymentMethodFactory : IPaymentMethodFactory
{
    public IPaymentMethod Create(PaymentMethodType type) => type switch
    {
        PaymentMethodType.Wallet => new WalletPaymentMethod(),
        PaymentMethodType.Card => new CardPaymentMethod(),
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Phương thức thanh toán không được hỗ trợ.")
    };
}
