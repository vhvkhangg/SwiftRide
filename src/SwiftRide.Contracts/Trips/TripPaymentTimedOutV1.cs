namespace SwiftRide.Contracts.Trips;

// Event nội bộ của Trip: Saga thanh toán đã quá hạn chờ kết quả.
public class TripPaymentTimedOutV1
{
    public Guid CorrelationId { get; set; }
    public Guid TripId { get; set; }
    public DateTimeOffset DeadlineUtc { get; set; }
}
