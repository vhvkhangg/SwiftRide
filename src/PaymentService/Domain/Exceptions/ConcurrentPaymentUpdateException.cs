namespace SwiftRide.PaymentService.Domain.Exceptions;

public sealed class ConcurrentPaymentUpdateException : Exception
{
    public ConcurrentPaymentUpdateException(Exception innerException)
        : base(
            "Payment đã được cập nhật bởi request khác. " +
            "Vui lòng tải lại trạng thái trước khi thử tiếp.",
            innerException)
    {
    }
}