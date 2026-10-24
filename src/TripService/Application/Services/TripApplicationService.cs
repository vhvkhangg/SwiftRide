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
    private readonly IMatchingPricingClient _matchingPricing;

    public TripApplicationService(
        ITripWriter writer,
        ITripReader reader,
        IMatchingPricingClient matchingPricing)
    {
        _writer = writer;
        _reader = reader;
        _matchingPricing = matchingPricing;
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

        if (request.Distance < 0 || request.EstimatedTime < 0 || request.Surge <= 0)
        {
            throw new ArgumentException("Quãng đường và thời gian không được âm; hệ số surge phải lớn hơn 0.");
        }

        if (!request.PickupLatitude.HasValue || !request.PickupLongitude.HasValue ||
            !double.IsFinite(request.PickupLatitude.Value) ||
            !double.IsFinite(request.PickupLongitude.Value) ||
            request.PickupLatitude.Value < -90 || request.PickupLatitude.Value > 90 ||
            request.PickupLongitude.Value < -180 || request.PickupLongitude.Value > 180)
        {
            throw new ArgumentException("Cần PickupLatitude từ -90 đến 90 và PickupLongitude từ -180 đến 180.");
        }

        var quote = await _matchingPricing.GetQuoteAsync(
            new MatchingQuoteRequest(
                request.Distance,
                request.EstimatedTime,
                request.RideType,
                request.Surge,
                request.PromoCode),
            ct);

        var trip = Trip.Create(
            request.RiderId,
            request.PickupAddress,
            request.DropoffAddress,
            quote.Total);

        await _writer.AddAsync(trip, ct);

        return ToResponse(trip);
    }

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

        return ToResponse(trip);
    }

    public async Task ConfirmPriceAsync(Guid tripId, CancellationToken ct = default)
    {
        var trip = await _reader.GetByIdAsync(tripId, ct);
        if (trip is null) throw new KeyNotFoundException($"Không tìm thấy chuyến đi '{tripId}'.");

        trip.ConfirmPrice();
        await _writer.SaveAsync(trip, ct);
    }

    public async Task DriverAcceptAsync(Guid tripId, DriverAcceptRequest request, CancellationToken ct = default)
    {
        var trip = await _reader.GetByIdAsync(tripId, ct);
        if (trip == null) throw new KeyNotFoundException($"Không tìm thấy chuyến đi '{tripId}'.");

        trip.AcceptByDriver(request.DriverId);
        await _writer.SaveAsync(trip, ct);
    }

    public async Task StartEnRouteAsync(Guid tripId, CancellationToken ct = default)
    {
        var trip = await _reader.GetByIdAsync(tripId, ct);
        if (trip == null) throw new KeyNotFoundException($"Không tìm thấy chuyến đi '{tripId}'.");

        trip.StartEnRoute();
        await _writer.SaveAsync(trip, ct);
    }

    public async Task PickupAsync(Guid tripId, CancellationToken ct = default)
    {
        var trip = await _reader.GetByIdAsync(tripId, ct);
        if (trip == null) throw new KeyNotFoundException($"Không tìm thấy chuyến đi '{tripId}'.");

        trip.Pickup();
        await _writer.SaveAsync(trip, ct);
    }
    public async Task DropoffAsync(Guid tripId, CancellationToken ct = default)
    {
        var trip = await _reader.GetByIdAsync(tripId, ct);
        if (trip == null) throw new KeyNotFoundException($"Không tìm thấy chuyến đi '{tripId}'.");

        trip.Dropoff();
        trip.MarkPaymentPending();
        await _writer.SaveAsync(trip, ct);
    }

    public async Task CancelAsync(Guid tripId, CancellationToken ct = default)
    {
        var trip = await _reader.GetByIdAsync(tripId, ct);
        if (trip == null) throw new KeyNotFoundException($"Không tìm thấy chuyến đi '{tripId}'.");

        trip.Cancel();
        await _writer.SaveAsync(trip, ct);
    }

    public async Task MarkPaymentPendingAsync(Guid tripId, CancellationToken ct = default)
    {
        var trip = await _reader.GetByIdAsync(tripId, ct);
        if (trip == null) throw new KeyNotFoundException($"Không tìm thấy chuyến đi '{tripId}'.");

        trip.MarkPaymentPending();
        await _writer.SaveAsync(trip, ct);
    }

    public async Task MarkPaidAsync(Guid tripId, CancellationToken ct = default)
    {
        var trip = await _reader.GetByIdAsync(tripId, ct);
        if (trip == null) throw new KeyNotFoundException($"Không tìm thấy chuyến đi '{tripId}'.");

        // Bỏ qua nếu ĐÃ thanh toán xong từ trước
        if (trip.Status == TripStatus.Paid) return;

        trip.MarkPaid();
        await _writer.SaveAsync(trip, ct);
    }

    public async Task MarkFailedAsync(Guid tripId, CancellationToken ct = default)
    {
        var trip = await _reader.GetByIdAsync(tripId, ct);
        if (trip == null) throw new KeyNotFoundException($"Không tìm thấy chuyến đi '{tripId}'.");

        trip.MarkFailed();
        await _writer.SaveAsync(trip, ct);
    }

    private static TripResponse ToResponse(Trip trip)
    {
        return new TripResponse(
            trip.Id,
            trip.RiderId,
            trip.DriverId,
            trip.PickupAddress,
            trip.DropoffAddress,
            trip.FareEstimate,
            trip.Status,
            trip.CreatedAt,
            trip.UpdatedAt);
    }
}
