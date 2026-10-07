using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using SwiftRide.TripService.Api;
using SwiftRide.TripService.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<TripDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("TripDb")));

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

builder.Services.AddAuthorization();

// --- Đăng ký Dependency Injection (DI) ---
builder.Services.AddScoped<SwiftRide.TripService.Domain.Interfaces.ITripReader, SwiftRide.TripService.Infrastructure.Repositories.TripRepository>();
builder.Services.AddScoped<SwiftRide.TripService.Domain.Interfaces.ITripWriter, SwiftRide.TripService.Infrastructure.Repositories.TripRepository>();
builder.Services.AddScoped<SwiftRide.TripService.Application.Interfaces.ITripApplicationService, SwiftRide.TripService.Application.Services.TripApplicationService>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { service = "trip-service", status = "ok" }))
    .AllowAnonymous();

// --- Đăng ký các Router API của TripService ---
app.MapTripEndpoints();

app.UseAuthentication();
app.UseAuthorization();

app.Run();
