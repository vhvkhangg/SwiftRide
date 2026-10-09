using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver.GeoJsonObjectModel;

namespace SwiftRide.MatchingService.Domain.Models;

public sealed class DriverLocation
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }
    public required string DriverId { get; set; }
    public required GeoJsonPoint<GeoJson2DGeographicCoordinates> Location { get; set; }
    public bool IsAvailable { get; set; }
    public DateTime UpdatedAt { get; set; }
}
