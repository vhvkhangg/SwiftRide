using SwiftRide.PaymentService.Application.DTOs;
using SwiftRide.PaymentService.Application.Integrations;

namespace SwiftRide.PaymentService.Application.Services;

public sealed class ApprovePaymentDecisionService : IPaymentDecisionService
{
    public Task<PaymentDecision> DecideAsync(
        TripSnapshot trip,
        CreatePaymentRequest request,
        CancellationToken cancellationToken
    )
    {
        return Task.FromResult(new PaymentDecision(true));
    }
}