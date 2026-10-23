using System.Net;
using System.Net.Http.Json;
using SwiftRide.Security;

namespace SwiftRide.MatchingService.Api;

// All service-to-service requests use a dedicated internal token, never the client's identity headers.
public sealed class TripAccessClient(HttpClient client)
{
    public async Task<MatchingTrip?> GetAsync(Guid id, CancellationToken ct)
    {
        using var response = await client.GetAsync($"internal/trips/{id}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<MatchingTrip>(ct);
    }

    public async Task InviteAsync(Guid tripId, Guid driverId, CancellationToken ct)
    {
        using var response = await client.PostAsJsonAsync($"internal/trips/{tripId}/invitations", new { driverId }, ct);
        response.EnsureSuccessStatusCode();
    }
}
public sealed record MatchingTrip(Guid Id, Guid RiderId);
