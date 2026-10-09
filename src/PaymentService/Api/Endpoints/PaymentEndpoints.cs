using SwiftRide.PaymentService.Application.DTOs;
using SwiftRide.PaymentService.Application.Services;

namespace SwiftRide.PaymentService.Api.Endpoints;

public static class PaymentEndpoints
{
    public static IEndpointRouteBuilder MapPaymentEndpoints(
        this IEndpointRouteBuilder endpoints, bool enableDevelopmentReconciliation = false)
    {
        var group = endpoints.MapGroup("/payments");
        group.MapPost("/", CreatePaymentAsync);
        group.MapGet("/{id:guid}", GetPaymentAsync);
        group.MapPost("/{id:guid}/refund", RefundPaymentAsync);
        if (enableDevelopmentReconciliation)
            group.MapPost("/{id:guid}/reconcile", ReconcilePaymentAsync);
        return endpoints;
    }

    private static async Task<IResult> CreatePaymentAsync(
        CreatePaymentRequest request, IPaymentApplicationService service, CancellationToken ct)
    {
        try
        {
            var result = await service.CreatePaymentAsync(request, ct);
            return Results.Created($"/payments/{result.Id}", result);
        }
        catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
        catch (KeyNotFoundException ex) { return Results.NotFound(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
        catch (HttpRequestException)
        {
            return Results.Problem(detail: "Lỗi giao tiếp TripService. Retry với cùng IdempotencyKey.",
                statusCode: StatusCodes.Status502BadGateway);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return Results.Problem(detail: "TripService timeout. Retry với cùng IdempotencyKey.",
                statusCode: StatusCodes.Status504GatewayTimeout);
        }
    }

    private static async Task<IResult> GetPaymentAsync(
        Guid id, IPaymentApplicationService service, CancellationToken ct)
    {
        var payment = await service.GetPaymentByIdAsync(id, ct);
        return payment is null ? Results.NotFound() : Results.Ok(payment);
    }

    private static async Task<IResult> RefundPaymentAsync(
        Guid id, IPaymentApplicationService service, CancellationToken ct)
    {
        try { return Results.Ok(await service.RefundPaymentAsync(id, ct)); }
        catch (KeyNotFoundException ex) { return Results.NotFound(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
    }

    private static async Task<IResult> ReconcilePaymentAsync(
        Guid id, IPaymentApplicationService service, CancellationToken ct)
    {
        try { return Results.Ok(await service.ReconcilePaymentAsync(id, ct)); }
        catch (KeyNotFoundException ex) { return Results.NotFound(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
        catch (HttpRequestException)
        {
            return Results.Problem(detail: "Lỗi giao tiếp TripService.",
                statusCode: StatusCodes.Status502BadGateway);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return Results.Problem(detail: "TripService timeout.",
                statusCode: StatusCodes.Status504GatewayTimeout);
        }
    }
}
