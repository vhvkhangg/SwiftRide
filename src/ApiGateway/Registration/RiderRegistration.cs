using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Mail;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SwiftRide.ApiGateway.Registration;

/// <summary>Body of <c>POST /api/auth/register</c> (Rider self sign-up from the app).</summary>
public sealed record RegisterRiderRequest(string? Email, string? Password, string? FirstName, string? LastName);

public sealed class RegistrationOptions
{
    public const string Section = "Registration";
    /// <summary>Keycloak base URL reachable from the Gateway (inside Compose: http://keycloak:8080).</summary>
    public string KeycloakUrl { get; set; } = "http://localhost:8180";
    public string Realm { get; set; } = "swiftride";
    /// <summary>Confidential client whose service account only has realm-management/manage-users.</summary>
    public string ClientId { get; set; } = "swiftride-registration";
    public string ClientSecret { get; set; } = "";
}

public enum RegistrationOutcome { Created, EmailTaken, Rejected, Unavailable }

public sealed record RegistrationResult(RegistrationOutcome Outcome, string? Message = null);

public static class RiderRegistration
{
    public const int MinPasswordLength = 12;
    public const int MaxPasswordLength = 128;
    public const int MaxNameLength = 100;

    /// <summary>Same rules as the realm password policy (length(12) and notUsername) so most errors never reach Keycloak.</summary>
    public static Dictionary<string, string[]> Validate(RegisterRiderRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        var email = request.Email?.Trim() ?? "";
        if (!IsEmail(email)) errors["email"] = ["Email không hợp lệ."];
        var password = request.Password ?? "";
        if (password.Length < MinPasswordLength || password.Length > MaxPasswordLength)
            errors["password"] = [$"Mật khẩu cần từ {MinPasswordLength} đến {MaxPasswordLength} ký tự."];
        else if (string.Equals(password, email, StringComparison.OrdinalIgnoreCase))
            errors["password"] = ["Mật khẩu không được trùng email."];
        if (!IsName(request.FirstName)) errors["firstName"] = [$"Nhập tên (tối đa {MaxNameLength} ký tự)."];
        if (!IsName(request.LastName)) errors["lastName"] = [$"Nhập họ (tối đa {MaxNameLength} ký tự)."];
        return errors;
    }

    private static bool IsName(string? value)
    {
        var name = value?.Trim() ?? "";
        return name.Length is > 0 and <= MaxNameLength && !name.Any(char.IsControl);
    }

    private static bool IsEmail(string email)
    {
        if (email.Length is 0 or > 254 || email.Any(c => char.IsWhiteSpace(c) || char.IsControl(c))) return false;
        return MailAddress.TryCreate(email, out var address) && address.Address == email && address.Host.Contains('.');
    }
}

/// <summary>
/// Creates Rider accounts through the Keycloak Admin REST API with a client-credentials token.
/// New users get the realm default role (default-roles-swiftride = Rider); the request never carries roles,
/// so the app cannot sign up a Driver or Admin.
/// </summary>
public sealed class KeycloakRegistrationClient(HttpClient http, RegistrationOptions options, ILogger<KeycloakRegistrationClient> logger)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    public async Task<RegistrationResult> CreateRiderAsync(RegisterRiderRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(options.ClientSecret))
        {
            logger.LogError("Registration:ClientSecret is not configured.");
            return new(RegistrationOutcome.Unavailable);
        }
        var baseUrl = options.KeycloakUrl.TrimEnd('/');
        var realm = Uri.EscapeDataString(options.Realm);
        try
        {
            using var tokenResponse = await http.PostAsync($"{baseUrl}/realms/{realm}/protocol/openid-connect/token",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = options.ClientId,
                    ["client_secret"] = options.ClientSecret,
                }), ct);
            if (!tokenResponse.IsSuccessStatusCode)
            {
                logger.LogError("Keycloak refused the registration client token: HTTP {Status}.", (int)tokenResponse.StatusCode);
                return new(RegistrationOutcome.Unavailable);
            }
            var token = (await tokenResponse.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("access_token").GetString();

            var email = request.Email!.Trim();
            var user = new
            {
                username = email,
                email,
                firstName = request.FirstName!.Trim(),
                lastName = request.LastName!.Trim(),
                enabled = true,
                emailVerified = false,
                credentials = new[] { new { type = "password", value = request.Password, temporary = false } },
            };
            using var create = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/admin/realms/{realm}/users")
            {
                Content = JsonContent.Create(user, options: Json),
            };
            create.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await http.SendAsync(create, ct);
            if (response.StatusCode == HttpStatusCode.Created) return new(RegistrationOutcome.Created);
            if (response.StatusCode == HttpStatusCode.Conflict) return new(RegistrationOutcome.EmailTaken, "Email này đã được đăng ký.");
            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                // Keycloak: {"errorMessage":"invalidPasswordMinLengthMessage", ...} or a user-profile validation error.
                var body = await response.Content.ReadAsStringAsync(ct);
                logger.LogInformation("Keycloak rejected a registration: {Body}", body.Length > 300 ? body[..300] : body);
                return new(RegistrationOutcome.Rejected, body.Contains("password", StringComparison.OrdinalIgnoreCase)
                    ? $"Mật khẩu chưa đạt yêu cầu: tối thiểu {RiderRegistration.MinPasswordLength} ký tự và không trùng email."
                    : "Thông tin đăng ký chưa hợp lệ.");
            }
            logger.LogError("Keycloak user creation failed: HTTP {Status}.", (int)response.StatusCode);
            return new(RegistrationOutcome.Unavailable);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException
                                           && !ct.IsCancellationRequested)
        {
            logger.LogError(exception, "Keycloak is unreachable for registration.");
            return new(RegistrationOutcome.Unavailable);
        }
    }
}

public static class RegistrationEndpoint
{
    public const string RateLimitPolicy = "register";

    public static IResult ToHttpResult(RegistrationResult result, string email) => result.Outcome switch
    {
        RegistrationOutcome.Created => Results.Created((string?)null, new { email, role = "Rider" }),
        RegistrationOutcome.EmailTaken => Results.Problem(result.Message, statusCode: StatusCodes.Status409Conflict, title: "Email đã tồn tại"),
        RegistrationOutcome.Rejected => Results.Problem(result.Message, statusCode: StatusCodes.Status400BadRequest, title: "Đăng ký không hợp lệ"),
        // Keycloak may or may not have created the user before failing; retrying returns 409 if it did.
        _ => Results.Problem("Chưa tạo được tài khoản lúc này. Thử lại sau ít phút.", statusCode: StatusCodes.Status503ServiceUnavailable, title: "Đăng ký tạm thời không khả dụng"),
    };
}
