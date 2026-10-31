using MassTransit;
using SwiftRide.Contracts.Matching;
using SwiftRide.Contracts.Payments;
using Microsoft.Extensions.Logging;

namespace SwiftRide.Messaging;

public class MessagePublisher : ITripMessageBus
{
    private readonly IPublishEndpoint _publisher;
    private readonly ISendEndpointProvider _sender;
    private readonly ILogger<MessagePublisher> _logger;

    public MessagePublisher(IPublishEndpoint publisher, ISendEndpointProvider sender, ILogger<MessagePublisher> logger)
    {
        _publisher = publisher;
        _sender = sender;
        _logger = logger;
    }

    // Publish: thông báo event cho mọi queue đã đăng ký nhận loại event đó.
    public Task PublishAsync<T>(T message, CancellationToken cancellationToken = default)
        where T : class
    {
        if (message is ProcessPaymentV1)
        {
            throw new ArgumentException("Dùng SendPaymentAsync để gửi command thanh toán.");
        }

        return _publisher.Publish(message, cancellationToken);
    }

    // Send: gửi command đến đúng endpoint của PaymentService.
    public async Task SendPaymentAsync(
        ProcessPaymentV1 message,
        CancellationToken cancellationToken = default)
    {
        var address = new Uri("queue:" + MessagingEndpoints.PaymentProcessPayment);
        var endpoint = await _sender.GetSendEndpoint(address);
        await endpoint.Send(message, context =>
        {
            context.MessageId = NewId.NextGuid();
            context.CorrelationId = message.CorrelationId;
            _logger.LogInformation("Staging payment command. TripId={TripId}, CorrelationId={CorrelationId}, MessageId={MessageId}, IdempotencyKey={IdempotencyKey}",
                message.TripId, message.CorrelationId, context.MessageId, message.IdempotencyKey);
        }, cancellationToken);
    }

    public async Task SendFindDriverAsync(
        FindDriverForTripV1 message,
        CancellationToken cancellationToken = default)
    {
        var address = new Uri("queue:" + MessagingEndpoints.MatchingFindDriverForTrip);
        var endpoint = await _sender.GetSendEndpoint(address);
        await endpoint.Send(message, context =>
        {
            context.MessageId = NewId.NextGuid();
            context.CorrelationId = message.CorrelationId;
            _logger.LogInformation("Staging matching command. TripId={TripId}, CorrelationId={CorrelationId}, MessageId={MessageId}, Attempt={Attempt}",
                message.TripId, message.CorrelationId, context.MessageId, message.Attempt);
        }, cancellationToken);
    }

    public async Task ReleaseDriverAsync(Guid driverId, Guid tripId, CancellationToken cancellationToken = default)
    {
        var endpoint = await _sender.GetSendEndpoint(new Uri("queue:" + MessagingEndpoints.MatchingReleaseDriver));
        await endpoint.Send(new ReleaseDriverV1 { DriverId = driverId, TripId = tripId, CorrelationId = tripId }, cancellationToken);
    }
}
