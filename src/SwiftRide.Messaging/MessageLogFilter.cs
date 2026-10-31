using MassTransit;
using Microsoft.Extensions.Logging;

namespace SwiftRide.Messaging;

public sealed class MessageLogFilter<T>(ILogger<MessageLogFilter<T>> logger) : IFilter<ConsumeContext<T>> where T : class
{
    public async Task Send(ConsumeContext<T> context, IPipe<ConsumeContext<T>> next)
    {
        var message = typeof(T).GetProperty("Message")?.GetValue(context.Message) ?? context.Message;
        object? Field(string name) => message.GetType().GetProperty(name)?.GetValue(message);
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["TripId"] = Field("TripId"),
            ["CorrelationId"] = context.CorrelationId ?? Field("CorrelationId"),
            ["MessageId"] = context.MessageId,
            ["Attempt"] = Field("Attempt"),
            ["IdempotencyKey"] = Field("IdempotencyKey"),
            ["RetryAttempt"] = context.GetRetryAttempt()
        });
        logger.LogInformation("Consuming {MessageType}", typeof(T).Name);
        try { await next.Send(context); }
        catch
        {
            logger.LogWarning("Consumption failed for {MessageType}; inspect retry/error queue", typeof(T).Name);
            throw;
        }
    }

    public void Probe(ProbeContext context) => context.CreateFilterScope("message-identifiers");
}
