using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Moq;
using SwiftRide.MatchingService.Api;
using SwiftRide.MatchingService.Application.DTOs;
using SwiftRide.MatchingService.Application.Interfaces;
using SwiftRide.MatchingService.Domain.Interfaces;
using SwiftRide.Security;

namespace SwiftRide.MatchingService.Tests;

public sealed class MatchingSecurityTests
{
    private const string UserKey = "matching-test-user-signing-key-at-least-32-bytes";
    private const string ServiceKey = "matching-test-service-signing-key-at-least-32-bytes";

    [Fact]
    public async Task LocationAndQuote_EnforceIdentityRolesAndServerPricingInputs()
    {
        var ct = TestContext.Current.CancellationToken;
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseSetting("urls", "http://127.0.0.1:0");
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["JwtSettings:Issuer"] = "SwiftRide.Gateway",
            ["JwtSettings:Audience"] = "SwiftRide.Client",
            ["JwtSettings:SecretKey"] = UserKey,
            ["ServiceAuth:SecretKey"] = ServiceKey
        });
        builder.Services.AddServiceSecurity(builder.Configuration);
        var repository = new Mock<IDriverRepository>();
        var pricing = new Mock<IPricingService>();
        QuoteRequest? received = null;
        pricing.Setup(item => item.CalculateAsync(It.IsAny<QuoteRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((QuoteRequest request, CancellationToken _) =>
            {
                received = request;
                return new QuoteResponse(0, 60, 20, 0, 0, 0, 8, 88);
            });
        builder.Services.AddSingleton(repository.Object);
        builder.Services.AddSingleton(pricing.Object);
        builder.Services.AddSingleton(Mock.Of<IMatchingService>());
        builder.Services.AddSingleton(new TripAccessClient(new HttpClient { BaseAddress = new Uri("http://127.0.0.1:1/") }));
        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapDriverEndpoints();
        app.MapMatchingEndpoints();
        await app.StartAsync(ct);
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        var driverId = Guid.NewGuid();
        var payload = new { driverId = Guid.NewGuid(), latitude = 10.77, longitude = 106.7, isAvailable = true };
        using (var response = await client.PostAsJsonAsync("/api/drivers/location", payload, ct))
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(driverId.ToString("D"), "Rider"));
        using (var response = await client.PostAsJsonAsync("/api/drivers/location", payload, ct))
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(driverId.ToString("D"), "Driver"));
        using (var response = await client.PostAsJsonAsync("/api/drivers/location", payload, ct))
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using (var response = await client.PostAsJsonAsync("/api/drivers/location", new { latitude = 91, longitude = 106, isAvailable = true }, ct))
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        repository.Verify(item => item.UpsertLocationAsync(driverId.ToString("D"),
            It.IsAny<MongoDB.Driver.GeoJsonObjectModel.GeoJsonPoint<MongoDB.Driver.GeoJsonObjectModel.GeoJson2DGeographicCoordinates>>(),
            true, It.IsAny<CancellationToken>()), Times.Once);
        repository.VerifyNoOtherCalls();

        var quote = new { distance = 5, estimatedTime = 10, surgeMultiplier = 100, costPerKm = 0, fareEstimate = 1 };
        using (var response = await client.PostAsJsonAsync("/quote", quote, ct))
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(Guid.NewGuid().ToString("D"), "Rider"));
        using (var response = await client.PostAsJsonAsync("/quote", quote, ct))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(88, (await response.Content.ReadFromJsonAsync<QuoteResponse>(ct))!.Total);
        }
        Assert.Equal(new QuoteRequest(5, 10, null, null), received);
        using (var response = await client.PostAsJsonAsync("/internal/quote", quote, ct))
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token("matching-service", "", true));
        using (var response = await client.PostAsJsonAsync("/internal/quote", quote, ct))
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token("trip-service", "", true));
        using (var response = await client.PostAsJsonAsync("/internal/quote", quote, ct))
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await app.StopAsync(ct);
    }

    private static string Token(string subject, string role, bool service = false) =>
        new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer: service ? "SwiftRide.Services" : "SwiftRide.Gateway",
            audience: service ? "SwiftRide.Internal" : "SwiftRide.Client",
            claims: [new Claim("sub", subject), new Claim("role", role)],
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(service ? ServiceKey : UserKey)),
                SecurityAlgorithms.HmacSha256)));
}
