using SwiftRide.Security;
using SwiftRide.PaymentService.Application.Integrations;
using SwiftRide.PaymentService.Application.Services;

namespace SwiftRide.PaymentService.Api.Filters;

public sealed class PaymentAccessFilter(ITripServiceClient trips, IPaymentApplicationService payments) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        try
        {
            Guid tripId;
            
            if (Guid.TryParse(
                http.Request.RouteValues["tripId"]?.ToString(),
                out var requestedTrip
            ))
            {
                tripId = requestedTrip;
            }
            else if (Guid.TryParse(
                http.Request.RouteValues["id"]?.ToString(),
                out var paymentId
            ))
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
        catch (HttpRequestException)
        {
            return Results.Problem(statusCode: 502, title: "Trip service unavailable.");
        }
        catch (OperationCanceledException) when (!http.RequestAborted.IsCancellationRequested)
        {
            return Results.Problem(statusCode: 504, title: "Trip service timeout.");
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Unauthorized();
        }
    }
}