using SwiftRide.PaymentService.Application.DTOs;
using SwiftRide.PaymentService.Application.Integrations;

namespace SwiftRide.PaymentService.Application.Services;

public sealed record PaymentDecision(
    bool Approved,
    string? FailureCode = null,
    string? FailureReason = null
);

public interface IPaymentDecisionService
{
    Task<PaymentDecision> DecideAsync(
        TripSnapshot trip,
        CreatePaymentRequest request,
        CancellationToken cancellationToken
    );
}