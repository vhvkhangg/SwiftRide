using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SwiftRide.MatchingService.Domain.Models;

public sealed class MatchingOutboxMessage
{
    public const string DriverFoundType = "DriverFoundForTripV1";
    public const string NoDriverFoundType = "NoDriverFoundForTripV1";
    public const string PendingStatus = "Pending";
    public const string InFlightStatus = "InFlight";
    public const string DeliveredStatus = "Delivered";

    [BsonId]
    public required string Id { get; set; }

    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid MessageId { get; set; }

    public required string Type { get; set; }

    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid TripId { get; set; }

    public int Attempt { get; set; }

    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid? DriverId { get; set; }

    public string? Reason { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public string Status { get; set; } = PendingStatus;
    public DateTime NextAttemptAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? DeliveredAtUtc { get; set; }
    public DateTime? LockExpiresAtUtc { get; set; }

    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid? LockId { get; set; }

    public int PublishAttempts { get; set; }
    public string? LastError { get; set; }

    public static string CreateId(Guid tripId, int attempt) => $"{tripId:N}:{attempt}";
}
