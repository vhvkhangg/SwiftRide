using SwiftRide.PaymentService.Domain.Enums;

namespace SwiftRide.PaymentService.Application.PaymentMethods;

/// <summary>Chọn phương thức thanh toán từ các implementation đã đăng ký qua DI.</summary>
public sealed class PaymentMethodFactory : IPaymentMethodFactory
{
    private readonly IReadOnlyDictionary<PaymentMethodType, IPaymentMethod> _methods;

    public PaymentMethodFactory(IEnumerable<IPaymentMethod> methods)
    {
        _methods = methods.ToDictionary(method => method.Type);
    }

    public IPaymentMethod Create(PaymentMethodType type)
    {
        if (_methods.TryGetValue(type, out var method))
        {
            return method;
        }

        throw new ArgumentOutOfRangeException(
            nameof(type), type, "Phương thức thanh toán không được hỗ trợ.");
    }
}
