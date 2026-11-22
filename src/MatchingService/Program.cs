using SwiftRide.Security;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using SwiftRide.Messaging;
using MongoDB.Driver;
using SwiftRide.MatchingService.Api;
using SwiftRide.MatchingService.Application.Interfaces;
using SwiftRide.MatchingService.Application.Services;
using SwiftRide.MatchingService.Application.Consumers;
using SwiftRide.MatchingService.Domain.Interfaces;
using SwiftRide.MatchingService.Infrastructure.Repositories;
using SwiftRide.MatchingService.Infrastructure.Time;
using SwiftRide.MatchingService.Api.Middleware;
using MongoDB.Bson;

var builder = WebApplication.CreateBuilder(args);
// Tham số giá và TTL vị trí đổi được khi đang chạy: file tự nạp lại; biến môi trường vẫn ghi đè (test).
builder.Configuration
    .AddJsonFile("config/matching-settings.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();
builder.Services.Configure<PricingOptions>(builder.Configuration.GetSection("Pricing"));
builder.Services.Configure<DriverAvailabilityOptions>(builder.Configuration.GetSection("DriverAvailability"));

builder.Services.AddSwiftRideMessaging(builder.Configuration, "matching", consumers =>
{
    // Gắn đúng queue đã được Messaging.Setup tạo; retry chung vẫn áp dụng.
    consumers.AddConsumer<ReleaseDriverConsumer>()
        .Endpoint(endpoint =>
        {
            endpoint.Name = MessagingEndpoints.MatchingReleaseDriver;
            endpoint.ConfigureConsumeTopology = false;
        });
    consumers.AddConsumer<FindDriverForTripConsumer>()
        .Endpoint(endpoint =>
        {
            endpoint.Name = MessagingEndpoints.MatchingFindDriverForTrip;
            endpoint.ConfigureConsumeTopology = false;
        });
    consumers.AddConsumer<DriverReservationConfirmedConsumer>()
        .Endpoint(endpoint => endpoint.Name = MessagingEndpoints.MatchingReservationConfirmed);
    consumers.AddConsumer<DriverReservationReleasedConsumer>()
        .Endpoint(endpoint => endpoint.Name = MessagingEndpoints.MatchingReservationReleased);
});

var mongoConnectionString = builder.Configuration.GetConnectionString("MatchDb")
    ?? throw new InvalidOperationException("Chuỗi kết nối 'MatchDb' là bắt buộc.");

builder.Services.AddSingleton<IMongoClient>(_ => new MongoClient(mongoConnectionString));
builder.Services.AddSingleton<IMongoDatabase>(serviceProvider =>
    serviceProvider.GetRequiredService<IMongoClient>().GetDatabase(
        MongoUrl.Create(mongoConnectionString).DatabaseName ?? "swiftride_matching"));
builder.Services.AddHostedService<ReservationRecoveryWorker>();
builder.Services.AddHostedService<MatchingOutboxDispatcher>();
builder.Services.AddSingleton<IDriverRepository, MongoDriverRepository>();
builder.Services.AddSingleton<IMatchingOutboxRepository, MongoMatchingOutboxRepository>();
builder.Services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
builder.Services.AddScoped<IPricingService, PricingService>();
builder.Services.AddScoped<IMatchingService, MatchingService>();
builder.Services.AddScoped<IPricingStrategy, SwiftRide.MatchingService.Domain.Strategies.StandardPricingStrategy>();
builder.Services.AddScoped<IPricingStrategy, SwiftRide.MatchingService.Domain.Strategies.SurgePricingStrategy>();
builder.Services.AddScoped<IPricingStrategy, SwiftRide.MatchingService.Domain.Strategies.PromoPricingStrategy>();

builder.Services.AddServiceSecurity(builder.Configuration);


builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

builder.Services.AddTransient<InternalTripHandler>();
builder.Services.AddHttpClient<TripAccessClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:TripService:BaseUrl"]
        ?? throw new InvalidOperationException("TripService URL required."));
    client.Timeout = TimeSpan.FromSeconds(5);
}).AddHttpMessageHandler<InternalTripHandler>();

var app = builder.Build();
var settingsLog = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("MatchingSettings");
var pricingMonitor = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<PricingOptions>>();
var availabilityMonitor = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<DriverAvailabilityOptions>>();
pricingMonitor.CurrentValue.Validate();
_ = availabilityMonitor.CurrentValue.LocationTtl;
pricingMonitor.OnChange((o, _) => settingsLog.LogInformation(
    "Pricing changed at runtime: CostPerKm={Km}, CostPerMinute={Min}, VatRate={Vat}, Toll={Toll}", o.CostPerKm, o.CostPerMinute, o.VatRate, o.Toll));
availabilityMonitor.OnChange((o, _) => settingsLog.LogInformation(
    "DriverAvailability changed at runtime: LocationTtlSeconds={Ttl}", o.LocationTtlSeconds));

// Readiness kiểm tra kết nối RabbitMQ; giữ nguyên /health hiện có.
app.MapHealthChecks("/health/ready").AllowAnonymous();

app.UseExceptionHandler();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/health", () => Results.Ok(new { service = "matching-service", status = "hoạt động" }))
    .WithSummary("Kiểm tra trạng thái hoạt động của dịch vụ")
    .AllowAnonymous();

app.MapMatchingEndpoints();
app.MapDriverEndpoints();
app.UseAuthentication();
app.UseAuthorization();

await app.Services.GetRequiredService<IDriverRepository>().EnsureIndexesAsync(CancellationToken.None);
var database = app.Services.GetRequiredService<IMongoDatabase>();
var hello = await database.RunCommandAsync<BsonDocument>(new BsonDocument("hello", 1));
if (!hello.Contains("setName") || !hello["isWritablePrimary"].AsBoolean)
    throw new InvalidOperationException("Matching Outbox cần MongoDB replica set primary; cấu hình standalone không hỗ trợ transaction.");
await app.Services.GetRequiredService<IMatchingOutboxRepository>().EnsureIndexesAsync(CancellationToken.None);
await MongoDbSeeder.SeedAsync(database);
app.Run();

public partial class Program;
