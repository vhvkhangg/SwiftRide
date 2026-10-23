using Microsoft.EntityFrameworkCore;
using SwiftRide.Security;
using SwiftRide.TripService.Application.DTOs;
using SwiftRide.TripService.Application.Interfaces;
using SwiftRide.TripService.Domain.Enums;
using SwiftRide.TripService.Infrastructure;

namespace SwiftRide.TripService.Api;

public static class TripEndpoints
{
    public static void MapTripEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/trips").RequireAuthorization().AddEndpointFilter<TripAccessFilter>();
        group.MapPost("/", async (CreateTripRequest request, HttpContext context, ITripApplicationService service, CancellationToken ct) =>
        {
            var result = await service.CreateAsync(request with { RiderId = context.User.UserId() }, ct);
            return Results.Created($"/api/trips/{result.Id}", result);
        }).RequireAuthorization(p => p.RequireRole("Rider"));
        group.MapGet("/invitations", async (HttpContext context, TripDbContext db, CancellationToken ct) =>
        {
            var driverId = context.User.UserId();
            var now = DateTime.UtcNow;
            var invitations = await db.DriverInvitations.AsNoTracking()
                .Where(x => x.DriverId == driverId && x.ExpiresAtUtc > now &&
                    db.Trips.Any(t => t.Id == x.TripId && t.DriverId == null && t.Status == TripStatus.Requested))
                .Select(x => new { x.TripId, x.ExpiresAtUtc }).ToListAsync(ct);
            return Results.Ok(invitations);
        }).RequireAuthorization(p => p.RequireRole("Driver"));
        group.MapGet("/{id:guid}", async (Guid id, ITripApplicationService service, CancellationToken ct) =>
            Results.Ok(await service.GetByIdAsync(id, ct)));
        group.MapPost("/{id:guid}/driver-accept", async (Guid id, HttpContext context, ITripApplicationService service, CancellationToken ct) =>
        {
            await service.DriverAcceptAsync(id, new DriverAcceptRequest(context.User.UserId()), ct);
            return Results.NoContent();
        }).RequireAuthorization(p => p.RequireRole("Driver"));
        group.MapPost("/{id:guid}/enroute", async (Guid id, ITripApplicationService service, CancellationToken ct) =>
        { await service.StartEnRouteAsync(id, ct); return Results.NoContent(); }).RequireAuthorization(p => p.RequireRole("Driver"));
        group.MapPost("/{id:guid}/pickup", async (Guid id, ITripApplicationService service, CancellationToken ct) =>
        { await service.PickupAsync(id, ct); return Results.NoContent(); }).RequireAuthorization(p => p.RequireRole("Driver"));
        group.MapPost("/{id:guid}/dropoff", async (Guid id, ITripApplicationService service, CancellationToken ct) =>
        { await service.DropoffAsync(id, ct); return Results.NoContent(); }).RequireAuthorization(p => p.RequireRole("Driver"));
        group.MapPost("/{id:guid}/cancel", async (Guid id, ITripApplicationService service, CancellationToken ct) =>
        { await service.CancelAsync(id, ct); return Results.NoContent(); }).RequireAuthorization(p => p.RequireRole("Rider"));

        // Public callers, including Riders/Drivers, can never invoke system transitions.
        app.MapPost("/trips/{id:guid}/pay", async (Guid id, ITripApplicationService service, CancellationToken ct) =>
        { await service.MarkPaidAsync(id, ct); return Results.NoContent(); }).RequireAuthorization("Payments");
        app.MapPost("/trips/{id:guid}/payment-pending", async (Guid id, ITripApplicationService service, CancellationToken ct) =>
        { await service.MarkPaymentPendingAsync(id, ct); return Results.NoContent(); }).RequireAuthorization("Payments");
        app.MapPost("/trips/{id:guid}/fail", async (Guid id, ITripApplicationService service, CancellationToken ct) =>
        { await service.MarkFailedAsync(id, ct); return Results.NoContent(); }).RequireAuthorization("Payments");

        app.MapGet("/internal/trips/{id:guid}", async (Guid id, TripDbContext db, CancellationToken ct) =>
        {
            var trip = await db.Trips.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
            return trip is null ? Results.NotFound() : Results.Ok(trip);
        }).RequireAuthorization("InternalRead");
        app.MapPost("/internal/trips/{id:guid}/pay", async (Guid id, ITripApplicationService service, CancellationToken ct) =>
        { await service.MarkPaidAsync(id, ct); return Results.NoContent(); }).RequireAuthorization("Payments");
        app.MapPost("/internal/trips/{id:guid}/invitations", async (Guid id, InvitationRequest request, TripDbContext db, CancellationToken ct) =>
        {
            if (request.DriverId == Guid.Empty) return Results.BadRequest();
            var trip = await db.Trips.SingleOrDefaultAsync(x => x.Id == id, ct);
            if (trip is null) return Results.NotFound();
            if (trip.Status != TripStatus.Requested || trip.DriverId is not null) return Results.Conflict();
            var invite = await db.DriverInvitations.SingleOrDefaultAsync(x => x.TripId == id && x.DriverId == request.DriverId, ct);
            if (invite is null)
                db.DriverInvitations.Add(new() { TripId = id, DriverId = request.DriverId, ExpiresAtUtc = DateTime.UtcNow.AddMinutes(2) });
            else invite.ExpiresAtUtc = DateTime.UtcNow.AddMinutes(2);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).RequireAuthorization("Matching");
    }
}

public sealed record InvitationRequest(Guid DriverId);

public sealed class TripAccessFilter(TripDbContext db) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            var http = context.HttpContext;
            if (http.Request.RouteValues["id"] is object raw && Guid.TryParse(raw.ToString(), out var id))
            {
                var trip = await db.Trips.SingleOrDefaultAsync(x => x.Id == id, http.RequestAborted);
                if (trip is null) return Results.NotFound();
                var userId = http.User.UserId();
                if (http.Request.Path.Value!.EndsWith("/driver-accept", StringComparison.OrdinalIgnoreCase))
                {
                    if (trip.Status != TripStatus.Requested || trip.DriverId is not null) return Results.Conflict();
                    if (!await db.DriverInvitations.AnyAsync(x => x.TripId == id && x.DriverId == userId &&
                        x.ExpiresAtUtc > DateTime.UtcNow, http.RequestAborted)) return Results.Forbid();
                }
                else if (!(http.User.IsInRole("Rider") && trip.RiderId == userId) &&
                         !(http.User.IsInRole("Driver") && trip.DriverId == userId))
                    return Results.Forbid();
                // Role policies on mutations distinguish Rider cancellation from Driver updates.
            }
            return await next(context);
        }
        catch (DbUpdateConcurrencyException) { return Results.Conflict(new { error = "Trip changed; reload before retrying." }); }
        catch (KeyNotFoundException) { return Results.NotFound(); }
        catch (InvalidOperationException) { return Results.Conflict(new { error = "Transition not allowed." }); }
        catch (UnauthorizedAccessException) { return Results.Unauthorized(); }
    }
}
