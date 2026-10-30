using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SwiftRide.Contracts.Matching;
using SwiftRide.Contracts.Payments;
using SwiftRide.Messaging;
using SwiftRide.TripService.Application.DTOs;
using SwiftRide.TripService.Application.Interfaces;
using SwiftRide.TripService.Domain.Entities;
using SwiftRide.TripService.Domain.Enums;
using SwiftRide.TripService.Domain.Interfaces;

namespace SwiftRide.TripService.Application.Services;

public sealed class TripApplicationService : ITripApplicationService
{
    private readonly ITripWriter _writer;
    private readonly ITripReader _reader;
    private readonly ITripWorkflowRepository _workflow;
    private readonly ITripUnitOfWork _unitOfWork;
    private readonly ITripNotifier _notifier;
    private readonly IMatchingPricingClient _matchingPricing;
    private readonly ITripMessageBus _messageBus;
    private readonly TripMatchingOptions _options;

    public TripApplicationService(
        ITripWriter writer,
        ITripReader reader,
        ITripWorkflowRepository workflow,
        ITripUnitOfWork unitOfWork,
        ITripNotifier notifier,
        IMatchingPricingClient matchingPricing,
        ITripMessageBus messageBus,
        IOptions<TripMatchingOptions> options)
    {
        _writer = writer;
        _reader = reader;
        _workflow = workflow;
        _unitOfWork = unitOfWork;
        _notifier = notifier;
        _matchingPricing = matchingPricing;
        _messageBus = messageBus;
        _options = options.Value;

        if (_options.MaxAttempts < 1 || _options.InvitationTimeoutSeconds < 1 ||
            !double.IsFinite(_options.SearchRadiusKm) || _options.SearchRadiusKm <= 0)
            throw new InvalidOperationException("Cấu hình TripMatching không hợp lệ.");
    }

    public async Task<TripResponse> CreateAsync(CreateTripRequest request, CancellationToken ct = default)
    {
        // 1. Kiểm tra Idempotency nếu có
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var existingTrip = await _reader.GetByIdempotencyKeyAsync(request.IdempotencyKey, ct);
            if (existingTrip is not null)
            {
                var currentPayloadHash = GeneratePayloadHash(request);
                if (existingTrip.PayloadHash == currentPayloadHash)
                {
                    // Cùng key + cùng payload -> Trả về chuyến cũ
                    return ToResponse(existingTrip);
                }
                // Cùng key + khác payload -> Từ chối (Lỗi 409 Conflict)
                throw new InvalidOperationException("IdempotencyKey đã được sử dụng với payload khác.");
            }
        }

        if (request.RiderId == Guid.Empty ||
            string.IsNullOrWhiteSpace(request.PickupAddress) ||
            string.IsNullOrWhiteSpace(request.DropoffAddress) ||
            request.PickupAddress.Length > 500 || request.DropoffAddress.Length > 500)
            throw new ArgumentException("Cần RiderId và hai địa chỉ hợp lệ, tối đa 500 ký tự.");

        if (request.Distance < 0 || request.EstimatedTime < 0 || request.Surge <= 0)
            throw new ArgumentException("Quãng đường và thời gian không được âm; hệ số surge phải lớn hơn 0.");

        if (!request.PickupLatitude.HasValue || !request.PickupLongitude.HasValue ||
            !double.IsFinite(request.PickupLatitude.Value) ||
            !double.IsFinite(request.PickupLongitude.Value) ||
            request.PickupLatitude.Value < -90 || request.PickupLatitude.Value > 90 ||
            request.PickupLongitude.Value < -180 || request.PickupLongitude.Value > 180)
            throw new ArgumentException("Cần tọa độ điểm đón hợp lệ.");

        // Lấy báo giá
        var quote = await _matchingPricing.GetQuoteAsync(
        new MatchingQuoteRequest(
            request.Distance,
            request.EstimatedTime,
            request.RideType,
            request.Surge,
            request.PromoCode),
        ct);

