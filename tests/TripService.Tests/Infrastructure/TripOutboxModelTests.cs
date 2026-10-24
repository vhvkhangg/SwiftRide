using Microsoft.EntityFrameworkCore;
using SwiftRide.TripService.Infrastructure;
using Xunit;

namespace SwiftRide.TripService.Tests.Infrastructure;

public sealed class TripOutboxModelTests
{
    private static TripDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TripDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=model_test;Username=unused;Password=unused")
            .Options;

        return new TripDbContext(options);
    }

    [Fact]
    public void DbContext_MapsMassTransitInboxAndOutboxTables()
    {
        using var db = CreateContext();
        var tables = db.Model.GetEntityTypes()
            .Select(entity => entity.GetTableName())
            .ToHashSet();

        Assert.Contains("InboxState", tables);
        Assert.Contains("OutboxMessage", tables);
        Assert.Contains("OutboxState", tables);
    }

    [Fact]
    public void OutboxMigration_IsRegistered()
    {
        using var db = CreateContext();
        var migrations = db.Database.GetMigrations();

        Assert.Contains(migrations, migration => migration.EndsWith("_AddTripOutbox", StringComparison.Ordinal));
    }
}
