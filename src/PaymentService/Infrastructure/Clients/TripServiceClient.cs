using System.Net;
using System.Net.Http.Json;
using SwiftRide.PaymentService.Application.Integrations;

namespace SwiftRide.PaymentService.Infrastructure.Clients;

public sealed class TripServiceClient : ITripServiceClient
{
    private readonly HttpClient _httpClient;

    public TripServiceClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<TripSnapshot?> GetByIdAsync(
        Guid tripId,
        CancellationToken cancellationToken = default
    )
    {
        using var response = await _httpClient.GetAsync(
            $"trips/{tripId:D}",
            cancellationToken
        );

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<TripSnapshot>(cancellationToken);
    }

    public async Task MarkPaidAsync(
        Guid tripId,
        CancellationToken cancellationToken = default
    )
    {
        using var response = await _httpClient.PostAsync(
            $"trips/{tripId:D}/pay",
            content: null,
            cancellationToken: cancellationToken
        );

        response.EnsureSuccessStatusCode();
    }
}