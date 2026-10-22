using Microsoft.AspNetCore.Authentication.JwtBearer;
using SwiftRide.Messaging;
using Microsoft.EntityFrameworkCore;
using SwiftRide.PaymentService.Infrastructure.Persistence;
using SwiftRide.PaymentService.Domain.Repositories;
using SwiftRide.PaymentService.Infrastructure.Repositories;
using SwiftRide.PaymentService.Application.Services;
using SwiftRide.PaymentService.Api.Endpoints;
using SwiftRide.PaymentService.Application.Integrations;
using SwiftRide.PaymentService.Infrastructure.Clients;
using MassTransit;
using SwiftRide.PaymentService.Application.Consumers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<PaymentDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("PaymentDb")));

builder.Services.AddSwiftRideMessaging(
    builder.Configuration,
    "payment",
    registerConsumers: x =>
    {
        x.AddConsumer<ProcessPaymentConsumer>()
            .Endpoint(e =>
            {
                e.Name = MessagingEndpoints.PaymentProcessPayment;
            });

        x.AddEntityFrameworkOutbox<PaymentDbContext>(o =>
        {
            o.UsePostgres();

            o.DuplicateDetectionWindow = TimeSpan.FromHours(24);
        });

        x.AddConfigureEndpointsCallback((context, name, endpoint) =>
        {
            if (name == MessagingEndpoints.PaymentProcessPayment)
            {
                endpoint.UseEntityFrameworkOutbox<PaymentDbContext>(
                    context);
            }
        });
    });

builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();

builder.Services.AddScoped<
    IPaymentApplicationService,
    PaymentApplicationService>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

builder.Services.AddAuthorization();

builder.Services.AddHttpClient<
    ITripServiceClient,
    TripServiceClient
>(client =>
{
    var baseUrl = builder.Configuration["Services:TripService:BaseUrl"]
    ?? throw new InvalidOperationException("Chưa cấu hình URL của TripService.");

    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(5);
});

var app = builder.Build();

// Readiness kiểm tra kết nối RabbitMQ; giữ nguyên /health hiện có.
app.MapHealthChecks("/health/ready").AllowAnonymous();

app.MapPaymentEndpoints(app.Environment.IsDevelopment());

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
