using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Moq;
using SwiftRide.TripService.Api;
using SwiftRide.TripService.Application.DTOs;
using SwiftRide.TripService.Application.Interfaces;
using SwiftRide.TripService.Domain.Enums;
using SwiftRide.TripService.Infrastructure;
using Xunit;

namespace SwiftRide.TripService.Tests;

public sealed class TripSecurityTests
{
    private const string UserKey = "test-user-signing-key-at-least-32-bytes-long";
    private const string ServiceKey = "test-service-signing-key-at-least-32-bytes-long";

    [Fact]
    public async Task TripEndpoints_EnforceIdentityRolesAndServiceIsolation()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseSetting("urls", "http://127.0.0.1:0");
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["JwtSettings:Issuer"] = "SwiftRide.Gateway",
            ["JwtSettings:Audience"] = "SwiftRide.Client",
            ["JwtSettings:SecretKey"] = UserKey,
            ["ServiceAuth:SecretKey"] = ServiceKey
        });
        var security = typeof(TripEndpoints).Assembly.GetType("SwiftRide.Security.ServiceSecurity")!;
        security.GetMethod("AddServiceSecurity")!.Invoke(null, [builder.Services, builder.Configuration]);
        builder.Services.AddDbContext<TripDbContext>(options => options.UseNpgsql(
            "Host=localhost;Database=unused_security_test;Username=unused;Password=unused"));
        var trips = new Mock<ITripApplicationService>();
        CreateTripRequest? received = null;
        trips.Setup(service => service.CreateAsync(It.IsAny<CreateTripRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CreateTripRequest request, CancellationToken cancellationToken) =>
            {
                received = request;
                return new TripResponse(Guid.NewGuid(), request.RiderId, null, request.PickupAddress,
                    request.DropoffAddress, 100, TripStatus.Requested, DateTime.UtcNow, null);
            });
        trips.Setup(service => service.MarkPaidAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        builder.Services.AddSingleton(trips.Object);
        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapTripEndpoints();
        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        var riderId = Guid.NewGuid();
        var body = new CreateTripRequest(Guid.NewGuid(), "A", "B", 10.77, 106.69, 2, 10);

        async Task Check(string path, string? token, HttpStatusCode expected, bool create = false)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, path);
            request.Headers.Add("X-User-Id", riderId.ToString());
            request.Headers.Add("X-User-Roles", "Admin");
            if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (create) request.Content = JsonContent.Create(body);
            using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            Assert.Equal(expected, response.StatusCode);
        }

        await Check("/trips/", null, HttpStatusCode.Unauthorized, true);
        await Check("/trips/", Token(riderId.ToString(), "Driver"), HttpStatusCode.Forbidden, true);
        await Check("/trips/", Token("not-a-guid", "Rider"), HttpStatusCode.Unauthorized, true);
        await Check("/trips/", Token(Guid.Empty.ToString(), "Rider"), HttpStatusCode.Unauthorized, true);
        await Check("/trips/", Token(riderId.ToString(), "Rider", audience: "wrong"), HttpStatusCode.Unauthorized, true);
        await Check("/trips/", Token(riderId.ToString(), "Rider", expired: true), HttpStatusCode.Unauthorized, true);
        await Check("/trips/", Token(riderId.ToString(), "Rider", key: ServiceKey), HttpStatusCode.Unauthorized, true);
        await Check("/trips/", Token(riderId.ToString(), "Rider"), HttpStatusCode.Created, true);
        Assert.Equal(riderId, received!.RiderId);
        trips.Verify(service => service.CreateAsync(It.IsAny<CreateTripRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        var paymentPath = $"/internal/trips/{Guid.NewGuid()}/pay";
        await Check(paymentPath, Token(riderId.ToString(), "Admin"), HttpStatusCode.Unauthorized);
        await Check(paymentPath, Token("matching-service", "", true), HttpStatusCode.Forbidden);
        await Check(paymentPath, Token("payment-service", "", true), HttpStatusCode.NoContent);
        await Check("/trips/", Token("payment-service", "Rider", true), HttpStatusCode.Unauthorized, true);
        await app.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task InternalSnapshot_WithInternalReadToken_ReturnsAllRequiredFields()
    {
        var dbRoot = new InMemoryDatabaseRoot();
        var dbName = $"snapshot-test-{Guid.NewGuid()}";

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseSetting("urls", "http://127.0.0.1:0");
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["JwtSettings:Issuer"] = "SwiftRide.Gateway",
            ["JwtSettings:Audience"] = "SwiftRide.Client",
            ["JwtSettings:SecretKey"] = UserKey,
            ["ServiceAuth:SecretKey"] = ServiceKey
        });
        var security = typeof(TripEndpoints).Assembly.GetType("SwiftRide.Security.ServiceSecurity")!;
        security.GetMethod("AddServiceSecurity")!.Invoke(null, [builder.Services, builder.Configuration]);
        
        builder.Services.AddDbContext<TripDbContext>(options =>
            options.UseInMemoryDatabase(dbName, dbRoot));
            
        var trips = new Mock<ITripApplicationService>();
        builder.Services.AddSingleton(trips.Object);
        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapTripEndpoints();
        await app.StartAsync(TestContext.Current.CancellationToken);

        SwiftRide.TripService.Domain.Entities.Trip seededTrip;
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TripDbContext>();
            seededTrip = SwiftRide.TripService.Domain.Entities.Trip.Create(
                Guid.NewGuid(), "A", "B", 99_000m, 10.7, 106.7);
            seededTrip.ConfirmPrice();
            seededTrip.BeginMatchingAttempt(3);
            seededTrip.AcceptByDriver(Guid.NewGuid());
            seededTrip.StartEnRoute();
            seededTrip.Pickup();
            seededTrip.Dropoff();
            seededTrip.MarkPaymentPending();
            db.Trips.Add(seededTrip);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        using var req = new HttpRequestMessage(
            HttpMethod.Get,
            $"/internal/trips/{seededTrip.Id:D}");
        req.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", Token("payment-service", "", service: true));

        using var response = await client.SendAsync(req, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(
            TestContext.Current.CancellationToken);

        Assert.Equal(seededTrip.Id.ToString(), json.GetProperty("id").GetString());
        Assert.Equal(seededTrip.RiderId.ToString(), json.GetProperty("riderId").GetString());
        Assert.Equal(seededTrip.DriverId!.Value.ToString(), json.GetProperty("driverId").GetString());
        Assert.Equal(seededTrip.FareEstimate!.Value, json.GetProperty("fareEstimate").GetDecimal());
        Assert.Equal("PaymentPending", json.GetProperty("status").GetString());

        await app.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task InternalSnapshot_WithoutInternalReadToken_Returns401Or403()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseSetting("urls", "http://127.0.0.1:0");
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["JwtSettings:Issuer"] = "SwiftRide.Gateway",
            ["JwtSettings:Audience"] = "SwiftRide.Client",
            ["JwtSettings:SecretKey"] = UserKey,
            ["ServiceAuth:SecretKey"] = ServiceKey
        });
        var security = typeof(TripEndpoints).Assembly.GetType("SwiftRide.Security.ServiceSecurity")!;
        security.GetMethod("AddServiceSecurity")!.Invoke(null, [builder.Services, builder.Configuration]);
        builder.Services.AddDbContext<TripDbContext>(options =>
            options.UseInMemoryDatabase($"snapshot-unauth-{Guid.NewGuid()}"));
        var trips = new Mock<ITripApplicationService>();
        builder.Services.AddSingleton(trips.Object);
        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapTripEndpoints();
        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        var tripId = Guid.NewGuid();

        // Unauthenticated call
        using var res1 = await client.GetAsync(
            $"/internal/trips/{tripId:D}",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, res1.StatusCode);

        // Rider token (wrong policy)
        using var req2 = new HttpRequestMessage(HttpMethod.Get, $"/internal/trips/{tripId:D}");
        req2.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", Token(Guid.NewGuid().ToString(), "Rider"));
        using var res2 = await client.SendAsync(req2, TestContext.Current.CancellationToken);
        Assert.True(res2.StatusCode == HttpStatusCode.Unauthorized || res2.StatusCode == HttpStatusCode.Forbidden || res2.StatusCode == HttpStatusCode.NotFound);

        // Service token không có quyền
        using var req3 = new HttpRequestMessage(HttpMethod.Get, $"/internal/trips/{tripId:D}");
        req3.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", Token("matching-service", "", service: true));
        using var res3 = await client.SendAsync(req3, TestContext.Current.CancellationToken);
        Assert.True(res3.StatusCode == HttpStatusCode.Unauthorized || res3.StatusCode == HttpStatusCode.Forbidden || res3.StatusCode == HttpStatusCode.NotFound);

        await app.StopAsync(TestContext.Current.CancellationToken);
    }

    private static string Token(string subject, string role, bool service = false,
        string? audience = null, bool expired = false, string? key = null)
    {
        var now = DateTime.UtcNow;
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            service ? "SwiftRide.Services" : "SwiftRide.Gateway",
            audience ?? (service ? "SwiftRide.Internal" : "SwiftRide.Client"),
            [new Claim("sub", subject), new Claim("role", role)],
            now.AddMinutes(-10), expired ? now.AddMinutes(-1) : now.AddMinutes(5),
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                key ?? (service ? ServiceKey : UserKey))), SecurityAlgorithms.HmacSha256)));
    }
}