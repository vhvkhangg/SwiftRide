
using SwiftRide.Contracts.Payments;
using SwiftRide.PaymentService.Application.Messaging;
using Xunit;

namespace SwiftRide.PaymentService.Tests.Application;

public sealed class ProcessPaymentV1MapperTests
{
    private static ProcessPaymentV1 NewMessage()
    {
        var riderId = Guid.NewGuid();
        var driverId = Guid.NewGuid();

        var tripId = Guid.NewGuid();
        return new ProcessPaymentV1
        {
            CorrelationId = tripId,
            TripId = tripId,
            Amount = 100_000m,
            Currency = "VND",
            IdempotencyKey = $"trip-{tripId:N}-payment-v1",
            PayerAccount = $"rider-{riderId:D}",
            PayeeAccount = $"driver-{driverId:D}",
            OccurredAtUtc = DateTimeOffset.UtcNow
        };
    }

    [Fact]
    public void ValidMessage_ShouldMapSuccessfully()
    {
        var message = NewMessage();

        var request = ProcessPaymentV1Mapper.ToRequest(message);

        Assert.Equal(message.TripId, request.TripId);
        Assert.Equal(message.Amount, request.Amount);
        Assert.Equal(
            message.IdempotencyKey,
            request.IdempotencyKey);
        Assert.Equal(
            message.PayerAccount,
            request.PayerAccount);
        Assert.Equal(
            message.PayeeAccount,
            request.PayeeAccount);
    }

    [Fact]
    public void InvalidCurrency_ShouldReject()
    {
        var message = NewMessage();
        message.Currency = "USD";

        Assert.Throws<ArgumentException>(
            () => ProcessPaymentV1Mapper.ToRequest(message));
    }

    [Fact]
    public void EmptyCorrelationId_ShouldReject()
    {
        var message = NewMessage();
        message.CorrelationId = Guid.Empty;

        Assert.Throws<ArgumentException>(
            () => ProcessPaymentV1Mapper.ToRequest(message));
    }

    [Fact]
    public void InvalidPayerAccount_ShouldReject()
    {
        var message = NewMessage();
        message.PayerAccount = "rider-invalid";

        Assert.Throws<ArgumentException>(
            () => ProcessPaymentV1Mapper.ToRequest(message));
    }

    [Fact]
    public void InvalidAmount_ShouldReject()
    {
        var message = NewMessage();
        message.Amount = -100m;

        Assert.Throws<ArgumentException>(
            () => ProcessPaymentV1Mapper.ToRequest(message));
    }

    [Fact]
    public void EmptyIdempotencyKey_ShouldReject()
    {
        var message = NewMessage();
        message.IdempotencyKey = string.Empty;

        Assert.Throws<ArgumentException>(() =>
            ProcessPaymentV1Mapper.ToRequest(message));
    }
}
