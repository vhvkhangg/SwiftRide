using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using MongoDB.Driver;
using SwiftRide.MatchingService.Api;
using SwiftRide.MatchingService.Application.Interfaces;
using SwiftRide.MatchingService.Application.Services;
using SwiftRide.MatchingService.Domain.Interfaces;
using SwiftRide.MatchingService.Infrastructure.Repositories;
using SwiftRide.MatchingService.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

var mongoConnectionString = builder.Configuration.GetConnectionString("MatchDb")
    ?? throw new InvalidOperationException("Connection string 'MatchDb' is required.");

builder.Services.AddSingleton<IMongoClient>(_ => new MongoClient(mongoConnectionString));
builder.Services.AddSingleton<IDriverRepository, MongoDriverRepository>();
builder.Services.AddScoped<IPricingService, PricingService>();
builder.Services.AddScoped<IMatchingService, MatchingService>();
builder.Services.AddScoped<IPricingStrategy, SwiftRide.MatchingService.Domain.Strategies.StandardPricingStrategy>();
builder.Services.AddScoped<IPricingStrategy, SwiftRide.MatchingService.Domain.Strategies.SurgePricingStrategy>();
builder.Services.AddScoped<IPricingStrategy, SwiftRide.MatchingService.Domain.Strategies.PromoPricingStrategy>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

builder.Services.AddAuthorization();
builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

var app = builder.Build();

app.UseExceptionHandler();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/health", () => Results.Ok(new { service = "matching-service", status = "ok" }))
    .WithSummary("Check matching service health")
    .AllowAnonymous();
    
app.MapMatchingEndpoints();
app.MapDriverEndpoints();
app.UseAuthentication();
app.UseAuthorization();

await app.Services.GetRequiredService<IDriverRepository>().EnsureIndexesAsync(CancellationToken.None);
app.Run();

public partial class Program;
