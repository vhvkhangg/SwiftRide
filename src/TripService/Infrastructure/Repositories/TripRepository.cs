using System;
using System.Threading;
using System.Threading.Tasks;
using SwiftRide.TripService.Domain.Entities;
using SwiftRide.TripService.Domain.Enums;
using SwiftRide.TripService.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace SwiftRide.TripService.Infrastructure.Repositories;

public sealed class TripRepository : ITripReader, ITripWriter
{
    private readonly TripDbContext _db;

    public TripRepository(TripDbContext db) => _db = db;

    public async Task<Trip?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        // Load entity từ DB
        var trip = await _db.Trips.FindAsync(new object[] { id }, ct);

        // Cần phục hồi state nếu trip tồn tại
        trip?.RehydrateState();

        return trip;
    }

    public async Task AddAsync(Trip trip, CancellationToken ct = default)
    {
        await _db.Trips.AddAsync(trip, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task SaveAsync(Trip trip, CancellationToken ct = default)
    {
        _db.Trips.Update(trip);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<Trip?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default)
    {
        var trip = await _db.Trips.FirstOrDefaultAsync(t => t.IdempotencyKey == idempotencyKey, ct);
        trip?.RehydrateState();
        return trip;
    }

    public async Task<Trip?> GetActiveTripAsync(Guid userId, string role, CancellationToken ct = default)
    {
        // Các trạng thái được coi là Active (Chưa kết thúc hoàn toàn)
        var activeStatuses = new[]
        {
        TripStatus.Requested, TripStatus.PriceConfirmed, TripStatus.DriverAccepted,
        TripStatus.EnRoute, TripStatus.PickedUp, TripStatus.DroppedOff, TripStatus.PaymentPending
    };

        var query = _db.Trips.Where(t => activeStatuses.Contains(t.Status));

        if (role == "Rider")
            query = query.Where(t => t.RiderId == userId);
        else if (role == "Driver")
            query = query.Where(t => t.DriverId == userId);
        else
            return null;

        // Lấy chuyến đi mới nhất đang active
        var trip = await query.OrderByDescending(t => t.CreatedAt).FirstOrDefaultAsync(ct);
        trip?.RehydrateState();
        return trip;
    }
}
