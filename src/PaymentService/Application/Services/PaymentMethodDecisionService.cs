using SwiftRide.PaymentService.Application.DTOs;
using SwiftRide.PaymentService.Application.Integrations;
using SwiftRide.PaymentService.Application.PaymentMethods;

namespace SwiftRide.PaymentService.Application.Services;

/// <summary>Quyết định thanh toán bằng phương thức do factory tạo theo yêu cầu.</summary>
public sealed class PaymentMethodDecisionService(IPaymentMethodFactory methods) : IPaymentDecisionService
{
    public Task<PaymentDecision> DecideAsync(
        TripSnapshot trip,
        CreatePaymentRequest request,
        CancellationToken cancellationToken) =>
        methods.Create(request.PaymentMethod).AuthorizeAsync(trip, request, cancellationToken);
}
