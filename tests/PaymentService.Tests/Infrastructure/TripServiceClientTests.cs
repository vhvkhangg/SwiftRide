
using System.Net;
using System.Net.Http.Json;
using SwiftRide.PaymentService.Application.Integrations;
using SwiftRide.PaymentService.Infrastructure.Clients;
using Xunit;

namespace SwiftRide.PaymentService.Tests.Infrastructure;

public sealed class TripServiceClientTests
{
    private sealed class StubHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(responseFactory(request));
        }
    }

    private static TripServiceClient CreateClient(
        Func<HttpRequestMessage, HttpResponseMessage> response)
    {
        var http = new HttpClient(new StubHandler(response))
        {
            BaseAddress = new Uri("http://localhost:5101/")
        };

        return new TripServiceClient(http);
    }

    [Theory]
    [InlineData("PriceConfirmed", TripStatusCode.PriceConfirmed)]
    [InlineData("PaymentPending", TripStatusCode.PaymentPending)]
    [InlineData("Paid", TripStatusCode.Paid)]
    public async Task GetById_ShouldParseTripStatus(
        string status,
        TripStatusCode expected)
    {
        var tripId = Guid.NewGuid();
        var riderId = Guid.NewGuid();
        var driverId = Guid.NewGuid();

        var client = CreateClient(request =>
        {
            Assert.Equal(
                $"/internal/trips/{tripId:D}",
                request.RequestUri?.AbsolutePath);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    id = tripId,
                    riderId,
                    driverId,
                    fareEstimate = 100_000m,
                    status
                })
            };
        });

        var result = await client.GetByIdAsync(tripId, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(tripId, result.Id);
        Assert.Equal(riderId, result.RiderId);
        Assert.Equal(driverId, result.DriverId);
        Assert.Equal(100_000m, result.FareEstimate);
        Assert.Equal(expected, result.Status);
    }

    [Fact]
    public async Task GetById_UnknownStatus_ShouldReject()
    {
        var client = CreateClient(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    id = Guid.NewGuid(),
                    riderId = Guid.NewGuid(),
                    status = "UnknownStatus"
                })
            });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.GetByIdAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetById_NotFound_ShouldReturnNull()
    {
        var client = CreateClient(_ =>
            new HttpResponseMessage(HttpStatusCode.NotFound));

        var result = await client.GetByIdAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.Null(result);
    }
}
