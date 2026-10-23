using MongoDB.Driver;
using Moq;
using SwiftRide.MatchingService.Application.DTOs;
using SwiftRide.MatchingService.Application.Services;
using SwiftRide.MatchingService.Domain.Interfaces;
using SwiftRide.MatchingService.Domain.Models;
using SwiftRide.MatchingService.Domain.Strategies;

namespace SwiftRide.MatchingService.Tests.Application;

public sealed class PricingServiceTests
{
    [Fact]
    public async Task CalculateAsync_UsesStandardPricingWhenNoSurgeRuleExists()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var service = CreateService(
            new DateTime(2026, 10, 23, 3, 0, 0, DateTimeKind.Utc),
            surgeRule: null);

        var result = await service.CalculateAsync(
            new QuoteRequest(10m, 20m, "Car", null),
            cancellationToken);

        Assert.Equal(ExpectedTotal(1m, 1m), result.Total);
        Assert.Equal(120m, result.Distance);
        Assert.Equal(40m, result.Time);
        Assert.Equal(0m, result.Discount);
    }

    [Fact]
    public async Task CalculateAsync_UsesSurgeRuleForLocalPeakHour()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var service = CreateService(
            new DateTime(2026, 10, 23, 11, 0, 0, DateTimeKind.Utc),
            new SurgeRuleDocument { StartHour = 17, EndHour = 19, Multiplier = 1.2m });

        var result = await service.CalculateAsync(
            new QuoteRequest(10m, 20m, "Car", null),
            cancellationToken);

        Assert.Equal(ExpectedTotal(1.2m, 1m), result.Total);
        Assert.Equal(160m * 0.2m, result.Surge);
    }

    [Fact]
    public async Task CalculateAsync_UsesActiveUnexpiredPromoDiscount()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var now = new DateTime(2026, 10, 23, 3, 0, 0, DateTimeKind.Utc);
        var service = CreateService(
            now,
            surgeRule: null,
            promoCode: new PromoCodeDocument
            {
                Code = "SWIFTRIDE2026",
                DiscountRate = 0.1m,
                IsActive = true,
                ExpiredAt = now.AddDays(1)
            });

        var result = await service.CalculateAsync(
            new QuoteRequest(10m, 20m, "Car", "SWIFTRIDE2026"),
            cancellationToken);

        Assert.Equal(ExpectedTotal(1m, 0.9m), result.Total);
        Assert.Equal(160m * 0.1m, result.Discount);
    }

    [Fact]
    public async Task CalculateAsync_IgnoresInvalidPromoCode()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var service = CreateService(
            new DateTime(2026, 10, 23, 3, 0, 0, DateTimeKind.Utc),
            surgeRule: null,
            promoCode: null);

        var result = await service.CalculateAsync(
            new QuoteRequest(10m, 20m, "Car", "WELCOME10"),
            cancellationToken);

        Assert.Equal(ExpectedTotal(1m, 1m), result.Total);
        Assert.Equal(0m, result.Discount);
    }

    [Theory]
    [InlineData(-1, 20)]
    [InlineData(10, -1)]
    public async Task CalculateAsync_ThrowsForInvalidInput(decimal distance, decimal time)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var service = CreateService(
            new DateTime(2026, 10, 23, 3, 0, 0, DateTimeKind.Utc),
            surgeRule: null);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.CalculateAsync(
                new QuoteRequest(distance, time, "Car", null),
                cancellationToken));
    }

    private static PricingService CreateService(
        DateTime utcNow,
        SurgeRuleDocument? surgeRule,
        PromoCodeDocument? promoCode = null)
    {
        var database = new Mock<IMongoDatabase>();
        database
            .Setup(item => item.GetCollection<SurgeRuleDocument>(
                "surge_rules",
                It.IsAny<MongoCollectionSettings>()))
            .Returns(CreateCollection(surgeRule));
        database
            .Setup(item => item.GetCollection<PromoCodeDocument>(
                "promo_codes",
                It.IsAny<MongoCollectionSettings>()))
            .Returns(CreateCollection(promoCode));

        return new PricingService(
            [
                new StandardPricingStrategy(),
                new SurgePricingStrategy(),
                new PromoPricingStrategy()
            ],
            new StubDateTimeProvider(utcNow),
            database.Object);
    }

    private static IMongoCollection<T> CreateCollection<T>(T? document)
    {
        var collection = new Mock<IMongoCollection<T>>();
        var cursor = new Mock<IAsyncCursor<T>>();
        cursor.SetupGet(item => item.Current)
            .Returns(document is null ? Array.Empty<T>() : [document]);
        cursor.SetupSequence(item => item.MoveNextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(document is not null)
            .ReturnsAsync(false);
        cursor.SetupSequence(item => item.MoveNext(It.IsAny<CancellationToken>()))
            .Returns(document is not null)
            .Returns(false);

        collection
            .Setup(item => item.FindAsync(
                It.IsAny<FilterDefinition<T>>(),
                It.IsAny<FindOptions<T, T>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(cursor.Object);
        return collection.Object;
    }

    private static decimal ExpectedTotal(decimal surgeMultiplier, decimal promotionFactor) =>
        160m * surgeMultiplier * promotionFactor * 1.1m;

    private sealed class StubDateTimeProvider(DateTime utcNow) : IDateTimeProvider
    {
        public DateTime UtcNow { get; } = utcNow;
    }
}
