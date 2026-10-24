using SwiftRide.TripService.Application.DTOs;

namespace SwiftRide.TripService.Application.Interfaces;

public interface IMatchingPricingClient
{
    Task<MatchingQuoteResponse> GetQuoteAsync(MatchingQuoteRequest request, CancellationToken ct);
}
