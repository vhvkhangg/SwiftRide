using SwiftRide.Contracts.Payments;
using SwiftRide.PaymentService.Application.DTOs;

namespace SwiftRide.PaymentService.Application.Messaging;

public static class ProcessPaymentV1Mapper
{
    public static CreatePaymentRequest ToRequest(ProcessPaymentV1 message)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (message.CorrelationId == Guid.Empty)
        {
            throw new ArgumentException(
                "CorrelationId không được để trống.",
                nameof(message));
        }

        if (message.TripId == Guid.Empty)
        {
            throw new ArgumentException(
                "TripId không được để trống.",
                nameof(message));
        }

        var expectedKey = $"trip-{message.TripId:N}-payment-v1";

        if (!string.Equals(
            message.IdempotencyKey,
            expectedKey,
            StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "IdempotencyKey không khớp quy tắc của TripService.",
                nameof(message));
        }

        if (!string.Equals(message.Currency, "VND", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "PaymentService hiện chỉ hỗ trợ VND.",
                nameof(message));
        }

        if (message.Amount <= 0 || decimal.Round(message.Amount, 2) != message.Amount)
        {
            throw new ArgumentException(
                "Amount phải lớn hơn 0 và có tối đa 2 chữ số thập phân.",
                nameof(message));
        }

        if (message.OccurredAtUtc == default)
        {
            throw new ArgumentException(
                "OccurredAtUtc không hợp lệ.",
                nameof(message));
        }

        ValidateText(message.IdempotencyKey, nameof(message.IdempotencyKey));

        ValidateAccount(
            message.PayerAccount,
            "rider-",
            nameof(message.PayerAccount));

        ValidateAccount(
            message.PayeeAccount,
            "driver-",
            nameof(message.PayeeAccount));

        return new CreatePaymentRequest(
            message.TripId,
            message.Amount,
            message.IdempotencyKey,
            message.PayerAccount,
            message.PayeeAccount);
    }

    private static void ValidateText(
        string? value,
        string field)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.Length > 128)
        {
            throw new ArgumentException(
                $"{field} phải có từ 1 đến 128 ký tự.");
        }
    }

    private static void ValidateAccount(
        string? account,
        string prefix,
        string field)
    {
        if (string.IsNullOrWhiteSpace(account) ||
            !account.StartsWith(prefix, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"{field} phải bắt đầu bằng '{prefix}'.");
        }

        var guidPart = account[prefix.Length..];

        if (!Guid.TryParseExact(guidPart, "D", out var id) ||
            id == Guid.Empty ||
            !string.Equals(
                account,
                $"{prefix}{id:D}",
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"{field} phải có định dạng {prefix}<GUID> hợp lệ.");
        }
    }
}