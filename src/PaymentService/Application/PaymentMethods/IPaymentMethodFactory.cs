using SwiftRide.PaymentService.Domain.Enums;

namespace SwiftRide.PaymentService.Application.PaymentMethods;

public interface IPaymentMethodFactory
{
    IPaymentMethod Create(PaymentMethodType type);
}
