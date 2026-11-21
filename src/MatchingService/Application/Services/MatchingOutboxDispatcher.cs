using MassTransit;
using SwiftRide.Contracts.Matching;
using SwiftRide.MatchingService.Domain.Interfaces;
using SwiftRide.MatchingService.Domain.Models;

namespace SwiftRide.MatchingService.Application.Services;

public sealed class MatchingOutboxDispatcher(
    IServiceScopeFactory scopes,
    ILogger<MatchingOutboxDispatcher> logger) : BackgroundService
{
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan IdleDelay = TimeSpan.FromMilliseconds(500);
    private readonly Guid dispatcherId = Guid.NewGuid();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var outbox = scope.ServiceProvider.GetRequiredService<IMatchingOutboxRepository>();
                var message = await outbox.ClaimNextAsync(
                    dispatcherId, DateTime.UtcNow, LeaseDuration, stoppingToken);
                if (message is null)
                {
                    await Task.Delay(IdleDelay, stoppingToken);
                    continue;
                }

                var publisher = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();
                try
                {
                    await PublishAsync(publisher, message, stoppingToken);
                    await outbox.MarkDeliveredAsync(
                        message.Id, dispatcherId, DateTime.UtcNow, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    var retryDelay = TimeSpan.FromSeconds(
                        Math.Min(300, Math.Pow(2, Math.Min(message.PublishAttempts, 8))));
                    try
                    {
                        await outbox.ScheduleRetryAsync(
                            message.Id,
                            dispatcherId,
                            DateTime.UtcNow.Add(retryDelay),
                            exception.ToString(),
                            stoppingToken);
                    }
                    catch (Exception persistenceException) when (
                        persistenceException is not OperationCanceledException)
                    {
                        logger.LogError(
                            persistenceException,
                            "Could not reschedule Matching outbox message {OutboxMessageId}; its lease will expire.",
                            message.Id);
                    }

                    logger.LogWarning(
                        exception,
                        "Could not publish Matching outbox message {OutboxMessageId}; retry scheduled.",
                        message.Id);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Matching outbox dispatch loop failed.");
                await Task.Delay(IdleDelay, stoppingToken);
            }
        }
    }

    private static Task PublishAsync(
        IPublishEndpoint publisher,
        MatchingOutboxMessage message,
        CancellationToken cancellationToken)
    {
        var occurredAtUtc = new DateTimeOffset(
            DateTime.SpecifyKind(message.OccurredAtUtc, DateTimeKind.Utc));
        return message.Type switch
        {
            MatchingOutboxMessage.DriverFoundType when message.DriverId is { } driverId =>
                publisher.Publish(new DriverFoundForTripV1
                {
                    CorrelationId = message.TripId,
                    TripId = message.TripId,
                    Attempt = message.Attempt,
                    DriverId = driverId,
                    OccurredAtUtc = occurredAtUtc
                }, context =>
                {
                    context.MessageId = message.MessageId;
                    context.CorrelationId = message.TripId;
                }, cancellationToken),
            MatchingOutboxMessage.NoDriverFoundType =>
                publisher.Publish(new NoDriverFoundForTripV1
                {
                    CorrelationId = message.TripId,
                    TripId = message.TripId,
                    Attempt = message.Attempt,
                    Reason = message.Reason ?? "Không tìm thấy tài xế khả dụng.",
                    OccurredAtUtc = occurredAtUtc
                }, context =>
                {
                    context.MessageId = message.MessageId;
                    context.CorrelationId = message.TripId;
                }, cancellationToken),
            _ => throw new InvalidOperationException(
                $"Unsupported Matching outbox message type '{message.Type}'.")
        };
    }
}
