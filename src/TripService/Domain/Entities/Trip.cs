using System;
using System.ComponentModel.DataAnnotations.Schema;
using SwiftRide.TripService.Domain.Enums;
using SwiftRide.TripService.Domain.States;

namespace SwiftRide.TripService.Domain.Entities;

public sealed class Trip
{
    public Guid Id { get; private set; }
    public Guid RiderId { get; private set; }
    public Guid? DriverId { get; private set; }
    public string PickupAddress { get; private set; } = string.Empty;
    public string DropoffAddress { get; private set; } = string.Empty;
    public decimal? FareEstimate { get; private set; }
    public TripStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    // Biến lưu trạng thái  
    [NotMapped]
    private TripState _state = null!;

    private Trip() { }

    public static Trip Create(Guid riderId, string pickup, string dropoff, decimal? fare)
    {
        var trip = new Trip
        {
            Id = Guid.NewGuid(),
            RiderId = riderId,
            PickupAddress = pickup,
            DropoffAddress = dropoff,
            FareEstimate = fare,
            CreatedAt = DateTime.UtcNow,
            Status = TripStatus.Requested
        };

        trip._state = new RequestedState();

        return trip;
    }

    // Hàm để phục hồi _state từ Status 
    public void RehydrateState()
    {
        // Khởi tạo state dựa vào Status lấy từ Database
        _state = Status switch
        {
            TripStatus.Requested => new RequestedState(),
            TripStatus.DriverAccepted => new DriverAcceptedState(),
            TripStatus.EnRoute => new EnRouteState(),
            TripStatus.PickedUp => new PickedUpState(),
            TripStatus.DroppedOff => new DroppedOffState(),
            TripStatus.PaymentPending => new PaymentPendingState(),
            TripStatus.Paid => new PaidState(),
            TripStatus.Failed => new FailedState(),
            TripStatus.Cancelled => new CancelledState(),
            _ => throw new NotImplementedException($"Chưa hỗ trợ Rehydrate cho status {Status}")
        };
    }

    // --- Các hàm Uỷ thác (Delegate) chuyển việc cho _state ---

    public void AcceptByDriver(Guid driverId)
    {
        if (_state == null) throw new InvalidOperationException("State is not initialized. Call RehydrateState first.");
        
        _state = _state.AcceptByDriver(driverId);
        DriverId = driverId;
        Status = _state.Status;
        UpdatedAt = DateTime.UtcNow;
    }

    public void StartEnRoute()
    {
        _state = _state.StartEnRoute();
        Status = _state.Status;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Pickup()
    {
        _state = _state.Pickup();
        Status = _state.Status;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Dropoff()
    {
        _state = _state.Dropoff();
        Status = _state.Status;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Cancel()
    {
        _state = _state.Cancel();
        Status = _state.Status;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkPaymentPending()
    {
        _state = _state.MarkPaymentPending();
        Status = _state.Status;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkPaid()
    {
        _state = _state.MarkPaid();
        Status = _state.Status;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkFailed()
    {
        _state = _state.MarkFailed();
        Status = _state.Status;
        UpdatedAt = DateTime.UtcNow;
    }
}
