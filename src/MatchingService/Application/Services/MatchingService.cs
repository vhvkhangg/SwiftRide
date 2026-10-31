using SwiftRide.MatchingService.Application.Interfaces;
using SwiftRide.MatchingService.Domain.Interfaces;
using SwiftRide.MatchingService.Application.DTOs;
using SwiftRide.Contracts.Matching;
using SwiftRide.MatchingService.Domain.Models;

namespace SwiftRide.MatchingService.Application.Services;

public sealed class MatchingService(IDriverRepository repository) : IMatchingService
{
    private static readonly TimeSpan ReservationDuration = TimeSpan.FromMinutes(2);

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
        return new MatchResponse(request.TripId, drivers
            .Where(item => Guid.TryParse(item.Driver.DriverId, out var id) && id != Guid.Empty)
            .Select(item =>
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

    public async Task<MatchedDriver?> FindNextDriverAsync(
        FindDriverForTripV1 request,
        CancellationToken cancellationToken)
    {
        if (request.TripId == Guid.Empty)
            throw new ArgumentException("TripId là bắt buộc.", nameof(request));
        if (request.Attempt < 1)
            throw new ArgumentOutOfRangeException(nameof(request), "Attempt phải lớn hơn 0.");
        if (!double.IsFinite(request.PickupLatitude) || request.PickupLatitude is < -90 or > 90 ||
            !double.IsFinite(request.PickupLongitude) || request.PickupLongitude is < -180 or > 180)
            throw new ArgumentOutOfRangeException(nameof(request), "Tọa độ điểm đón không hợp lệ.");
        if (!double.IsFinite(request.RadiusKm) || request.RadiusKm <= 0)
            throw new ArgumentOutOfRangeException(nameof(request), "Bán kính phải lớn hơn 0.");
        if (request.ExcludedDriverIds is null)
            throw new ArgumentException("ExcludedDriverIds không được để trống.", nameof(request));

        var existingReservation = await repository.FindReservationAsync(
            request.TripId,
            request.Attempt,
            cancellationToken);
        if (existingReservation is not null)
            return ToMatchedDriver(existingReservation, 0);

        var excluded = request.ExcludedDriverIds.Select(id =>
        {
            if (id == Guid.Empty)
                throw new ArgumentException("ExcludedDriverIds không được chứa GUID rỗng.", nameof(request));
            return id.ToString("D");
        }).ToArray();
        var result = await repository.FindNearestAvailableAsync(
            request.PickupLatitude,
            request.PickupLongitude,
            request.RadiusKm,
            excluded,
            cancellationToken);

        if (result is null) return null;
        if (!Guid.TryParse(result.Value.Driver.DriverId, out var driverId) || driverId == Guid.Empty)
            return null;

        var reserved = await repository.TryReserveAsync(
            result.Value.Driver.DriverId,
            request.TripId,
            request.Attempt,
            DateTime.UtcNow.Add(ReservationDuration),
            cancellationToken);
        if (!reserved)
            return null;

        return ToMatchedDriver(result.Value.Driver, result.Value.DistanceKm);
    }

    private static MatchedDriver ToMatchedDriver(DriverLocation driver, double distanceKm)
    {
        if (!Guid.TryParse(driver.DriverId, out var driverId) || driverId == Guid.Empty)
            throw new InvalidOperationException("Matching DB chứa DriverId không hợp lệ.");

        var coordinates = driver.Location.Coordinates;
        return new MatchedDriver(
            driverId.ToString("D"),
            coordinates.Latitude,
            coordinates.Longitude,
            distanceKm,
            new DateTimeOffset(driver.UpdatedAt, TimeSpan.Zero));
    }
}
