using SwiftRide.PaymentService.Domain.Entities;
using SwiftRide.PaymentService.Application.DTOs;
using SwiftRide.PaymentService.Domain.Enums;
using Xunit;

namespace SwiftRide.PaymentService.Tests.Domain;

public sealed class PaymentTests
{
    private static Payment NewPayment() => Payment.Create(
        Guid.NewGuid(), 100_000m, Guid.NewGuid().ToString("N"), "rider-001", "driver-001");

    [Fact]
    public void Create_Valid_ShouldStartPendingAndUnsynced()
    {
        var trip = Guid.NewGuid();
        var p = Payment.Create(trip, 100_000m, "k", "rider-001", "driver-001");
        Assert.NotEqual(Guid.Empty, p.Id);
        Assert.Equal(trip, p.TripId);
        Assert.Equal(100_000m, p.Amount);
        Assert.Equal("k", p.IdempotencyKey);
        Assert.Equal("rider-001", p.PayerAccount);
        Assert.Equal("driver-001", p.PayeeAccount);
        Assert.Equal(PaymentStatus.Pending, p.Status);
        Assert.False(p.IsTripSynced);
        Assert.Equal(p.CreatedAt, p.UpdatedAt);
    }

    [Fact]
    public void Create_EmptyTrip_ShouldThrow() => Assert.Throws<ArgumentException>(() =>
        Payment.Create(Guid.Empty, 1, "k", "rider-001", "driver-001"));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_BadAmount_ShouldThrow(decimal amount) =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Payment.Create(Guid.NewGuid(), amount, "k", "rider-001", "driver-001"));

    [Fact]
    public void Create_TooManyDecimalPlaces_ShouldThrow() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Payment.Create(Guid.NewGuid(), 1.234m, "k", "rider-001", "driver-001"));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_BlankKey_ShouldThrow(string key) =>
        Assert.Throws<ArgumentException>(() =>
            Payment.Create(Guid.NewGuid(), 1, key, "rider-001", "driver-001"));

    [Fact]
    public void Create_AccountTooLong_ShouldThrow() =>
        Assert.Throws<ArgumentException>(() =>
            Payment.Create(Guid.NewGuid(), 1, "k", new string('x', 129), "driver-001"));

    [Fact]
    public void MarkSucceeded_FromPending_ShouldWork()
    {
        var p = NewPayment();
        p.MarkSucceeded();
        Assert.Equal(PaymentStatus.Succeeded, p.Status);
    }

    [Fact]
    public void MarkFailed_FromPending_ShouldWork()
    {
        var p = NewPayment();
        p.MarkFailed();
        Assert.Equal(PaymentStatus.Failed, p.Status);
    }

    [Fact]
    public void MarkSucceeded_Twice_ShouldThrow()
    {
        var p = NewPayment();
        p.MarkSucceeded();
        Assert.Throws<InvalidOperationException>(p.MarkSucceeded);
    }

    [Fact]
    public void Refund_AfterSuccess_ShouldWork()
    {
        var p = NewPayment();
        p.MarkSucceeded();
        p.Refund();
        Assert.Equal(PaymentStatus.Refunded, p.Status);
    }

    [Fact]
    public void Refund_WhilePending_ShouldThrow() =>
        Assert.Throws<InvalidOperationException>(NewPayment().Refund);

    [Fact]
    public void MarkTripSynced_FromSucceeded_IsIdempotent()
    {
        var p = NewPayment();
        p.MarkSucceeded();
        p.MarkTripSynced();
        p.MarkTripSynced();
        Assert.True(p.IsTripSynced);
    }

    [Fact]
    public void MarkTripSynced_FromPending_ShouldThrow() =>
        Assert.Throws<InvalidOperationException>(NewPayment().MarkTripSynced);

    [Fact]
    public void MarkFailed_WithMetadata_ShouldPersistReasonAndIncrementVersion()
    {
        var payment = NewPayment();
        var originalVersion = payment.Version;

        payment.MarkFailed("PAYMENT_DECLINED", "Tài khoản không đủ số dư.");

        Assert.Equal(PaymentStatus.Failed, payment.Status);
        Assert.Equal("PAYMENT_DECLINED", payment.FailureCode);
        Assert.Equal("Tài khoản không đủ số dư.", payment.FailureReason);
        Assert.Equal(originalVersion + 1, payment.Version);
        Assert.False(payment.IsTripSynced);
    }

    [Theory]
    [InlineData("", "Reason")]
    [InlineData("PAYMENT_DECLINED", "")]
    [InlineData("PAYMENT_DECLINED", "   ")]
    public void MarkFailed_InvalidMetadata_ShouldKeepPaymentPending(
        string code, string reason)
    {
        var payment = NewPayment();
        var originalVersion = payment.Version;

        Assert.Throws<ArgumentException>(() =>
            payment.MarkFailed(code, reason));

        Assert.Equal(PaymentStatus.Pending, payment.Status);
        Assert.Equal(originalVersion, payment.Version);
    }

    [Fact]
    public void MarkFailed_AfterSuccess_ShouldNotOverwriteStatus()
    {
        var payment = NewPayment();
        payment.MarkSucceeded();

        Assert.Throws<InvalidOperationException>(() =>
            payment.MarkFailed("PAYMENT_DECLINED", "Too late"));

        Assert.Equal(PaymentStatus.Succeeded, payment.Status);
    }

    [Fact]
    public void FailedResponse_ShouldExposeFailureMetadata()
    {
        var payment = NewPayment();
        payment.MarkFailed("PAYMENT_DECLINED", "Thanh toán bị từ chối.");

        var response = PaymentResponse.From(payment);

        Assert.Equal(payment.Id, response.Id);
        Assert.Equal("Failed", response.Status);
        Assert.Equal("PAYMENT_DECLINED", response.FailureCode);
        Assert.Equal("Thanh toán bị từ chối.", response.FailureReason);
    }
}
