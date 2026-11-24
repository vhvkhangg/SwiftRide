using SwiftRide.PaymentService.Application.DTOs;
using SwiftRide.PaymentService.Application.Integrations;
using SwiftRide.PaymentService.Application.Services;
using SwiftRide.PaymentService.Domain.Enums;

namespace SwiftRide.PaymentService.Application.PaymentMethods;

/// <summary>Ví SwiftRide (mô phỏng): luôn đủ số dư trong bài tập.</summary>
public sealed class WalletPaymentMethod : IPaymentMethod
{
    public PaymentMethodType Type => PaymentMethodType.Wallet;

    public Task<PaymentDecision> AuthorizeAsync(
        TripSnapshot trip, CreatePaymentRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(new PaymentDecision(true));
}
