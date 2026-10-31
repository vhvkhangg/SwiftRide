using System.Net;
using System.Text;
using System.Text.Json;
using SwiftRide.TripService.Application.DTOs;
using SwiftRide.TripService.Infrastructure.Clients;
using Xunit;

namespace SwiftRide.TripService.Tests;

public sealed class MatchingPricingClientTests
{
    [Fact]
    public async Task GetQuote_PostsInternalRequestAndReadsTotal()
    {
        HttpRequestMessage? sentRequest = null;
        using var http = new HttpClient(new StubHandler(request =>
        {
            sentRequest = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"base":0,"distance":100,"time":20,"surge":0,"toll":0,"discount":0,"tax":12,"total":132}""",
                    Encoding.UTF8,
                    "application/json")
            };
        }))
        {
            BaseAddress = new Uri("http://matching/")
        };
        var client = new MatchingPricingClient(http);

        var quote = await client.GetQuoteAsync(
            new MatchingQuoteRequest(5, 10, null, null),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpMethod.Post, sentRequest!.Method);
        Assert.Equal("/internal/quote", sentRequest.RequestUri!.AbsolutePath);
        Assert.Equal(132, quote.Total);
        using var payload = JsonDocument.Parse(await sentRequest.Content!.ReadAsStringAsync(
            TestContext.Current.CancellationToken));
        Assert.Equal(5, payload.RootElement.GetProperty("distance").GetDecimal());
        Assert.Equal(10, payload.RootElement.GetProperty("estimatedTime").GetDecimal());
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(responseFactory(request));
    }
}
