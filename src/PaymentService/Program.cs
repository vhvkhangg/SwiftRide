using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using SwiftRide.PaymentService.Infrastructure;
using SwiftRide.PaymentService.Domain.Repositories;
using SwiftRide.PaymentService.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<PaymentDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("PaymentDb")));

builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

builder.Services.AddAuthorization();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { service = "payment-service", status = "ok" }))
    .AllowAnonymous();

app.UseAuthentication();
app.UseAuthorization();

app.Run();
