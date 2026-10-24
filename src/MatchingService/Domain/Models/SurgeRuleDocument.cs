using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SwiftRide.MatchingService.Domain.Models;

public sealed class SurgeRuleDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;
    public int StartHour { get; set; }
    public int EndHour { get; set; }
    public decimal Multiplier { get; set; }
}
