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

app.MapGet(
    "/health",
    async (
        PaymentDbContext dbContext,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var canConnect =
                await dbContext.Database.CanConnectAsync(
                    cancellationToken);

            if (!canConnect)
            {
                return Results.Json(
                    new
                    {
                        service = "payment-service",
                        status = "unhealthy",
                        database = "unreachable"
                    },
                    statusCode:
                        StatusCodes.Status503ServiceUnavailable);
            }

            return Results.Ok(new
            {
                service = "payment-service",
                status = "healthy",
                database = "connected"
            });
        }
        catch
        {
            return Results.Json(
                new
                {
                    service = "payment-service",
                    status = "unhealthy",
                    database = "unreachable"
                },
                statusCode:
                    StatusCodes.Status503ServiceUnavailable);
        }
    })
    .AllowAnonymous();

app.UseAuthentication();
app.UseAuthorization();

app.Run();
