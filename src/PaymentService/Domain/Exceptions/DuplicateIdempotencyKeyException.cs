namespace SwiftRide.PaymentService.Domain.Exceptions;

public sealed class DuplicateIdempotencyKeyException : Exception
{
    public DuplicateIdempotencyKeyException(Exception innerException)
        : base("Khóa idempotency đã được một request khác ghi vào database.", innerException)
    {
    }
}
