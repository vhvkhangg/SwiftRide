using MassTransit;
using SwiftRide.Security;
using SwiftRide.Messaging;
using Microsoft.EntityFrameworkCore;
using SwiftRide.TripService.Api;
using SwiftRide.TripService.Infrastructure;
using SwiftRide.TripService.Infrastructure.Clients;
using SwiftRide.TripService.Application.Interfaces;
using SwiftRide.TripService.Application.Consumers;
using SwiftRide.TripService.Application.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<TripDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("TripDb")));

builder.Services.AddSwiftRideMessaging(builder.Configuration, "trip", consumers =>
{
    consumers.AddConsumer<DriverFoundForTripConsumer>()
        .Endpoint(endpoint => endpoint.Name = MessagingEndpoints.TripDriverFoundForTrip);
    consumers.AddConsumer<NoDriverFoundForTripConsumer>()
        .Endpoint(endpoint => endpoint.Name = MessagingEndpoints.TripNoDriverFoundForTrip);
    consumers.AddConsumer<PaymentSucceededConsumer>()
        .Endpoint(endpoint => endpoint.Name = MessagingEndpoints.TripPaymentSucceeded);
    consumers.AddConsumer<PaymentFailedConsumer>()
        .Endpoint(endpoint => endpoint.Name = MessagingEndpoints.TripPaymentFailed);
    consumers.AddEntityFrameworkOutbox<TripDbContext>(options =>
    {
        options.UsePostgres();
        options.UseBusOutbox();
        options.DuplicateDetectionWindow = TimeSpan.FromHours(24);
    });
    consumers.AddConfigureEndpointsCallback((context, name, endpoint) =>
    {
        if (name is MessagingEndpoints.TripDriverFoundForTrip or
            MessagingEndpoints.TripNoDriverFoundForTrip or
            MessagingEndpoints.TripPaymentSucceeded or
            MessagingEndpoints.TripPaymentFailed)
        {
            endpoint.UseEntityFrameworkOutbox<TripDbContext>(context);
        }
    });
});

builder.Services.AddServiceSecurity(builder.Configuration);
builder.Services.AddSignalR();
builder.Services.Configure<TripMatchingOptions>(builder.Configuration.GetSection("TripMatching"));
builder.Services.AddHttpContextAccessor();

builder.Services.AddTransient<InternalTripHandler>();
builder.Services.AddHttpClient<IMatchingPricingClient, MatchingPricingClient>(client =>
{
    var baseUrl = builder.Configuration["Services:MatchingService:BaseUrl"]
        ?? throw new InvalidOperationException("Chưa cấu hình URL của MatchingService.");
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(5);
}).AddHttpMessageHandler<InternalTripHandler>();

// --- Đăng ký Dependency Injection (DI) ---
builder.Services.AddScoped<SwiftRide.TripService.Domain.Interfaces.ITripReader, SwiftRide.TripService.Infrastructure.Repositories.TripRepository>();
builder.Services.AddScoped<SwiftRide.TripService.Domain.Interfaces.ITripWriter, SwiftRide.TripService.Infrastructure.Repositories.TripRepository>();
builder.Services.AddScoped<SwiftRide.TripService.Application.Interfaces.ITripWorkflowRepository, SwiftRide.TripService.Infrastructure.Repositories.TripWorkflowRepository>();
builder.Services.AddScoped<SwiftRide.TripService.Application.Interfaces.ITripUnitOfWork, SwiftRide.TripService.Infrastructure.TripUnitOfWork>();
builder.Services.AddScoped<SwiftRide.TripService.Application.Interfaces.ITripNotifier, SwiftRide.TripService.Infrastructure.SignalRTripNotifier>();
builder.Services.AddScoped<SwiftRide.TripService.Application.Interfaces.ITripApplicationService, SwiftRide.TripService.Application.Services.TripApplicationService>();
builder.Services.AddHostedService<SwiftRide.TripService.Application.Services.DriverInvitationTimeoutWorker>();

var app = builder.Build();

// Readiness kiểm tra kết nối RabbitMQ; giữ nguyên /health hiện có.
app.MapHealthChecks("/health/ready").AllowAnonymous();

app.MapGet("/health", () => Results.Ok(new { service = "trip-service", status = "ok" }))
    .AllowAnonymous();

// --- Đăng ký các Router API của TripService ---
app.MapTripEndpoints();
app.MapHub<SwiftRide.TripService.Api.TripNotificationHub>("/hubs/trips",
    options => options.CloseOnAuthenticationExpiration = true);

app.UseAuthentication();
app.UseAuthorization();

if (builder.Configuration.GetValue<bool>("Database:Migrate"))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<TripDbContext>().Database.MigrateAsync();
}
app.Run();
