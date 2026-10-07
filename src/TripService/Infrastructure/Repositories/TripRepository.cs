using System;
using System.Threading;
using System.Threading.Tasks;
using SwiftRide.TripService.Domain.Entities;
using SwiftRide.TripService.Domain.Interfaces;

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
}
