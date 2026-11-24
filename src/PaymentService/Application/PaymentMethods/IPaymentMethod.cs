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
