using SwiftRide.PaymentService.Application.DTOs;
using SwiftRide.PaymentService.Application.Integrations;
using SwiftRide.PaymentService.Domain.Entities;
using SwiftRide.PaymentService.Domain.Enums;
using SwiftRide.PaymentService.Domain.Exceptions;
using SwiftRide.PaymentService.Domain.Repositories;

namespace SwiftRide.PaymentService.Application.Services;

public sealed class PaymentApplicationService : IPaymentApplicationService
{
    private readonly IPaymentRepository _payments;
    private readonly ITripServiceClient _trips;

    public PaymentApplicationService(IPaymentRepository payments, ITripServiceClient trips)
    {
        _payments = payments;
        _trips = trips;
    }

    public async Task<PaymentResponse> CreatePaymentAsync(
        CreatePaymentRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.TripId == Guid.Empty)
            throw new ArgumentException("ID chuyến đi không được để trống.");
        if (request.Amount <= 0 || decimal.Round(request.Amount, 2) != request.Amount)
            throw new ArgumentException("Số tiền không hợp lệ.");
        ValidateText(request.IdempotencyKey, nameof(request.IdempotencyKey));
        ValidateText(request.PayerAccount, nameof(request.PayerAccount));
        ValidateText(request.PayeeAccount, nameof(request.PayeeAccount));

        var existing = await _payments.GetByIdempotencyKeyAsync(
            request.IdempotencyKey, cancellationToken);
        if (existing is not null)
            return await HandleExistingAsync(existing, request, cancellationToken);

        var existingTripPayment = await _payments.GetSettledByTripIdAsync(request.TripId, cancellationToken);

        if (existingTripPayment is not null)
        {
            throw new InvalidOperationException(
                "Trip đã có Payment được ghi nhận. " +
                "Không thể thanh toán lại bằng IdempotencyKey khác.");
        }

        var trip = await _trips.GetByIdAsync(request.TripId, cancellationToken)
            ?? throw new KeyNotFoundException($"Không tìm thấy chuyến đi '{request.TripId}'.");
        if (trip.Status != TripStatusCode.PaymentPending)
            throw new InvalidOperationException($"Trip chưa cho phép thanh toán: '{trip.Status}'.");
        if (trip.RiderId == Guid.Empty || trip.DriverId is null || trip.DriverId == Guid.Empty)
            throw new InvalidOperationException("Trip thiếu RiderId hoặc DriverId hợp lệ.");
        if (trip.FareEstimate is null || trip.FareEstimate <= 0)
            throw new InvalidOperationException("Trip chưa có giá tiền hợp lệ.");
        if (request.Amount != trip.FareEstimate.Value)
            throw new InvalidOperationException("Số tiền khác với giá của Trip.");

        var payer = $"rider-{trip.RiderId:D}";
        var payee = $"driver-{trip.DriverId.Value:D}";
        if (!string.Equals(request.PayerAccount, payer, StringComparison.Ordinal) ||
            !string.Equals(request.PayeeAccount, payee, StringComparison.Ordinal))
            throw new InvalidOperationException("Tài khoản thanh toán không khớp Trip.");

        var payment = Payment.Create(trip.Id, trip.FareEstimate.Value,
            request.IdempotencyKey, payer, payee);
        var entries = new[]
        {
            LedgerEntry.Create(payment.Id, LedgerEntryType.Debit, payer, payment.Amount),
            LedgerEntry.Create(payment.Id, LedgerEntryType.Credit, payee, payment.Amount)
        };
        payment.MarkSucceeded();
        try
        {
            await _payments.AddAsync(payment, cancellationToken);
            await _payments.AddLedgerEntriesAsync(entries, cancellationToken);
            await _payments.SaveChangesAsync(cancellationToken);
        }
        catch (DuplicateIdempotencyKeyException)
        {
            var winner = await _payments.GetByIdempotencyKeyAsync(
                request.IdempotencyKey, cancellationToken);
            if (winner is null)
                throw new InvalidOperationException(
                    "Không tải được Payment sau khi trùng key. Hãy retry cùng key.");
            return await HandleExistingAsync(winner, request, cancellationToken);
        }
        catch (TripAlreadyPaidException ex)
        {
            throw new InvalidOperationException(
                "Trip vừa được thanh toán bởi request khác.",
                ex);
        }

