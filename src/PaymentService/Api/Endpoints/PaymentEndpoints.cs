using SwiftRide.Security;
using SwiftRide.PaymentService.Application.Integrations;
using SwiftRide.PaymentService.Application.DTOs;
using SwiftRide.PaymentService.Application.Services;
using SwiftRide.PaymentService.Domain.Exceptions;

namespace SwiftRide.PaymentService.Api.Endpoints;

public static class PaymentEndpoints
{
    public static IEndpointRouteBuilder MapPaymentEndpoints(
        this IEndpointRouteBuilder endpoints, bool enableDevelopmentReconciliation = false)
    {
        var group = endpoints.MapGroup("/payments").RequireAuthorization().AddEndpointFilter<PaymentAccessFilter>();
        group.MapPost("/", CreatePaymentAsync).RequireAuthorization(p => p.RequireRole("Rider"));
        group.MapGet("/{id:guid}", GetPaymentAsync);
        group.MapGet("/trip/{tripId:guid}", GetPaymentByTripAsync);
        group.MapPost("/{id:guid}/refund", RefundPaymentAsync).RequireAuthorization(p => p.RequireRole("Admin"));
        if (enableDevelopmentReconciliation)
            group.MapPost("/{id:guid}/reconcile", ReconcilePaymentAsync).RequireAuthorization(p => p.RequireRole("Admin"));
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
        catch (ConcurrentPaymentUpdateException ex) { return Results.Conflict(new { error = ex.Message }); }
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

    private static async Task<IResult> ReconcilePaymentAsync(
        Guid id, IPaymentApplicationService service, CancellationToken ct)
    {
        try { return Results.Ok(await service.ReconcilePaymentAsync(id, ct)); }
        catch (KeyNotFoundException ex) { return Results.NotFound(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
        catch (ConcurrentPaymentUpdateException ex) { return Results.Conflict(new { error = ex.Message }); }
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

public sealed class PaymentAccessFilter(ITripServiceClient trips, IPaymentApplicationService payments) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        try
        {
            Guid tripId;
            if (context.Arguments.OfType<CreatePaymentRequest>().FirstOrDefault() is { } request) tripId = request.TripId;
            else if (Guid.TryParse(http.Request.RouteValues["tripId"]?.ToString(), out var requestedTrip)) tripId = requestedTrip;
            else if (Guid.TryParse(http.Request.RouteValues["id"]?.ToString(), out var paymentId))
            {
                var payment = await payments.GetPaymentByIdAsync(paymentId, http.RequestAborted);
                if (payment is null) return Results.NotFound();
                tripId = payment.TripId;
            }
            else return Results.BadRequest();
            var trip = await trips.GetByIdAsync(tripId, http.RequestAborted);
            if (trip is null) return Results.NotFound();
            if (!http.User.IsInRole("Admin") &&
                !(http.User.IsInRole("Rider") && trip.RiderId == http.User.UserId()))
                return Results.Forbid();
            return await next(context);
        }
        catch (HttpRequestException) { return Results.Problem(statusCode: 502, title: "Trip service unavailable."); }
        catch (OperationCanceledException) when (!http.RequestAborted.IsCancellationRequested)
        { return Results.Problem(statusCode: 504, title: "Trip service timeout."); }
        catch (UnauthorizedAccessException) { return Results.Unauthorized(); }
    }
}