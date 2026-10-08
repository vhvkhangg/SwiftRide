using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SwiftRide.TripService.Application.DTOs;
using SwiftRide.TripService.Application.Interfaces;

namespace SwiftRide.TripService.Api;

public static class TripEndpoints
{
    public static void MapTripEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/trips").WithTags("Trips");

        // API Đặt xe (POST /trips)
        group.MapPost("/", async (CreateTripRequest request, ITripApplicationService service) =>
        {
            var response = await service.CreateAsync(request);
            return Results.Created($"/trips/{response.Id}", response);
        });

        // API Lấy thông tin chuyến đi
        group.MapGet("/{id:guid}", async (Guid id, ITripApplicationService service, CancellationToken ct) =>
        {
            try
            {
                var response = await service.GetByIdAsync(id, ct);
                return Results.Ok(response);
            }
            catch (Exception ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        });

        // API Tài xế nhận chuyến
        group.MapPost("/{id:guid}/driver-accept", async (Guid id, DriverAcceptRequest request, ITripApplicationService service) =>
        {
            await service.DriverAcceptAsync(id, request);
            return Results.NoContent();
        });

        // API Tài xế thông báo đang đến điểm đón
        group.MapPost("/{id:guid}/enroute", async (Guid id, ITripApplicationService service) =>
        {
            await service.StartEnRouteAsync(id);
            return Results.NoContent();
        });

        // API Tài xế xác nhận đã đón khách
        group.MapPost("/{id:guid}/pickup", async (Guid id, ITripApplicationService service) =>
        {
            await service.PickupAsync(id);
            return Results.NoContent();
        });

        // API Tài xế xác nhận đã trả khách
        group.MapPost("/{id:guid}/dropoff", async (Guid id, ITripApplicationService service) =>
        {
            await service.DropoffAsync(id);
            return Results.NoContent();
        });

        // API Chuyển sang chờ thanh toán
        group.MapPost("/{id:guid}/payment-pending", async (Guid id, ITripApplicationService service) =>
        {
            await service.MarkPaymentPendingAsync(id);
            return Results.NoContent();
        });

        // API Xử lý Thanh toán thành công (Webhook từ Payment Service gọi sang)
        group.MapPost("/{id:guid}/pay", async (Guid id, ITripApplicationService service) =>
        {
            await service.MarkPaidAsync(id);
            return Results.NoContent();
        });

        // API Xử lý Chuyến đi thất bại
        group.MapPost("/{id:guid}/fail", async (Guid id, ITripApplicationService service) =>
        {
            await service.MarkFailedAsync(id);
            return Results.NoContent();
        });

        // API Hủy chuyến (Rider tự hủy)
        group.MapPost("/{id:guid}/cancel", async (Guid id, ITripApplicationService service) =>
        {
            await service.CancelAsync(id);
            return Results.NoContent();
        });
    }
}
