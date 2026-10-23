using SwiftRide.MatchingService.Application.Interfaces;
using SwiftRide.MatchingService.Domain.Interfaces;
using SwiftRide.MatchingService.Domain.Models;
using SwiftRide.MatchingService.Domain.Strategies;
using SwiftRide.MatchingService.Application.DTOs;
using MongoDB.Driver;

namespace SwiftRide.MatchingService.Application.Services;

public sealed class PricingService(
    IEnumerable<IPricingStrategy> strategies,
    IDateTimeProvider dateTimeProvider,
    IMongoDatabase database) : IPricingService
{
    private const decimal CostPerKm = 12m;
    private const decimal CostPerMinute = 2m;
    private const decimal VatRate = 0.1m;
    private const decimal Toll = 0m;

    public async Task<QuoteResponse> CalculateAsync(
        QuoteRequest request,
        CancellationToken cancellationToken = default)
    {
        Validate(request);
        var utcNow = dateTimeProvider.UtcNow;
        var localHour = utcNow.AddHours(7).Hour;
        var surgeMultiplier = await GetSurgeMultiplierAsync(localHour, cancellationToken);
        var discountRate = await GetDiscountRateAsync(request.PromoCode, utcNow, cancellationToken);
        var promotionFactor = 1m - discountRate;
        var context = new PricingContext(
            request.Distance, request.EstimatedTime, CostPerKm, CostPerMinute,
            surgeMultiplier, Toll, promotionFactor, VatRate);
        var operating = CostPerKm * request.Distance + CostPerMinute * request.EstimatedTime;
        var strategy = SelectStrategy(discountRate, surgeMultiplier);
        var total = strategy.CalculatePrice(context);
        var discount = operating * surgeMultiplier * (1m - promotionFactor);
        var tax = Math.Max(0m, total - ((operating * surgeMultiplier + Toll) * promotionFactor));

        return new QuoteResponse(
            Base: 0m,
            Distance: CostPerKm * request.Distance,
            Time: CostPerMinute * request.EstimatedTime,
            Surge: operating * (surgeMultiplier - 1m),
            Toll,
            Discount: Math.Max(0m, discount),
            Tax: Math.Max(0m, tax),
            Total: total);
    }

    private IPricingStrategy SelectStrategy(decimal discountRate, decimal surgeMultiplier)
    {
        if (discountRate > 0m)
            return strategies.OfType<PromoPricingStrategy>().Single();
        if (surgeMultiplier > 1m)
            return strategies.OfType<SurgePricingStrategy>().Single();
        return strategies.OfType<StandardPricingStrategy>().Single();
    }

    private async Task<decimal> GetSurgeMultiplierAsync(
        int localHour,
        CancellationToken cancellationToken)
    {
        var collection = database.GetCollection<SurgeRuleDocument>("surge_rules");
        var filter = Builders<SurgeRuleDocument>.Filter.And(
            Builders<SurgeRuleDocument>.Filter.Lte(rule => rule.StartHour, localHour),
            Builders<SurgeRuleDocument>.Filter.Gt(rule => rule.EndHour, localHour));
        var cursor = await collection.FindAsync(filter, cancellationToken: cancellationToken);
        var rule = await cursor.FirstOrDefaultAsync(cancellationToken);
        return rule?.Multiplier ?? 1.0m;
    }

    private async Task<decimal> GetDiscountRateAsync(
        string? promoCode,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(promoCode))
            return 0m;

        var collection = database.GetCollection<PromoCodeDocument>("promo_codes");
        var filter = Builders<PromoCodeDocument>.Filter.And(
            Builders<PromoCodeDocument>.Filter.Eq(document => document.Code, promoCode),
            Builders<PromoCodeDocument>.Filter.Eq(document => document.IsActive, true),
            Builders<PromoCodeDocument>.Filter.Gt(document => document.ExpiredAt, utcNow));
        var cursor = await collection.FindAsync(filter, cancellationToken: cancellationToken);
        var document = await cursor.FirstOrDefaultAsync(cancellationToken);
        return document is null ? 0m : Math.Clamp(document.DiscountRate, 0m, 1m);
    }

    private static void Validate(QuoteRequest request)
    {
        if (request.Distance < 0 || request.EstimatedTime < 0)
            throw new ArgumentOutOfRangeException(nameof(request), "Khoảng cách và thời gian dự kiến không được nhỏ hơn 0.");
    }
}
