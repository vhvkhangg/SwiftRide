using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using SwiftRide.ApiGateway.Registration;
using Xunit;

namespace SwiftRide.ApiGateway.Tests;

public sealed class RiderRegistrationTests
{
    private static RegisterRiderRequest Valid() => new("new.rider@example.com", "Mat-khau-du-dai-1", "An", "Nguyễn");

    [Fact]
    public void Validate_AcceptsAWellFormedRequest() => Assert.Empty(RiderRegistration.Validate(Valid()));

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("a b@example.com")]
    [InlineData("name@localhost")]
    [InlineData("")]
    public void Validate_RejectsBadEmail(string email) =>
        Assert.Contains("email", RiderRegistration.Validate(Valid() with { Email = email }).Keys);

    [Fact]
    public void Validate_FollowsRealmPasswordPolicy()
    {
        Assert.Contains("password", RiderRegistration.Validate(Valid() with { Password = "short-11ch" }).Keys);
        Assert.Contains("password", RiderRegistration.Validate(Valid() with { Password = new string('x', 129) }).Keys);
        Assert.Contains("password", RiderRegistration.Validate(Valid() with { Email = "long.enough@example.com", Password = "LONG.ENOUGH@example.com" }).Keys);
        Assert.Contains("password", RiderRegistration.Validate(Valid() with { Password = null }).Keys);
    }

    [Fact]
    public void Validate_RequiresBothNames()
    {
        var errors = RiderRegistration.Validate(Valid() with { FirstName = "  ", LastName = new string('a', 101) });
        Assert.Contains("firstName", errors.Keys);
        Assert.Contains("lastName", errors.Keys);
    }

    [Fact]
    public async Task CreateRider_UsesClientCredentialsAndNeverSendsRoles()
    {
        var handler = new FakeKeycloak(HttpStatusCode.Created);
        var result = await Client(handler).CreateRiderAsync(Valid() with { Email = "  new.rider@example.com " }, TestContext.Current.CancellationToken);

        Assert.Equal(RegistrationOutcome.Created, result.Outcome);
        Assert.Equal("http://kc:8080/realms/swiftride/protocol/openid-connect/token", handler.Requests[0].Url);
        Assert.Contains("grant_type=client_credentials", handler.Requests[0].Body);
        Assert.Contains("client_secret=dev-secret", handler.Requests[0].Body);
        Assert.Equal("http://kc:8080/admin/realms/swiftride/users", handler.Requests[1].Url);
        Assert.Equal("Bearer admin-token", handler.Requests[1].Authorization);
        using var user = JsonDocument.Parse(handler.Requests[1].Body);
        Assert.Equal("new.rider@example.com", user.RootElement.GetProperty("username").GetString());
        Assert.Equal("new.rider@example.com", user.RootElement.GetProperty("email").GetString());
        Assert.False(user.RootElement.GetProperty("credentials")[0].GetProperty("temporary").GetBoolean());
        // Role comes only from the realm default role (Rider); the request cannot ask for Driver/Admin.
        Assert.False(user.RootElement.TryGetProperty("realmRoles", out _));
        Assert.False(user.RootElement.TryGetProperty("groups", out _));
    }

    [Theory]
    [InlineData(HttpStatusCode.Conflict, RegistrationOutcome.EmailTaken, 409)]
    [InlineData(HttpStatusCode.BadRequest, RegistrationOutcome.Rejected, 400)]
    [InlineData(HttpStatusCode.Forbidden, RegistrationOutcome.Unavailable, 503)]
    [InlineData(HttpStatusCode.InternalServerError, RegistrationOutcome.Unavailable, 503)]
    public async Task CreateRider_MapsKeycloakAnswers(HttpStatusCode keycloak, RegistrationOutcome expected, int http)
    {
        var handler = new FakeKeycloak(keycloak, """{"errorMessage":"invalidPasswordMinLengthMessage"}""");
        var result = await Client(handler).CreateRiderAsync(Valid(), TestContext.Current.CancellationToken);
        Assert.Equal(expected, result.Outcome);
        Assert.Equal(http, ((IStatusCodeHttpResult)RegistrationEndpoint.ToHttpResult(result, "x@example.com")).StatusCode);
        if (expected == RegistrationOutcome.Rejected) Assert.Contains("Mật khẩu", result.Message);
    }

    [Fact]
    public async Task CreateRider_FailsClosedWhenTokenRefusedUnreachableOrNotConfigured()
    {
        var ct = TestContext.Current.CancellationToken;
        var refused = new FakeKeycloak(HttpStatusCode.Created) { TokenStatus = HttpStatusCode.Unauthorized };
        Assert.Equal(RegistrationOutcome.Unavailable, (await Client(refused).CreateRiderAsync(Valid(), ct)).Outcome);
        Assert.Single(refused.Requests);

        var down = new FakeKeycloak(HttpStatusCode.Created) { Throw = true };
        Assert.Equal(RegistrationOutcome.Unavailable, (await Client(down).CreateRiderAsync(Valid(), ct)).Outcome);

        var notConfigured = new FakeKeycloak(HttpStatusCode.Created);
        Assert.Equal(RegistrationOutcome.Unavailable, (await Client(notConfigured, secret: "").CreateRiderAsync(Valid(), ct)).Outcome);
        Assert.Empty(notConfigured.Requests);
    }

    [Fact]
    public void Created_ReturnsRiderRoleWithoutEchoingThePassword()
    {
        var result = RegistrationEndpoint.ToHttpResult(new(RegistrationOutcome.Created), "a@example.com");
        Assert.Equal(StatusCodes.Status201Created, ((IStatusCodeHttpResult)result).StatusCode);
        Assert.Equal("""{"email":"a@example.com","role":"Rider"}""", JsonSerializer.Serialize(((IValueHttpResult)result).Value));
    }

    private static KeycloakRegistrationClient Client(FakeKeycloak handler, string secret = "dev-secret") =>
        new(new HttpClient(handler), new RegistrationOptions { KeycloakUrl = "http://kc:8080/", ClientSecret = secret }, NullLogger<KeycloakRegistrationClient>.Instance);

    private sealed class FakeKeycloak(HttpStatusCode createStatus, string createBody = "") : HttpMessageHandler
    {
        public List<(string Url, string Body, string? Authorization)> Requests { get; } = [];
        public HttpStatusCode TokenStatus { get; init; } = HttpStatusCode.OK;
        public bool Throw { get; init; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (Throw) throw new HttpRequestException("connection refused");
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
            Requests.Add((request.RequestUri!.ToString(), body, request.Headers.Authorization?.ToString()));
            if (request.RequestUri!.AbsolutePath.EndsWith("/token"))
                return new HttpResponseMessage(TokenStatus) { Content = new StringContent("""{"access_token":"admin-token","expires_in":60}""") };
            return new HttpResponseMessage(createStatus) { Content = new StringContent(createBody) };
        }
    }
}
