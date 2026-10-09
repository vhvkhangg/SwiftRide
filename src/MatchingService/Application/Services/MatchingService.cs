using SwiftRide.MatchingService.Application.Interfaces;
using SwiftRide.MatchingService.Domain.Interfaces;
using SwiftRide.MatchingService.Application.DTOs;

namespace SwiftRide.MatchingService.Application.Services;

public sealed class MatchingService(IDriverRepository repository) : IMatchingService
{
    public async Task<MatchResponse> MatchAsync(MatchRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TripId))
            throw new ArgumentException("Mã chuyến đi (TripId) là bắt buộc.", nameof(request));
        if (request.PickupLatitude is < -90 or > 90 || request.PickupLongitude is < -180 or > 180)
            throw new ArgumentOutOfRangeException(nameof(request), "Tọa độ điểm đón không hợp lệ.");
        if (request.RadiusKm <= 0)
            throw new ArgumentOutOfRangeException(nameof(request), "Bán kính (RadiusKm) phải lớn hơn 0.");

        var drivers = await repository.FindNearbyAvailableAsync(
            request.PickupLatitude, request.PickupLongitude, request.RadiusKm, 10, cancellationToken);
        return new MatchResponse(request.TripId, drivers.Select(item =>
        {
            var coordinates = item.Driver.Location.Coordinates;
            return new MatchedDriver(
                item.Driver.DriverId,
                coordinates.Latitude,
                coordinates.Longitude,
                item.DistanceKm,
                new DateTimeOffset(item.Driver.UpdatedAt, TimeSpan.Zero));
        }).ToArray());
    }
}
