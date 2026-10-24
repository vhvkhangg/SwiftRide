using System.Net.Http.Json;
using SwiftRide.TripService.Application.DTOs;
using SwiftRide.TripService.Application.Interfaces;

namespace SwiftRide.TripService.Infrastructure.Clients;

public sealed class MatchingPricingClient(HttpClient client) : IMatchingPricingClient
{
    public async Task<MatchingQuoteResponse> GetQuoteAsync(MatchingQuoteRequest request, CancellationToken ct)
    {
        using var response = await client.PostAsJsonAsync("internal/quote", request, ct);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<MatchingQuoteResponse>(ct)
            ?? throw new InvalidOperationException("Matching trả về báo giá rỗng.");
    }
}
