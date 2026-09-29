using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using SwiftRide.TripService.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<TripDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("TripDb")));

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

builder.Services.AddAuthorization();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { service = "trip-service", status = "ok" }))
    .AllowAnonymous();

app.UseAuthentication();
app.UseAuthorization();

app.Run();
