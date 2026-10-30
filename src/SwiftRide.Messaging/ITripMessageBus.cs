using SwiftRide.Contracts.Matching;
using SwiftRide.Contracts.Payments;

namespace SwiftRide.Messaging;

public interface ITripMessageBus
{
    Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class;
    Task SendPaymentAsync(ProcessPaymentV1 message, CancellationToken cancellationToken = default);
    Task SendFindDriverAsync(FindDriverForTripV1 message, CancellationToken cancellationToken = default);
    Task ReleaseDriverAsync(Guid driverId, Guid tripId, CancellationToken cancellationToken = default);
}