        await SyncTripAsync(payment, cancellationToken);
        return PaymentResponse.From(payment);
    }

    private async Task<PaymentResponse> HandleExistingAsync(
        Payment payment, CreatePaymentRequest request, CancellationToken ct)
    {
        if (payment.TripId != request.TripId || payment.Amount != request.Amount ||
            !string.Equals(payment.PayerAccount, request.PayerAccount, StringComparison.Ordinal) ||
            !string.Equals(payment.PayeeAccount, request.PayeeAccount, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "IdempotencyKey đã được sử dụng cho payload khác.");

        await SyncTripAsync(payment, ct);
        return PaymentResponse.From(payment);
    }

    private async Task SyncTripAsync(Payment payment, CancellationToken ct)
    {
        if (payment.Status != PaymentStatus.Succeeded || payment.IsTripSynced)
            return;

        await _trips.MarkPaidAsync(payment.TripId, ct);
        payment.MarkTripSynced();
        await _payments.SaveChangesAsync(ct);
    }

    private static void ValidateText(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 128)
            throw new ArgumentException($"{field} phải có từ 1 đến 128 ký tự.");
    }

    public async Task<PaymentResponse?> GetPaymentByIdAsync(
        Guid paymentId, CancellationToken cancellationToken = default)
    {
        var p = await _payments.GetByIdAsync(paymentId, cancellationToken);
        return p is null ? null : PaymentResponse.From(p);
    }

    public async Task<PaymentResponse?> GetPaymentByTripIdAsync(
        Guid tripId,
        CancellationToken cancellationToken = default)
    {
        if (tripId == Guid.Empty)
        {
            throw new ArgumentException(
                "TripId không được để trống.",
                nameof(tripId));
        }

        var payment = await _payments.GetSettledByTripIdAsync(
            tripId,
            cancellationToken);

        return payment is null ? null : PaymentResponse.From(payment);
    }

    public async Task<PaymentResponse> ReconcilePaymentAsync(
        Guid paymentId, CancellationToken cancellationToken = default)
    {
        var p = await _payments.GetByIdAsync(paymentId, cancellationToken)
            ?? throw new KeyNotFoundException($"Không tìm thấy Payment '{paymentId}'.");
        if (p.Status != PaymentStatus.Succeeded)
            throw new InvalidOperationException("Chỉ reconcile Payment Succeeded.");
        await SyncTripAsync(p, cancellationToken);
        return PaymentResponse.From(p);
    }

    public async Task<RefundPaymentResponse> RefundPaymentAsync(
        Guid paymentId, CancellationToken cancellationToken = default)
    {
        var payment = await _payments.GetByIdAsync(paymentId, cancellationToken)
            ?? throw new KeyNotFoundException($"Không tìm thấy Payment '{paymentId}'.");
        if (payment.Status != PaymentStatus.Succeeded)
            throw new InvalidOperationException("Chỉ refund Payment Succeeded.");

        var original = await _payments.GetLedgerEntriesByPaymentIdAsync(
            paymentId, cancellationToken);
        var validOriginalLedger =
            original.Count == 2 &&
            original.Any(x =>
                x.Type == LedgerEntryType.Debit &&
                x.Account == payment.PayerAccount &&
                x.Amount == payment.Amount) &&
            original.Any(x =>
                x.Type == LedgerEntryType.Credit &&
                x.Account == payment.PayeeAccount &&
                x.Amount == payment.Amount);

        if (!validOriginalLedger)
        {
            throw new InvalidOperationException(
                "Ledger gốc không hợp lệ. " +
                "Không thể thực hiện refund.");
        }
        
        payment.Refund();
        var reversal = original.Select(x => LedgerEntry.Create(payment.Id,
            x.Type == LedgerEntryType.Debit ? LedgerEntryType.Credit : LedgerEntryType.Debit,
            x.Account, x.Amount)).ToArray();
        await _payments.AddLedgerEntriesAsync(reversal, cancellationToken);
        await _payments.SaveChangesAsync(cancellationToken);
        return RefundPaymentResponse.From(payment);
    }
}
