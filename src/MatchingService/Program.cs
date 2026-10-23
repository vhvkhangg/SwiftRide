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
using SwiftRide.MatchingService.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSwiftRideMessaging(builder.Configuration, "matching", consumers =>
{
    // Gắn đúng queue đã được Messaging.Setup tạo; retry chung vẫn áp dụng.
    consumers.AddConsumer<TripRequestedConsumer>()
        .Endpoint(endpoint => endpoint.Name = MessagingEndpoints.MatchingTripRequested);
});

var mongoConnectionString = builder.Configuration.GetConnectionString("MatchDb")
    ?? throw new InvalidOperationException("Chuỗi kết nối 'MatchDb' là bắt buộc.");

builder.Services.AddSingleton<IMongoClient>(_ => new MongoClient(mongoConnectionString));
builder.Services.AddSingleton<IDriverRepository, MongoDriverRepository>();
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
app.Run();

public partial class Program;
