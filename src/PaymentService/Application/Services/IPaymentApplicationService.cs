using SwiftRide.PaymentService.Application.DTOs;

namespace SwiftRide.PaymentService.Application.Services;

public interface IPaymentApplicationService
{
    Task<PaymentResponse> CreatePaymentAsync(
        CreatePaymentRequest request, CancellationToken cancellationToken = default);
    Task<PaymentResponse?> GetPaymentByIdAsync(
        Guid paymentId, CancellationToken cancellationToken = default);
    Task<PaymentResponse?> GetPaymentByTripIdAsync(
        Guid tripId,
        CancellationToken cancellationToken = default);
    Task<RefundPaymentResponse> RefundPaymentAsync(
        Guid paymentId, CancellationToken cancellationToken = default);
    Task<PaymentResponse> ReconcilePaymentAsync(
        Guid paymentId, CancellationToken cancellationToken = default);
}
