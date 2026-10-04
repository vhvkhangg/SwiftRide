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

// --- Đăng ký Dependency Injection (DI) ---
// Hệ thống sẽ tự động dùng TripRepository mỗi khi có class nào yêu cầu ITripReader hoặc ITripWriter
builder.Services.AddScoped<SwiftRide.TripService.Domain.Interfaces.ITripReader, SwiftRide.TripService.Infrastructure.Repositories.TripRepository>();
builder.Services.AddScoped<SwiftRide.TripService.Domain.Interfaces.ITripWriter, SwiftRide.TripService.Infrastructure.Repositories.TripRepository>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { service = "trip-service", status = "ok" }))
    .AllowAnonymous();

app.UseAuthentication();
app.UseAuthorization();

app.Run();
