using System;
using System.Threading;
using System.Threading.Tasks;
using SwiftRide.Contracts.Trips;
using SwiftRide.Messaging;
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
    private readonly MessagePublisher _publisher;

    public TripApplicationService(ITripWriter writer, ITripReader reader, MessagePublisher publisher)
    {
        _writer = writer;
        _reader = reader;
        _publisher = publisher;
    }

    public async Task<TripResponse> CreateAsync(CreateTripRequest request, CancellationToken ct = default)
    {
        if (request.RiderId == Guid.Empty ||
            string.IsNullOrWhiteSpace(request.PickupAddress) ||
            string.IsNullOrWhiteSpace(request.DropoffAddress) ||
            request.PickupAddress.Length > 500 || request.DropoffAddress.Length > 500)
        {
            throw new ArgumentException("Cần RiderId và hai địa chỉ hợp lệ, tối đa 500 ký tự.");
        }

        if (!request.PickupLatitude.HasValue || !request.PickupLongitude.HasValue ||
            !double.IsFinite(request.PickupLatitude.Value) ||
            !double.IsFinite(request.PickupLongitude.Value) ||
            request.PickupLatitude.Value < -90 || request.PickupLatitude.Value > 90 ||
            request.PickupLongitude.Value < -180 || request.PickupLongitude.Value > 180)
        {
            throw new ArgumentException("Cần PickupLatitude từ -90 đến 90 và PickupLongitude từ -180 đến 180.");
        }

        var trip = Trip.Create(
            request.RiderId,
            request.PickupAddress,
            request.DropoffAddress,
            request.FareEstimate);

        await _writer.AddAsync(trip, ct);

        var message = new TripRequestedV1
        {
            CorrelationId = trip.Id,
            TripId = trip.Id,
            RiderId = trip.RiderId,
            PickupAddress = trip.PickupAddress,
            DropoffAddress = trip.DropoffAddress,
            PickupLatitude = request.PickupLatitude,
            PickupLongitude = request.PickupLongitude,
            OccurredAtUtc = new DateTimeOffset(trip.CreatedAt, TimeSpan.Zero)
        };

        try
        {
            await _publisher.PublishAsync(message, ct);
        }
        catch (Exception ex)
        {
            // Giữ ID chuyến đã lưu để client không tạo lại chuyến khi gửi message lỗi.
            throw new TripPublishException(trip.Id, ex);
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

public sealed class TripPublishException : Exception
{
    public Guid TripId { get; }

    public TripPublishException(Guid tripId, Exception innerException)
        : base("Chuyến đã lưu nhưng chưa xác nhận được việc gửi message.", innerException)
    {
        TripId = tripId;
    }
}
