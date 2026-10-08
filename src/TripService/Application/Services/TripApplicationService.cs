using System;
using System.Threading;
using System.Threading.Tasks;
using SwiftRide.TripService.Application.DTOs;
using SwiftRide.TripService.Application.Interfaces;
using SwiftRide.TripService.Domain.Entities;
using SwiftRide.TripService.Domain.Enums;
using SwiftRide.TripService.Domain.Interfaces;

namespace SwiftRide.TripService.Application.Services;

public sealed class TripApplicationService : ITripApplicationService
{
    private readonly ITripWriter _writer;
    private readonly ITripReader _reader;

    public TripApplicationService(ITripWriter writer, ITripReader reader)
    {
        _writer = writer;
        _reader = reader;
    }

    public async Task<TripResponse> CreateAsync(CreateTripRequest request, CancellationToken ct = default)
    {
        var trip = Trip.Create(
            request.RiderId,
            request.PickupAddress,
            request.DropoffAddress,
            request.FareEstimate);

        await _writer.AddAsync(trip, ct);

        return new TripResponse(
            trip.Id,
            trip.RiderId,
            trip.DriverId,
            trip.PickupAddress,
            trip.DropoffAddress,
            trip.FareEstimate,
            trip.Status,
            trip.CreatedAt,
            trip.UpdatedAt
        );
    }

    // --- CÁC HÀM DƯỚI ĐÂY SẼ LÀM Ở CÁC BƯỚC TIẾP THEO ---

    public async Task<TripResponse> GetByIdAsync(
    Guid tripId,
    CancellationToken ct = default)
    {
        var trip = await _reader.GetByIdAsync(tripId, ct);

        if (trip is null)
        {
            throw new KeyNotFoundException(
                $"Không tìm thấy chuyến đi '{tripId}'.");
        }

        return new TripResponse(
            trip.Id,
            trip.RiderId,
            trip.DriverId,
            trip.PickupAddress,
            trip.DropoffAddress,
            trip.FareEstimate,
            trip.Status,
            trip.CreatedAt,
            trip.UpdatedAt
        );
    }

    public async Task DriverAcceptAsync(Guid tripId, DriverAcceptRequest request, CancellationToken ct = default)
    {
        var trip = await _reader.GetByIdAsync(tripId, ct);
        if (trip == null) throw new Exception("Không tìm thấy chuyến đi.");

        trip.AcceptByDriver(request.DriverId);
        await _writer.SaveAsync(trip, ct);
    }

    public async Task StartEnRouteAsync(Guid tripId, CancellationToken ct = default)
    {
        var trip = await _reader.GetByIdAsync(tripId, ct);
        if (trip == null) throw new Exception("Không tìm thấy chuyến đi.");

        trip.StartEnRoute();
        await _writer.SaveAsync(trip, ct);
    }

    public async Task PickupAsync(Guid tripId, CancellationToken ct = default)
    {
        var trip = await _reader.GetByIdAsync(tripId, ct);
        if (trip == null) throw new Exception("Không tìm thấy chuyến đi.");

        trip.Pickup();
        await _writer.SaveAsync(trip, ct);
    }
    public async Task DropoffAsync(Guid tripId, CancellationToken ct = default)
    {
        var trip = await _reader.GetByIdAsync(tripId, ct);
        if (trip == null) throw new Exception("Không tìm thấy chuyến đi.");

        trip.Dropoff();
        await _writer.SaveAsync(trip, ct);
    }

    public async Task CancelAsync(Guid tripId, CancellationToken ct = default)
    {
        var trip = await _reader.GetByIdAsync(tripId, ct);
        if (trip == null) throw new Exception("Không tìm thấy chuyến đi.");

        trip.Cancel();
        await _writer.SaveAsync(trip, ct);
    }

    public async Task MarkPaymentPendingAsync(Guid tripId, CancellationToken ct = default)
    {
        var trip = await _reader.GetByIdAsync(tripId, ct);
        if (trip == null) throw new Exception("Không tìm thấy chuyến đi.");

        trip.MarkPaymentPending();
        await _writer.SaveAsync(trip, ct);
    }

    public async Task MarkPaidAsync(Guid tripId, CancellationToken ct = default)
    {
        var trip = await _reader.GetByIdAsync(tripId, ct);
        if (trip == null) throw new Exception("Không tìm thấy chuyến đi.");

        // Bỏ qua nếu ĐÃ thanh toán xong từ trước
        if (trip.Status == TripStatus.Paid) return;

        trip.MarkPaid();
        await _writer.SaveAsync(trip, ct);
    }

    public async Task MarkFailedAsync(Guid tripId, CancellationToken ct = default)
    {
        var trip = await _reader.GetByIdAsync(tripId, ct);
        if (trip == null) throw new Exception("Không tìm thấy chuyến đi.");

        trip.MarkFailed();
        await _writer.SaveAsync(trip, ct);
    }
}
