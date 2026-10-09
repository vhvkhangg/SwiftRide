using SwiftRide.MatchingService.Domain.Models;
using SwiftRide.MatchingService.Domain.Strategies;

namespace SwiftRide.MatchingService.Tests.Domain;

public sealed class PricingStrategyTests
{
    private static PricingContext Context(
        decimal distance = 10m,
        decimal time = 20m,
        decimal surge = 1.5m,
        decimal toll = 5m,
        decimal promotionFactor = 0.8m,
        decimal vat = 0.1m) =>
        new(distance, time, 12m, 2m, surge, toll, promotionFactor, vat);

    [Fact]
    public void StandardPricingStrategy_UsesSpecifiedFormula()
    {
        var result = new StandardPricingStrategy().CalculatePrice(Context());

        Assert.Equal(215.6m, result);
    }

    [Fact]
    public void SurgePricingStrategy_UsesSurgeMultiplier()
    {
        var result = new SurgePricingStrategy().CalculatePrice(Context(surge: 2m));

        Assert.Equal(286m, result);
    }

    [Fact]
    public void PromoPricingStrategy_ClampsPromotionFactorToOne()
    {
        var result = new PromoPricingStrategy().CalculatePrice(Context(promotionFactor: 2m));

        Assert.Equal(269.5m, result);
    }

    [Theory]
    [InlineData(-10, 20, 1.5, 5, 0.8, 0.1)]
    [InlineData(10, -20, 1.5, 5, 0.8, 0.1)]
    [InlineData(10, 20, -1.5, 5, 0.8, 0.1)]
    [InlineData(10, 20, 1.5, -5000, 0.8, 0.1)]
    [InlineData(10, 20, 1.5, 5, -2, 0.1)]
    public void AllStrategies_NeverReturnNegativePrice(
        decimal distance,
        decimal time,
        decimal surge,
        decimal toll,
        decimal promotionFactor,
        decimal vat)
    {
        var context = Context(distance, time, surge, toll, promotionFactor, vat);

        Assert.True(new StandardPricingStrategy().CalculatePrice(context) >= 0);
        Assert.True(new SurgePricingStrategy().CalculatePrice(context) >= 0);
        Assert.True(new PromoPricingStrategy().CalculatePrice(context) >= 0);
    }
}
