using Microsoft.EntityFrameworkCore;
using SwiftRide.TripService.Application.Interfaces;

namespace SwiftRide.TripService.Infrastructure;

public sealed class TripUnitOfWork(TripDbContext db) : ITripUnitOfWork
{
    public Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken ct) =>
        ExecuteAsync(async token =>
        {
            await operation(token);
            return true;
        }, ct);

    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var result = await operation(ct);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }
}
