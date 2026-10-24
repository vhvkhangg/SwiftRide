using System;
using System.Threading;
using System.Threading.Tasks;
using SwiftRide.TripService.Application.DTOs;
using SwiftRide.Contracts.Matching;
using SwiftRide.Contracts.Payments;

namespace SwiftRide.TripService.Application.Interfaces;

public interface ITripApplicationService
{
    // Tạo chuyến đi mới
    Task<TripResponse> CreateAsync(CreateTripRequest request, CancellationToken ct = default);
    
    // Lấy thông tin chuyến đi
    Task<TripResponse> GetByIdAsync(Guid tripId, CancellationToken ct = default);

    // Rider xác nhận báo giá
    Task ConfirmPriceAsync(Guid tripId, CancellationToken ct = default);
    Task HandleDriverFoundAsync(DriverFoundForTripV1 result, CancellationToken ct = default);
    Task HandleNoDriverFoundAsync(NoDriverFoundForTripV1 result, CancellationToken ct = default);
    Task DriverRejectAsync(Guid tripId, Guid driverId, CancellationToken ct = default);
    Task HandleInvitationTimeoutAsync(Guid tripId, Guid driverId, int attempt, CancellationToken ct = default);
    
    // Tài xế nhận chuyến
    Task DriverAcceptAsync(Guid tripId, DriverAcceptRequest request, CancellationToken ct = default);
    
    // Các thao tác trạng thái khác 
    Task StartEnRouteAsync(Guid tripId, CancellationToken ct = default);
    Task PickupAsync(Guid tripId, CancellationToken ct = default);
    Task DropoffAsync(Guid tripId, CancellationToken ct = default);
    Task CancelAsync(Guid tripId, CancellationToken ct = default);
    
    // Xử lý các thao tác liên quan đến thanh toán
    Task MarkPaymentPendingAsync(Guid tripId, CancellationToken ct = default);
    Task MarkPaidAsync(Guid tripId, CancellationToken ct = default);
    Task MarkFailedAsync(Guid tripId, CancellationToken ct = default);
    Task HandlePaymentSucceededAsync(PaymentSucceededV1 result, CancellationToken ct = default);
    Task HandlePaymentFailedAsync(PaymentFailedV1 result, CancellationToken ct = default);
}
