using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SwiftRide.PaymentService.Domain.Enums;

namespace SwiftRide.PaymentService.Application.DTOs
{
    public sealed record CreatePaymentRequest(
        Guid TripId,
        decimal Amount,
        string IdempotencyKey,
        string PayerAccount,
        string PayeeAccount,
        PaymentMethodType PaymentMethod = PaymentMethodType.Wallet
    );
}