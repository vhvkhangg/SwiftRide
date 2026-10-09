using System.ComponentModel.DataAnnotations;
using SwiftRide.MatchingService.Application.DTOs;

namespace SwiftRide.MatchingService.Api;

/// <summary>Payload used to calculate an estimated ride quote.</summary>
public sealed record QuoteRequestDto
{
    [Range(0, double.MaxValue)] public decimal Distance { get; init; }
    [Range(0, double.MaxValue)] public decimal EstimatedTime { get; init; }
    public string? RideType { get; init; }
    [Range(0.01, double.MaxValue)] public decimal Surge { get; init; } = 1m;
    public string? PromoCode { get; init; }

    public QuoteRequest ToDomain() => new(Distance, EstimatedTime, RideType, Surge, PromoCode);
}

/// <summary>Detailed price breakdown returned for a ride quote.</summary>
public sealed record QuoteResponseDto(
    decimal Base, decimal Distance, decimal Time, decimal Surge,
    decimal Toll, decimal Discount, decimal Tax, decimal Total);

/// <summary>Payload used to find available drivers near a pickup location.</summary>
public sealed record MatchRequestDto
{
    [Required] public required string TripId { get; init; }
    [Range(-90, 90)] public double PickupLatitude { get; init; }
    [Range(-180, 180)] public double PickupLongitude { get; init; }
    [Range(0.01, 1000)] public double RadiusKm { get; init; } = 5;

    public MatchRequest ToDomain() => new(TripId, PickupLatitude, PickupLongitude, RadiusKm);
}

/// <summary>Driver returned by the matching endpoint, ordered by pickup distance.</summary>
public sealed record MatchedDriverDto(
    string DriverId,
    double Latitude,
    double Longitude,
    double DistanceKm,
    DateTimeOffset UpdatedAt);

/// <summary>Matching result for a trip.</summary>
public sealed record MatchResponseDto(string TripId, IReadOnlyList<MatchedDriverDto> Drivers);

/// <summary>Payload used by a driver app to publish its GPS location.</summary>
public sealed record DriverLocationRequestDto
{
    [Required] public required string DriverId { get; init; }
    [Range(-90, 90)] public double Latitude { get; init; }
    [Range(-180, 180)] public double Longitude { get; init; }
    public bool IsAvailable { get; init; }
}

/// <summary>Driver location update acknowledgement.</summary>
public sealed record DriverLocationResponseDto(string DriverId, bool IsAvailable, DateTimeOffset UpdatedAt);
