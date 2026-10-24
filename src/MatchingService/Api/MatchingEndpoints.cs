using SwiftRide.Security;
using MongoDB.Driver.GeoJsonObjectModel;
using SwiftRide.MatchingService.Application.Interfaces;
using SwiftRide.MatchingService.Domain.Interfaces;

namespace SwiftRide.MatchingService.Api;

public static class MatchingEndpoints
{
    public static void MapMatchingEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/quote", (QuoteRequestDto request, IPricingService service) =>
        {
            var result = service.Calculate(request.ToDomain());
            return Results.Ok(new QuoteResponseDto(result.Base, result.Distance, result.Time, result.Surge,
                result.Toll, result.Discount, result.Tax, result.Total));
        })
        .WithSummary("Tính toán báo giá chuyến đi")
        .WithTags("Định giá (Pricing)")
        .Produces<QuoteResponseDto>().RequireAuthorization(p => p.RequireRole("Rider"));

        app.MapPost("/internal/quote", (QuoteRequestDto request, IPricingService service) =>
        {
            var result = service.Calculate(request.ToDomain());
            return Results.Ok(new QuoteResponseDto(result.Base, result.Distance, result.Time, result.Surge,
                result.Toll, result.Discount, result.Tax, result.Total));
        })
        .WithSummary("Tính báo giá cho Trip Service")
        .WithTags("Định giá (Pricing)")
        .Produces<QuoteResponseDto>().RequireAuthorization("InternalPricing");

        _ = app.MapPost("/match", async (MatchRequestDto request, IMatchingService service, TripAccessClient trips, HttpContext context, CancellationToken ct) =>
        {
            if (!Guid.TryParse(request.TripId, out var tripId)) return Results.BadRequest();
            var trip = await trips.GetAsync(tripId, ct);
            if (trip is null) return Results.NotFound();
            if (trip.RiderId != context.User.UserId()) return Results.Forbid();
            if (!string.Equals(trip.Status, "PriceConfirmed", StringComparison.Ordinal))
                return Results.Conflict();

            var result = await service.MatchAsync(request.ToDomain(), ct);
            var candidate = result.Drivers.FirstOrDefault();
            if (candidate is not null && Guid.TryParse(candidate.DriverId, out var driverId))
                await trips.InviteAsync(tripId, driverId, ct);

            var response = new MatchResponseDto(result.TripId,
                candidate is null
                    ? []
                    : [new MatchedDriverDto(candidate.DriverId, candidate.Latitude, candidate.Longitude,
                        candidate.DistanceKm, candidate.UpdatedAt)]);
            return Results.Ok(response);
        })
        .WithSummary("Tìm các tài xế trống ở gần")
        .WithTags("Ghép đôi (Matching)")
        .Produces<MatchResponseDto>().RequireAuthorization(p => p.RequireRole("Rider"));
    }

    public static void MapDriverEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/drivers/location", async (
            DriverLocationRequestDto request, IDriverRepository repository, HttpContext context, CancellationToken ct) =>
        {
            request = request with { DriverId = context.User.UserId().ToString() };
            var point = GeoJson.Point(GeoJson.Geographic(request.Longitude, request.Latitude));
            await repository.UpsertLocationAsync(request.DriverId, point, request.IsAvailable, ct);
            return Results.Ok(new DriverLocationResponseDto(request.DriverId, request.IsAvailable, DateTimeOffset.UtcNow));
        })
        .WithSummary("Cập nhật vị trí GPS của tài xế")
        .WithTags("Tài xế (Drivers)")
        .Produces<DriverLocationResponseDto>().RequireAuthorization(p => p.RequireRole("Driver"));
    }
}