namespace SwiftRide.PaymentService.Application.Integrations;

public interface ITripServiceClient
{
    Task<TripSnapshot?> GetByIdAsync(
        Guid tripId,
        CancellationToken cancellationToken = default);

    Task MarkPaidAsync(
        Guid tripId,
        CancellationToken cancellationToken = default);
}