using MongoDB.Driver;
using SwiftRide.MatchingService.Domain.Interfaces;
using SwiftRide.MatchingService.Domain.Models;

namespace SwiftRide.MatchingService.Infrastructure.Repositories;

public sealed class MongoMatchingOutboxRepository(IMongoDatabase database) : IMatchingOutboxRepository
{
    private readonly IMongoCollection<MatchingOutboxMessage> collection =
        database.GetCollection<MatchingOutboxMessage>("matching_outbox");

    public async Task EnsureIndexesAsync(CancellationToken cancellationToken)
    {
        await collection.Indexes.CreateOneAsync(
            new CreateIndexModel<MatchingOutboxMessage>(
                Builders<MatchingOutboxMessage>.IndexKeys
                    .Ascending(message => message.Status)
                    .Ascending(message => message.NextAttemptAtUtc)
                    .Ascending(message => message.LockExpiresAtUtc)
                    .Ascending(message => message.CreatedAtUtc)),
            cancellationToken: cancellationToken);
    }

    public Task<bool> ExistsAsync(
        Guid tripId,
        int attempt,
        CancellationToken cancellationToken) =>
        collection.Find(message => message.Id == MatchingOutboxMessage.CreateId(tripId, attempt))
            .AnyAsync(cancellationToken);

    public async Task<bool> EnqueueNoDriverFoundAsync(
        Guid tripId,
        int attempt,
        string reason,
        CancellationToken cancellationToken)
    {
        if (tripId == Guid.Empty)
            throw new ArgumentException("TripId không hợp lệ.", nameof(tripId));
        if (attempt < 1)
            throw new ArgumentOutOfRangeException(nameof(attempt));
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reason là bắt buộc.", nameof(reason));

        var now = DateTime.UtcNow;
        var message = new MatchingOutboxMessage
        {
            Id = MatchingOutboxMessage.CreateId(tripId, attempt),
            MessageId = Guid.NewGuid(),
            Type = MatchingOutboxMessage.NoDriverFoundType,
            TripId = tripId,
            Attempt = attempt,
            Reason = reason,
            OccurredAtUtc = now,
            NextAttemptAtUtc = now,
            CreatedAtUtc = now
        };

        try
        {
            await collection.InsertOneAsync(message, cancellationToken: cancellationToken);
            return true;
        }
        catch (MongoWriteException exception) when (
            exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            return false;
        }
    }

    public async Task<MatchingOutboxMessage?> ClaimNextAsync(
        Guid lockId,
        DateTime nowUtc,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken)
    {
        if (lockId == Guid.Empty)
            throw new ArgumentException("LockId không hợp lệ.", nameof(lockId));
        if (leaseDuration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(leaseDuration));

        var due = Builders<MatchingOutboxMessage>.Filter.Lte(message => message.NextAttemptAtUtc, nowUtc);
        var available = Builders<MatchingOutboxMessage>.Filter.Or(
            Builders<MatchingOutboxMessage>.Filter.Eq(message => message.Status, MatchingOutboxMessage.PendingStatus),
            Builders<MatchingOutboxMessage>.Filter.And(
                Builders<MatchingOutboxMessage>.Filter.Eq(message => message.Status, MatchingOutboxMessage.InFlightStatus),
                Builders<MatchingOutboxMessage>.Filter.Lte(message => message.LockExpiresAtUtc, nowUtc)));
        var filter = Builders<MatchingOutboxMessage>.Filter.And(due, available);
        var update = Builders<MatchingOutboxMessage>.Update
            .Set(message => message.Status, MatchingOutboxMessage.InFlightStatus)
            .Set(message => message.LockId, lockId)
            .Set(message => message.LockExpiresAtUtc, nowUtc.Add(leaseDuration))
            .Inc(message => message.PublishAttempts, 1);
        return await collection.FindOneAndUpdateAsync(
            filter,
            update,
            new FindOneAndUpdateOptions<MatchingOutboxMessage>
            {
                Sort = Builders<MatchingOutboxMessage>.Sort.Ascending(message => message.CreatedAtUtc),
                ReturnDocument = ReturnDocument.After
            },
            cancellationToken);
    }

    public async Task MarkDeliveredAsync(
        string messageId,
        Guid lockId,
        DateTime deliveredAtUtc,
        CancellationToken cancellationToken)
    {
        var result = await collection.UpdateOneAsync(
            Builders<MatchingOutboxMessage>.Filter.Eq(message => message.Id, messageId) &
            Builders<MatchingOutboxMessage>.Filter.Eq(message => message.LockId, lockId),
            Builders<MatchingOutboxMessage>.Update
                .Set(message => message.Status, MatchingOutboxMessage.DeliveredStatus)
                .Set(message => message.DeliveredAtUtc, deliveredAtUtc)
                .Unset(message => message.LockId)
                .Unset(message => message.LockExpiresAtUtc)
                .Unset(message => message.LastError),
            cancellationToken: cancellationToken);
        if (result.MatchedCount == 0)
            throw new InvalidOperationException($"Outbox message '{messageId}' is no longer owned by this dispatcher.");
    }

    public async Task ScheduleRetryAsync(
        string messageId,
        Guid lockId,
        DateTime nextAttemptAtUtc,
        string error,
        CancellationToken cancellationToken)
    {
        var result = await collection.UpdateOneAsync(
            Builders<MatchingOutboxMessage>.Filter.Eq(message => message.Id, messageId) &
            Builders<MatchingOutboxMessage>.Filter.Eq(message => message.LockId, lockId),
            Builders<MatchingOutboxMessage>.Update
                .Set(message => message.Status, MatchingOutboxMessage.PendingStatus)
                .Set(message => message.NextAttemptAtUtc, nextAttemptAtUtc)
                .Set(message => message.LastError, error.Length <= 2000 ? error : error[..2000])
                .Unset(message => message.LockId)
                .Unset(message => message.LockExpiresAtUtc),
            cancellationToken: cancellationToken);
        if (result.MatchedCount == 0)
            throw new InvalidOperationException($"Outbox message '{messageId}' is no longer owned by this dispatcher.");
    }
}
