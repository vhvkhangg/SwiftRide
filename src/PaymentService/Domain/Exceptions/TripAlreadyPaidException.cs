namespace SwiftRide.PaymentService.Domain.Exceptions;

public sealed class TripAlreadyPaidException : Exception
{
    public TripAlreadyPaidException(Exception innerException)
        : base(
            "Trip đã có Payment thành công hoặc đã hoàn tiền.",
            innerException)
    {
    }
}