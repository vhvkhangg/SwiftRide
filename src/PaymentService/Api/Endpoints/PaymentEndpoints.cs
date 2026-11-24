using SwiftRide.PaymentService.Api.Filters;
using SwiftRide.Security;
using SwiftRide.PaymentService.Application.Integrations;
using SwiftRide.PaymentService.Application.DTOs;
using SwiftRide.PaymentService.Application.Services;
using SwiftRide.PaymentService.Domain.Exceptions;

namespace SwiftRide.PaymentService.Api.Endpoints;

public static class PaymentEndpoints
{

    public static IEndpointRouteBuilder MapPaymentEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/payments")
            .RequireAuthorization()
            .AddEndpointFilter<PaymentAccessFilter>();

        group.MapGet("/{id:guid}", GetPaymentAsync);

        group.MapGet("/trip/{tripId:guid}", GetPaymentByTripAsync);

        group.MapPost("/{id:guid}/refund", RefundPaymentAsync)
            .RequireAuthorization(p => p.RequireRole("Admin"));

        return endpoints;
    }

    private static async Task<IResult> GetPaymentAsync(
        Guid id, IPaymentApplicationService service, CancellationToken ct)
    {
        var payment = await service.GetPaymentByIdAsync(id, ct);
        return payment is null ? Results.NotFound() : Results.Ok(payment);
    }

    private static async Task<IResult> GetPaymentByTripAsync(
        Guid tripId,
        IPaymentApplicationService service,
        CancellationToken ct)
    {
        try
        {
            var payment = await service.GetPaymentByTripIdAsync(tripId, ct);
            return payment is null ? Results.NotFound() : Results.Ok(payment);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> RefundPaymentAsync(
        Guid id, IPaymentApplicationService service, CancellationToken ct)
    {
        try { return Results.Ok(await service.RefundPaymentAsync(id, ct)); }
        catch (KeyNotFoundException ex) { return Results.NotFound(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
        catch (ConcurrentPaymentUpdateException ex) { return Results.Conflict(new { error = ex.Message }); }
    }
}
