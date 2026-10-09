
using SwiftRide.PaymentService.Domain.Entities;
using SwiftRide.PaymentService.Domain.Enums;
using Xunit;

namespace SwiftRide.PaymentService.Tests.Domain;

public sealed class PaymentVersionTests
{
    private static Payment NewPayment()
    {
        return Payment.Create(
            Guid.NewGuid(),
            100_000m,
            Guid.NewGuid().ToString("N"),
            "rider-001",
            "driver-001");
    }

    [Fact]
    public void Create_ShouldStartWithVersionZero()
    {
        var payment = NewPayment();

        Assert.Equal(0L, payment.Version);
        Assert.Equal(PaymentStatus.Pending, payment.Status);
    }

    [Fact]
    public void MarkSucceeded_ShouldIncrementVersion()
    {
        var payment = NewPayment();

        payment.MarkSucceeded();

        Assert.Equal(1L, payment.Version);
        Assert.Equal(PaymentStatus.Succeeded, payment.Status);
    }

    [Fact]
    public void MarkFailed_ShouldIncrementVersion()
    {
        var payment = NewPayment();

        payment.MarkFailed();

        Assert.Equal(1L, payment.Version);
        Assert.Equal(PaymentStatus.Failed, payment.Status);
    }

    [Fact]
    public void Refund_ShouldIncrementVersion()
    {
        var payment = NewPayment();

        payment.MarkSucceeded();

        var beforeRefund = payment.Version;

        payment.Refund();

        Assert.Equal(beforeRefund + 1, payment.Version);
        Assert.Equal(PaymentStatus.Refunded, payment.Status);
    }

    [Fact]
    public void MarkTripSynced_Twice_ShouldIncrementOnlyOnce()
    {
        var payment = NewPayment();
        payment.MarkSucceeded();

        var beforeSync = payment.Version;

        payment.MarkTripSynced();
        payment.MarkTripSynced();

        Assert.True(payment.IsTripSynced);
        Assert.Equal(beforeSync + 1, payment.Version);
    }

    [Fact]
    public void InvalidRefund_ShouldNotChangeVersion()
    {
        var payment = NewPayment();
        var before = payment.Version;

        Assert.Throws<InvalidOperationException>(
            payment.Refund);

        Assert.Equal(before, payment.Version);
        Assert.Equal(PaymentStatus.Pending, payment.Status);
    }
}
