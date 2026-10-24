using SwiftRide.MatchingService.Application.DTOs;

namespace SwiftRide.MatchingService.Application.Interfaces;

public interface IPricingService
{
    Task<QuoteResponse> CalculateAsync(
        QuoteRequest request,
        CancellationToken cancellationToken = default);
}
