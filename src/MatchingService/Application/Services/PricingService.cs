using SwiftRide.MatchingService.Application.Interfaces;
using SwiftRide.MatchingService.Domain.Interfaces;
using SwiftRide.MatchingService.Domain.Models;
using SwiftRide.MatchingService.Domain.Strategies;
using SwiftRide.MatchingService.Application.DTOs;

namespace SwiftRide.MatchingService.Application.Services;

public sealed class PricingService(IEnumerable<IPricingStrategy> strategies) : IPricingService
{
    private const decimal CostPerKm = 12m;
    private const decimal CostPerMinute = 2m;
    private const decimal VatRate = 0.1m;
    private const decimal Toll = 0m;

    public QuoteResponse Calculate(QuoteRequest request)
    {
        Validate(request);
        var promotionFactor = string.IsNullOrWhiteSpace(request.PromoCode) ? 1m : 0.9m;
        var context = new PricingContext(
            request.Distance, request.EstimatedTime, CostPerKm, CostPerMinute,
            request.Surge, Toll, promotionFactor, VatRate);
        var operating = CostPerKm * request.Distance + CostPerMinute * request.EstimatedTime;
        var strategy = SelectStrategy(request);
        var total = strategy.CalculatePrice(context);
        var discount = operating * request.Surge * (1m - promotionFactor);
        var tax = Math.Max(0m, total - ((operating * request.Surge + Toll) * promotionFactor));

        return new QuoteResponse(
            Base: 0m,
            Distance: CostPerKm * request.Distance,
            Time: CostPerMinute * request.EstimatedTime,
            Surge: operating * (request.Surge - 1m),
            Toll,
            Discount: Math.Max(0m, discount),
            Tax: Math.Max(0m, tax),
            Total: total);
    }

    private IPricingStrategy SelectStrategy(QuoteRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.PromoCode))
            return strategies.OfType<PromoPricingStrategy>().Single();
        if (request.Surge > 1m)
            return strategies.OfType<SurgePricingStrategy>().Single();
        return strategies.OfType<StandardPricingStrategy>().Single();
    }

    private static void Validate(QuoteRequest request)
    {
        if (request.Distance < 0 || request.EstimatedTime < 0)
            throw new ArgumentOutOfRangeException(nameof(request), "Khoảng cách và thời gian dự kiến không được nhỏ hơn 0.");
        if (request.Surge <= 0)
            throw new ArgumentOutOfRangeException(nameof(request), "Hệ số giá tăng (Surge multiplier) phải lớn hơn 0.");
    }
}
