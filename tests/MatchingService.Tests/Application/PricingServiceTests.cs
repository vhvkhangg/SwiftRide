using SwiftRide.MatchingService.Application.Services;
using SwiftRide.MatchingService.Application.DTOs;
using SwiftRide.MatchingService.Domain.Strategies;

namespace SwiftRide.MatchingService.Tests.Application;

public sealed class PricingServiceTests
{
    private static PricingService CreateService() =>
        new([
            new StandardPricingStrategy(),
            new SurgePricingStrategy(),
            new PromoPricingStrategy()
        ]);

    [Fact]
    public void Calculate_UsesStandardStrategy_WhenNoSurgeOrPromo()
    {
        var result = CreateService().Calculate(new QuoteRequest(10m, 20m, "Car", 1m, null));

        Assert.Equal(176m, result.Total);
        Assert.Equal(120m, result.Distance);
        Assert.Equal(40m, result.Time);
        Assert.Equal(0m, result.Discount);
    }

    [Fact]
    public void Calculate_UsesSurgeStrategy_WhenSurgeIsGreaterThanOne()
    {
        var result = CreateService().Calculate(new QuoteRequest(10m, 20m, "Car", 1.5m, null));

        Assert.Equal(264m, result.Total);
        Assert.Equal(80m, result.Surge);
    }

    [Fact]
    public void Calculate_UsesPromoStrategy_WhenPromoCodeIsPresent()
    {
        var result = CreateService().Calculate(new QuoteRequest(10m, 20m, "Car", 1m, "WELCOME10"));

        Assert.Equal(158.4m, result.Total);
        Assert.Equal(16m, result.Discount);
    }

    [Theory]
    [InlineData(-1, 20, 1)]
    [InlineData(10, -1, 1)]
    [InlineData(10, 20, 0)]
    public void Calculate_ThrowsForInvalidInput(decimal distance, decimal time, decimal surge)
    {
        var request = new QuoteRequest(distance, time, "Car", surge, null);

        Assert.Throws<ArgumentOutOfRangeException>(() => CreateService().Calculate(request));
    }
}
