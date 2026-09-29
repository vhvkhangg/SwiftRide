using SwiftRide.TripService.Infrastructure;

namespace SwiftRide.TripService.Tests;

public sealed class ScaffoldTests
{
    [Fact]
    public void TripServiceAssembly_IsReferencedByTestProject()
    {
        Assert.Equal("TripService", typeof(TripDbContext).Assembly.GetName().Name);
    }
}
