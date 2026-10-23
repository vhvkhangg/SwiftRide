
namespace SwiftRide.PaymentService.Application.Exceptions;

public sealed class PaymentCommandRejectedException
    : InvalidOperationException
{
    public PaymentCommandRejectedException(string message)
        : base(message)
    {
    }
}
