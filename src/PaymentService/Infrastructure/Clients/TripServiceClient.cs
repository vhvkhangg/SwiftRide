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

    private sealed record TripHttpSnapshot(
        Guid Id,
        Guid RiderId,
        Guid? DriverId,
        decimal? FareEstimate,
        string Status);

    public async Task<TripSnapshot?> GetByIdAsync(
        Guid tripId,
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync(
            $"internal/trips/{tripId:D}",
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        var data = await response.Content
            .ReadFromJsonAsync<TripHttpSnapshot>(
                cancellationToken: cancellationToken);

        if (data is null ||
            !Enum.TryParse<TripStatusCode>(
                data.Status, true, out var status) ||
            !Enum.IsDefined(status))
        {
            throw new InvalidOperationException(
                "TripService trả về snapshot không hợp lệ.");
        }

        return new TripSnapshot(
            data.Id,
            data.RiderId,
            data.DriverId,
            data.FareEstimate,
            status);
    }


    public async Task MarkPaidAsync(
        Guid tripId,
        CancellationToken cancellationToken = default
    )
    {
        using var response = await _httpClient.PostAsync(
            $"internal/trips/{tripId:D}/pay",
            content: null,
            cancellationToken: cancellationToken
        );

        response.EnsureSuccessStatusCode();
    }
}