        // 3. Tạo chuyến mới kèm Hash
        var trip = Trip.Create(
            request.RiderId,
            request.PickupAddress,
            request.DropoffAddress,
            quote.Total,
            request.PickupLatitude,
            request.PickupLongitude,
            request.IdempotencyKey,
            GeneratePayloadHash(request)); // Băm DTO

        await _writer.AddAsync(trip, ct);
        return ToResponse(trip);
    }

    public async Task<TripResponse?> GetActiveTripAsync(Guid userId, string role, CancellationToken ct = default)
    {
        var trip = await _reader.GetActiveTripAsync(userId, role, ct);
        return trip is null ? null : ToResponse(trip);
    }

    // Hàm hỗ trợ băm Payload
    private static string GeneratePayloadHash(CreateTripRequest request)
    {
        // Loại bỏ các trường không cấu thành sự khác biệt về payload nghiệp vụ (như IdempotencyKey)
        var payloadForHash = new { request.RiderId, request.PickupAddress, request.DropoffAddress, request.Distance };
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payloadForHash));
        return Convert.ToBase64String(sha256.ComputeHash(bytes));
    }
    public async Task<TripResponse> GetByIdAsync(Guid tripId, CancellationToken ct = default)
    {
        var trip = await GetTripAsync(tripId, ct);
        return ToResponse(trip);
    }

    public async Task ConfirmPriceAsync(Guid tripId, CancellationToken ct = default)
    {
        await _unitOfWork.ExecuteAsync(async transactionCt =>
        {
            var trip = await GetTripAsync(tripId, transactionCt);
            var changed = trip.Status == TripStatus.Requested;

            trip.ConfirmPrice();
            if (trip.Status != TripStatus.PriceConfirmed)
                throw new InvalidOperationException("Chỉ có thể xác nhận giá cho chuyến đang chờ Rider.");

            if (trip.MatchingAttempt == 0)
            {
                trip.BeginMatchingAttempt(_options.MaxAttempts);
                changed = true;
            }

            if (changed)
                await _writer.SaveAsync(trip, transactionCt);

            await SendFindDriverAsync(trip, trip.MatchingAttempt, transactionCt);
        }, ct);
    }

    public async Task HandleDriverFoundAsync(DriverFoundForTripV1 result, CancellationToken ct = default)
    {
        var trip = await _reader.GetByIdAsync(result.TripId, ct);
        if (trip is null || trip.Status != TripStatus.PriceConfirmed ||
            trip.MatchingAttempt != result.Attempt)
            return;

        if (result.DriverId == Guid.Empty ||
            trip.PickupLatitude is null || trip.PickupLongitude is null)
            throw new InvalidOperationException("Kết quả Matching thiếu DriverId hoặc tọa độ điểm đón.");

        var expiresAtUtc = DateTime.UtcNow.AddSeconds(_options.InvitationTimeoutSeconds);
        var created = await _workflow.AddInvitationAsync(new DriverInvitation
        {
            TripId = trip.Id,
            DriverId = result.DriverId,
            Attempt = result.Attempt,
            ExpiresAtUtc = expiresAtUtc
        }, ct);

        if (created)
        {
            await _notifier.NotifyDriverInvitationAsync(
                result.DriverId,
                trip.Id,
                trip.PickupAddress,
                trip.DropoffAddress,
                trip.FareEstimate,
                expiresAtUtc,
                ct);
        }
    }

    public async Task HandleNoDriverFoundAsync(NoDriverFoundForTripV1 result, CancellationToken ct = default)
    {
        var trip = await _reader.GetByIdAsync(result.TripId, ct);
        if (trip is null || trip.Status != TripStatus.PriceConfirmed ||
            trip.MatchingAttempt != result.Attempt)
            return;

        trip.MarkFailed();
        await SaveAndNotifyAsync(trip, ct);
    }

    public async Task DriverAcceptAsync(Guid tripId, DriverAcceptRequest request, CancellationToken ct = default)
    {
        var response = await _unitOfWork.ExecuteAsync(async transactionCt =>
        {
            var trip = await GetTripAsync(tripId, transactionCt);
            var invitation = await _workflow.GetActiveInvitationAsync(
                tripId, request.DriverId, DateTime.UtcNow, transactionCt);

            if (invitation is null || invitation.Attempt != trip.MatchingAttempt)
                throw new InvalidOperationException("Tài xế không có lời mời còn hiệu lực cho chuyến này.");

            trip.AcceptByDriver(request.DriverId);
            await _writer.SaveAsync(trip, transactionCt);

            // Bắt buộc: Đánh dấu lời mời đã được giải quyết để Timeout Worker không quét lại và gây lỗi đồng thời
            await _workflow.ResolveInvitationAsync(
                tripId, request.DriverId, trip.MatchingAttempt, DateTime.UtcNow, false, transactionCt);

            return ToResponse(trip);
        }, ct);

        await _notifier.NotifyTripChangedAsync(response, ct);
    }

    public Task DriverRejectAsync(Guid tripId, Guid driverId, CancellationToken ct = default) =>
        ResolveInvitationAndContinueAsync(tripId, driverId, timeout: false, ct);

    public Task HandleInvitationTimeoutAsync(
        Guid tripId,
        Guid driverId,
        int attempt,
        CancellationToken ct = default) =>
        ResolveInvitationAndContinueAsync(tripId, driverId, attempt, timeout: true, ct);

    private async Task ResolveInvitationAndContinueAsync(
        Guid tripId,
        Guid driverId,
        bool timeout,
        CancellationToken ct) =>
        await ResolveInvitationAndContinueAsync(tripId, driverId, attempt: null, timeout, ct);

    private async Task ResolveInvitationAndContinueAsync(
        Guid tripId,
        Guid driverId,
        int? attempt,
        bool timeout,
        CancellationToken ct)
    {
        var response = await _unitOfWork.ExecuteAsync(async transactionCt =>
        {
            var trip = await GetTripAsync(tripId, transactionCt);
            if (trip.Status != TripStatus.PriceConfirmed)
                throw new InvalidOperationException("Chuyến không còn chờ phản hồi tài xế.");

            var invitation = await _workflow.GetActiveInvitationAsync(
                tripId, driverId, DateTime.UtcNow, transactionCt);
            if (!timeout && invitation is null)
                throw new InvalidOperationException("Lời mời không còn hiệu lực.");

            var invitationAttempt = attempt ?? invitation!.Attempt;
            if (invitationAttempt != trip.MatchingAttempt)
                return null;

            var resolved = await _workflow.ResolveInvitationAsync(
                tripId, driverId, invitationAttempt, DateTime.UtcNow, timeout, transactionCt);
            if (!resolved)
                return null;

            // Phối hợp Task 3: Tài xế từ chối (Reject) hoặc hết giờ (Timeout) -> Giải phóng tài xế này
            await _messageBus.ReleaseDriverAsync(driverId, tripId, transactionCt);

            if (trip.MatchingAttempt >= _options.MaxAttempts)
            {
                trip.MarkFailed();
                await _writer.SaveAsync(trip, transactionCt);
                return ToResponse(trip);
            }

            var nextAttempt = trip.BeginMatchingAttempt(_options.MaxAttempts);
            await _writer.SaveAsync(trip, transactionCt);
            await SendFindDriverAsync(trip, nextAttempt, transactionCt);
            return null;
        }, ct);

        if (response is not null)
            await _notifier.NotifyTripChangedAsync(response, ct);
    }
    
    public async Task StartEnRouteAsync(Guid tripId, CancellationToken ct = default)
    {
        var trip = await GetTripAsync(tripId, ct);
        trip.StartEnRoute();
        await SaveAndNotifyAsync(trip, ct);
    }

    public async Task PickupAsync(Guid tripId, CancellationToken ct = default)
    {
        var trip = await GetTripAsync(tripId, ct);
        trip.Pickup();
        await SaveAndNotifyAsync(trip, ct);
    }

    public async Task DropoffAsync(Guid tripId, CancellationToken ct = default)
    {
        var response = await _unitOfWork.ExecuteAsync(async transactionCt =>
        {
            var trip = await GetTripAsync(tripId, transactionCt);
            if (trip.DriverId is null || trip.FareEstimate is null or <= 0)
                throw new InvalidOperationException("Trip thiếu Driver hoặc giá hợp lệ để thanh toán.");

            if (trip.Status == TripStatus.PickedUp)
            {
                trip.Dropoff();
                trip.MarkPaymentPending();
                await _writer.SaveAsync(trip, transactionCt);
            }
            else if (trip.Status != TripStatus.PaymentPending)
            {
                throw new InvalidOperationException("Chuyến chưa ở trạng thái có thể thanh toán.");
            }

            var payment = new ProcessPaymentV1
            {
                CorrelationId = trip.Id,
                TripId = trip.Id,
                Amount = trip.FareEstimate.Value,
                Currency = "VND",
                IdempotencyKey = $"trip-{trip.Id:N}-payment-v1",
                PayerAccount = $"rider-{trip.RiderId:D}",
                PayeeAccount = $"driver-{trip.DriverId.Value:D}",
                OccurredAtUtc = DateTimeOffset.UtcNow
            };

            try
            {
                await _messageBus.SendPaymentAsync(payment, transactionCt);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new TripMessagePublishException(trip.Id, "Không gửi được yêu cầu thanh toán.", ex);
            }

            return ToResponse(trip);
        }, ct);

        await _notifier.NotifyTripChangedAsync(response, ct);
    }

    public async Task CancelAsync(Guid tripId, CancellationToken ct = default)
    {
        var response = await _unitOfWork.ExecuteAsync(async transactionCt =>
        {
            var trip = await GetTripAsync(tripId, transactionCt);
            trip.Cancel();
            await _writer.SaveAsync(trip, transactionCt);

            // Nếu chuyến bị hủy mà đã có tài xế nhận, phải giải phóng họ
            if (trip.DriverId.HasValue)
            {
                await _messageBus.ReleaseDriverAsync(trip.DriverId.Value, trip.Id, transactionCt);
            }

            return ToResponse(trip);
        }, ct);

        await _notifier.NotifyTripChangedAsync(response, ct);
    }

    public async Task MarkPaymentPendingAsync(Guid tripId, CancellationToken ct = default)
    {
        var trip = await GetTripAsync(tripId, ct);
        trip.MarkPaymentPending();
        await SaveAndNotifyAsync(trip, ct);
    }

    public async Task MarkPaidAsync(Guid tripId, CancellationToken ct = default)
    {
        var trip = await GetTripAsync(tripId, ct);
        if (trip.Status == TripStatus.Paid) return;

        trip.MarkPaid();
        await SaveAndNotifyAsync(trip, ct);
    }

    public async Task MarkFailedAsync(Guid tripId, CancellationToken ct = default)
    {
        var trip = await GetTripAsync(tripId, ct);
        trip.MarkFailed();
        await SaveAndNotifyAsync(trip, ct);
    }

    public async Task HandlePaymentSucceededAsync(PaymentSucceededV1 result, CancellationToken ct = default)
    {
        // All fields must be present and self-consistent before we trust the event.
        if (result.TripId == Guid.Empty ||
            result.CorrelationId != result.TripId ||
            result.PaymentId == Guid.Empty ||
            result.Amount <= 0 ||
            !string.Equals(result.Currency, "VND", StringComparison.Ordinal))
            throw new InvalidOperationException("PaymentSucceededV1 chứa dữ liệu không hợp lệ.");

        var trip = await GetTripAsync(result.TripId, ct);

        // Idempotency guard: if already Paid, verify the event matches what settled the trip.
        // An event with wrong data should not be silently swallowed just because the trip is Paid.
        if (trip.Status == TripStatus.Paid)
        {
            if (trip.FareEstimate != result.Amount)
                throw new InvalidOperationException(
                    "PaymentSucceededV1 tham chiếu Trip đã Paid nhưng Amount không khớp.");
            return;
        }

        if (trip.Status != TripStatus.PaymentPending)
            throw new InvalidOperationException(
                $"Trip không ở trạng thái chờ thanh toán: '{trip.Status}'.");

        if (trip.FareEstimate != result.Amount)
            throw new InvalidOperationException("Số tiền PaymentSucceededV1 không khớp FareEstimate của Trip.");

        trip.MarkPaid();
        await SaveAndNotifyAsync(trip, ct);
    }

    public async Task HandlePaymentFailedAsync(PaymentFailedV1 result, CancellationToken ct = default)
    {
        // Khối Validation & Anti-Corruption
        if (result.TripId == Guid.Empty ||
            result.CorrelationId != result.TripId ||
            result.PaymentId == Guid.Empty ||
            result.Amount <= 0 ||
            result.Currency != "VND" ||
            result.FailureCode != "PAYMENT_DECLINED" ||
            string.IsNullOrWhiteSpace(result.Reason) ||
            result.IdempotencyKey != $"trip-{result.TripId:N}-payment-v1")
        {
            throw new InvalidOperationException("PaymentFailedV1 không hợp lệ.");
        }

        var trip = await GetTripAsync(result.TripId, ct);

        // Đối chiếu tài chính
        if (trip.FareEstimate != result.Amount)
        {
            throw new InvalidOperationException("Số tiền PaymentFailedV1 không khớp FareEstimate của Trip.");
        }

        // Xử lý Event đến muộn khi chuyến đã Paid
        if (trip.Status == TripStatus.Paid)
        {
            // Trả về ngay lập tức để RabbitMQ Ack (xóa message), tránh ném lỗi khiến Broker retry.
            return;
        }

        // Đảm bảo chỉ xử lý khi chuyến đang chờ thanh toán
        if (trip.Status != TripStatus.PaymentPending) return;

        // Chống ghi lặp Reason và thay đổi trạng thái
        if (string.IsNullOrWhiteSpace(trip.PaymentFailureReason))
        {
            trip.RecordPaymentFailure(result.Reason);
            trip.MarkFailed();
            await SaveAndNotifyAsync(trip, ct);
        }
    }

    private async Task SendFindDriverAsync(Trip trip, int attempt, CancellationToken ct)
    {
        if (trip.PickupLatitude is null || trip.PickupLongitude is null)
            throw new InvalidOperationException("Trip thiếu tọa độ điểm đón cho Matching.");

        var excludedDrivers = await _workflow.GetInvitedDriverIdsAsync(trip.Id, ct);
        try
        {
            await _messageBus.SendFindDriverAsync(new FindDriverForTripV1
            {
                CorrelationId = trip.Id,
                TripId = trip.Id,
                Attempt = attempt,
                PickupLatitude = trip.PickupLatitude.Value,
                PickupLongitude = trip.PickupLongitude.Value,
                RadiusKm = _options.SearchRadiusKm,
                ExcludedDriverIds = excludedDrivers,
                OccurredAtUtc = DateTimeOffset.UtcNow
            }, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new TripMessagePublishException(trip.Id, "Không gửi được yêu cầu tìm tài xế.", ex);
        }
    }

    private async Task<Trip> GetTripAsync(Guid tripId, CancellationToken ct) =>
        await _reader.GetByIdAsync(tripId, ct)
        ?? throw new KeyNotFoundException($"Không tìm thấy chuyến đi '{tripId}'.");

    private async Task SaveAndNotifyAsync(Trip trip, CancellationToken ct)
    {
        await _writer.SaveAsync(trip, ct);
        await _notifier.NotifyTripChangedAsync(ToResponse(trip), ct);
    }

    private static TripResponse ToResponse(Trip trip) =>
        new(
            trip.Id,
            trip.RiderId,
            trip.DriverId,
            trip.PickupAddress,
            trip.DropoffAddress,
            trip.FareEstimate,
            trip.Status,
            trip.CreatedAt,
            trip.UpdatedAt,
            trip.PaymentFailureReason);
}

public sealed class TripMessagePublishException(Guid tripId, string message, Exception innerException)
    : Exception(message, innerException)
{
    public Guid TripId { get; } = tripId;
}
