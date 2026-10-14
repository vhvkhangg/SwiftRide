using MassTransit;
using SwiftRide.Contracts.Payments;

namespace SwiftRide.Messaging;

public class MessagePublisher
{
    private readonly IPublishEndpoint _publisher;
    private readonly ISendEndpointProvider _sender;

    public MessagePublisher(IPublishEndpoint publisher, ISendEndpointProvider sender)
    {
        _publisher = publisher;
        _sender = sender;
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
        await endpoint.Send(message, cancellationToken);
    }
}
