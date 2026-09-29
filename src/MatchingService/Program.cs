using Microsoft.AspNetCore.Authentication.JwtBearer;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

var mongoConnectionString = builder.Configuration.GetConnectionString("MatchDb")
    ?? throw new InvalidOperationException("Connection string 'MatchDb' is required.");

builder.Services.AddSingleton<IMongoClient>(_ => new MongoClient(mongoConnectionString));

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

builder.Services.AddAuthorization();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { service = "matching-service", status = "ok" }))
    .AllowAnonymous();

app.UseAuthentication();
app.UseAuthorization();

app.Run();
