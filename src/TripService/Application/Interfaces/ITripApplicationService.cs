using System;
using System.Threading;
using System.Threading.Tasks;
using SwiftRide.TripService.Application.DTOs;

namespace SwiftRide.TripService.Application.Interfaces;

public interface ITripApplicationService
{
    // Tạo chuyến đi mới
    Task<TripResponse> CreateAsync(CreateTripRequest request, CancellationToken ct = default);
    
    // Lấy thông tin chuyến đi
    Task<TripResponse> GetByIdAsync(Guid tripId, CancellationToken ct = default);
    
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
}
