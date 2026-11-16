using Microsoft.Extensions.Options;
using Moq;

namespace SwiftRide.TripService.Tests;

internal static class TestOptions
{
    public static IOptionsMonitor<T> Monitor<T>(T value) where T : class =>
        Mock.Of<IOptionsMonitor<T>>(m => m.CurrentValue == value);
}
