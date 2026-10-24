using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SwiftRide.MatchingService.Domain.Models;

public sealed class PromoCodeDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;
    public required string Code { get; set; }
    public decimal DiscountRate { get; set; }
    public bool IsActive { get; set; }
    public DateTime ExpiredAt { get; set; }
}
