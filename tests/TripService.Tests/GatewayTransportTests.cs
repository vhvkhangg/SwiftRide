using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Moq;
using SwiftRide.Security;
using SwiftRide.TripService.Api;
using SwiftRide.TripService.Application.Interfaces;
using SwiftRide.TripService.Application.DTOs;
using SwiftRide.TripService.Domain.Entities;
using SwiftRide.TripService.Domain.Enums;
using SwiftRide.TripService.Infrastructure;
using Xunit;

namespace SwiftRide.TripService.Tests;

public sealed class GatewayTransportTests
{
    private const string UserKey = "gateway-transport-test-user-key-at-least-32-bytes";

    [Fact]
    public async Task RealGateway_ProtectsHttpAndWebSocketsAndIsolatesRecipients()
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(60));
        var ct = deadline.Token;
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseSetting("urls", "http://127.0.0.1:0");
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["JwtSettings:Issuer"] = "SwiftRide.Gateway",
            ["JwtSettings:Audience"] = "SwiftRide.Client",
            ["JwtSettings:SecretKey"] = UserKey,
            ["ServiceAuth:SecretKey"] = "gateway-transport-test-service-key-at-least-32-bytes"
        });
        builder.Services.AddServiceSecurity(builder.Configuration);
        builder.Services.AddSignalR();
        var databaseName = Guid.NewGuid().ToString();
        builder.Services.AddDbContext<TripDbContext>(options => options.UseInMemoryDatabase(databaseName));
        var trips = new Mock<ITripApplicationService>();
        builder.Services.AddSingleton(trips.Object);
        await using var downstream = builder.Build();
        downstream.UseWebSockets();
        downstream.UseAuthentication();
        downstream.UseAuthorization();
        downstream.MapTripEndpoints();
        downstream.MapHub<TripNotificationHub>("/hubs/trips", options => options.CloseOnAuthenticationExpiration = true);
        await downstream.StartAsync(ct);

        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "SwiftRide.sln"))) root = root.Parent;
        Assert.NotNull(root);
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var gatewayFolder = Path.Combine(root.FullName, "src", "ApiGateway");
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = gatewayFolder,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        start.ArgumentList.Add(Path.Combine(gatewayFolder, "bin", configuration, "net10.0", "ApiGateway.dll"));
        start.ArgumentList.Add("--urls=http://127.0.0.1:0");
        start.ArgumentList.Add("--AuthDatabase:EnsureCreated=false");
        start.ArgumentList.Add("--DevelopmentAccounts:Enabled=false");
        start.ArgumentList.Add($"--JwtSettings:SecretKey={UserKey}");
        start.ArgumentList.Add("--JwtSettings:Issuer=SwiftRide.Gateway");
        start.ArgumentList.Add("--JwtSettings:Audience=SwiftRide.Client");
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
        var port = new Uri(downstream.Urls.Single()).Port;
        foreach (var index in new[] { 0, 8, 9 })
        {
            start.Environment[$"Routes__{index}__DownstreamHostAndPorts__0__Host"] = "127.0.0.1";
            start.Environment[$"Routes__{index}__DownstreamHostAndPorts__0__Port"] = port.ToString();
        }
        var listening = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var gateway = new Process { StartInfo = start, EnableRaisingEvents = true };
        gateway.OutputDataReceived += (_, output) =>
        {
            const string marker = "Now listening on: ";
            if (output.Data is { } line && line.Contains(marker))
                listening.TrySetResult(line[(line.IndexOf(marker, StringComparison.Ordinal) + marker.Length)..].Trim());
        };
        gateway.ErrorDataReceived += (_, _) => { };
        gateway.Exited += (_, _) => listening.TrySetException(new InvalidOperationException("Gateway exited before readiness."));
        Assert.True(gateway.Start());
        gateway.BeginOutputReadLine();
        gateway.BeginErrorReadLine();
        try
        {
            var address = await listening.Task.WaitAsync(ct);
            using var client = new HttpClient { BaseAddress = new Uri(address) };
            var rider = Guid.NewGuid();
            var driver = Guid.NewGuid();
            var other = Guid.NewGuid();
            var riderToken = Token(rider.ToString(), "Rider");
            var ownedTrip = Trip.Create(rider, "A", "B", 100, 10.77, 106.69);
            ownedTrip.ConfirmPrice();
            ownedTrip.BeginMatchingAttempt(3);
            ownedTrip.AcceptByDriver(driver);
            var invitedTrip = Trip.Create(rider, "C", "D", 100, 10.77, 106.69);
            invitedTrip.ConfirmPrice();
            invitedTrip.BeginMatchingAttempt(3);
            using (var scope = downstream.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<TripDbContext>();
                db.Trips.AddRange(ownedTrip, invitedTrip);
                db.DriverInvitations.Add(new DriverInvitation
                {
                    TripId = invitedTrip.Id, DriverId = driver, Attempt = 1,
                    ExpiresAtUtc = DateTime.UtcNow.AddMinutes(1)
                });
                await db.SaveChangesAsync(ct);
            }
            trips.Setup(service => service.GetByIdAsync(ownedTrip.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TripResponse(ownedTrip.Id, rider, driver, "A", "B", 100,
                    TripStatus.DriverAccepted, ownedTrip.CreatedAt, null));
            async Task Check(string path, string? token, HttpStatusCode expected, bool post = false)
            {
                using var request = new HttpRequestMessage(post ? HttpMethod.Post : HttpMethod.Get, path);
                if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Headers.Add("X-User-Id", rider.ToString());
                request.Headers.Add("X-User-Roles", "Admin");
                using var response = await client.SendAsync(request, ct);
                Assert.Equal(expected, response.StatusCode);
            }
            await Check("/api/trips/driver/invitations", null, HttpStatusCode.Unauthorized);
            await Check("/api/trips/driver/invitations", riderToken, HttpStatusCode.Forbidden);
            await Check("/api/trips/driver/invitations", Token(driver.ToString(), "Driver"), HttpStatusCode.OK);
            await Check("/api/trips/driver/invitations", Token("bad-subject", "Driver"), HttpStatusCode.Unauthorized);
            await Check("/api/trips/driver/invitations", Token(driver.ToString(), "Driver", true), HttpStatusCode.Unauthorized);
            await Check($"/api/trips/{ownedTrip.Id}", riderToken, HttpStatusCode.OK);
            await Check($"/api/trips/{ownedTrip.Id}", Token(other.ToString(), "Rider"), HttpStatusCode.Forbidden);
            await Check($"/api/trips/{ownedTrip.Id}/cancel", Token(other.ToString(), "Rider"), HttpStatusCode.Forbidden, true);
            await Check($"/api/trips/{ownedTrip.Id}/enroute", riderToken, HttpStatusCode.Forbidden, true);
            await Check($"/api/trips/{ownedTrip.Id}/enroute", Token(other.ToString(), "Driver"), HttpStatusCode.Forbidden, true);
            await Check($"/api/trips/{ownedTrip.Id}/enroute", Token(driver.ToString(), "Driver"), HttpStatusCode.NoContent, true);
            await Check($"/api/trips/{invitedTrip.Id}/driver-accept", Token(other.ToString(), "Driver"), HttpStatusCode.Forbidden, true);
            foreach (var recipient in new[] { driver, other })
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, "/api/trips/driver/invitations");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token(recipient.ToString(), "Driver"));
                using var response = await client.SendAsync(request, ct);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                using var invitations = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                Assert.Equal(recipient == driver ? 1 : 0, invitations.RootElement.GetArrayLength());
            }
            await Check($"/internal/trips/{Guid.NewGuid()}", riderToken, HttpStatusCode.NotFound);
            await Check($"/api/trips/{Guid.NewGuid()}/pay", riderToken, HttpStatusCode.Unauthorized, true);
            await Check("/api/hubs/trips/negotiate?negotiateVersion=1", null, HttpStatusCode.Unauthorized, true);
            await Check($"/api/hubs/trips/negotiate?access_token={riderToken}", null, HttpStatusCode.Unauthorized, true);
            await Check("/api/hubs/trips/negotiate?negotiateVersion=1", riderToken, HttpStatusCode.OK, true);
            await Check("/api/hubs/trips/negotiate?negotiateVersion=1", Token(other.ToString(), "Admin"), HttpStatusCode.Forbidden, true);

            var socketUrl = address.Replace("http://", "ws://", StringComparison.Ordinal) + "/api/hubs/trips";
            using var anonymous = new ClientWebSocket();
            await Assert.ThrowsAsync<WebSocketException>(() => anonymous.ConnectAsync(new Uri(socketUrl), ct));
            using var expired = new ClientWebSocket();
            await Assert.ThrowsAsync<WebSocketException>(() => expired.ConnectAsync(
                new Uri(socketUrl + "?access_token=" + Token(rider.ToString(), "Rider", true)), ct));
            using var riderSocket = await Connect(socketUrl, riderToken, queryToken: true, ct);
            using var driverSocket = await Connect(socketUrl, Token(driver.ToString(), "Driver"), queryToken: false, ct);
            using var otherSocket = await Connect(socketUrl, Token(other.ToString(), "Rider"), queryToken: true, ct);
            var hub = downstream.Services.GetRequiredService<IHubContext<TripNotificationHub>>();
            foreach (var recipient in new[] { rider, driver, other })
                await hub.Clients.Group(TripNotificationGroups.ForUser(recipient)).SendAsync("tripChanged", recipient.ToString(), ct);
            foreach (var (socket, recipient) in new[] { (riderSocket, rider), (driverSocket, driver), (otherSocket, other) })
            {
                var payload = await Receive(socket, ct);
                using var json = JsonDocument.Parse(payload.TrimEnd('\u001e'));
                Assert.Equal("tripChanged", json.RootElement.GetProperty("target").GetString());
                Assert.Equal(recipient.ToString(), json.RootElement.GetProperty("arguments")[0].GetString());
            }
            var helperStart = new ProcessStartInfo("pwsh")
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            helperStart.Environment["TASK1_HELPERS"] = Path.Combine(root.FullName, "tests", "Gateway.Security", "signalr-helpers.ps1");
            helperStart.Environment["TASK1_URL"] = address;
            helperStart.Environment["TASK1_TOKEN"] = riderToken;
            helperStart.Environment["TASK1_OTHER"] = Token(other.ToString(), "Rider");
            helperStart.Environment["TASK1_TRIP"] = ownedTrip.Id.ToString();
            const string helperScript = """
                $ErrorActionPreference='Stop'
                . $env:TASK1_HELPERS
                $owner=Open-HubSocket $env:TASK1_URL $env:TASK1_TOKEN $true
                $other=Open-HubSocket $env:TASK1_URL $env:TASK1_OTHER
                try {
                    Write-Output 'READY'
                    $null=Wait-HubEvent $owner 'tripChanged' $env:TASK1_TRIP
                    Assert-NoHubInvocation $other
                    Write-Output 'PASS'
                } finally { $owner.Dispose(); $other.Dispose() }
                """;
            helperStart.ArgumentList.Add("-NoProfile");
            helperStart.ArgumentList.Add("-EncodedCommand");
            helperStart.ArgumentList.Add(Convert.ToBase64String(Encoding.Unicode.GetBytes(helperScript)));
            using var helper = Process.Start(helperStart)!;
            var helperErrors = helper.StandardError.ReadToEndAsync(ct);
            try
            {
                Assert.Equal("READY", await helper.StandardOutput.ReadLineAsync(ct));
                await hub.Clients.Group(TripNotificationGroups.ForUser(rider))
                    .SendAsync("tripChanged", new { id = ownedTrip.Id }, ct);
                var helperResult = await helper.StandardOutput.ReadLineAsync(ct);
                await helper.WaitForExitAsync(ct);
                Assert.True(helper.ExitCode == 0, await helperErrors);
                Assert.Equal("PASS", helperResult);
            }
            finally
            {
                if (!helper.HasExited) helper.Kill(entireProcessTree: true);
                await helper.WaitForExitAsync(CancellationToken.None);
            }
        }
        finally
        {
            if (!gateway.HasExited) gateway.Kill(entireProcessTree: true);
            await gateway.WaitForExitAsync(CancellationToken.None);
            await downstream.StopAsync(CancellationToken.None);
        }
    }

    private static async Task<ClientWebSocket> Connect(string url, string token, bool queryToken, CancellationToken ct)
    {
        var socket = new ClientWebSocket();
        if (queryToken) url += "?access_token=" + token;
        else socket.Options.SetRequestHeader("Authorization", "Bearer " + token);
        try
        {
            await socket.ConnectAsync(new Uri(url), ct);
            await socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes("{\"protocol\":\"json\",\"version\":1}\u001e")),
                WebSocketMessageType.Text, true, ct);
            Assert.Equal("{}\u001e", await Receive(socket, ct));
            return socket;
        }
        catch { socket.Dispose(); throw; }
    }

    private static async Task<string> Receive(ClientWebSocket socket, CancellationToken ct)
    {
        var buffer = new byte[4096];
        var text = new StringBuilder();
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
            Assert.Equal(WebSocketMessageType.Text, result.MessageType);
            text.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
        } while (!result.EndOfMessage);
        return text.ToString();
    }

    private static string Token(string subject, string role, bool expired = false) =>
        new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            "SwiftRide.Gateway", "SwiftRide.Client", [new Claim("sub", subject), new Claim("role", role)],
            DateTime.UtcNow.AddMinutes(-10), DateTime.UtcNow.AddMinutes(expired ? -1 : 5),
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(UserKey)), SecurityAlgorithms.HmacSha256)));
}
