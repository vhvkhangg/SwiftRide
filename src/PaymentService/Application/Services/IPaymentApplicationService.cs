using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using SwiftRide.PaymentService.Application.DTOs;

namespace SwiftRide.PaymentService.Application.Services
{
    public interface IPaymentApplicationService
    {
        Task<PaymentResponse> CreatePaymentAsync(
            CreatePaymentRequest request,
            CancellationToken cancellationToken = default
        );

        Task<PaymentResponse?> GetPaymentByIdAsync(
            Guid paymentId,
            CancellationToken cancellationToken = default
        );

        Task<RefundPaymentResponse> RefundPaymentAsync(
            Guid paymentId,
            CancellationToken cancellationToken = default
        );
    }
}