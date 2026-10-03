using SwiftRide.PaymentService.Domain.Entities;
using SwiftRide.PaymentService.Domain.Enums;

using Xunit;

namespace SwiftRide.PaymentService.Tests.Domain;

public sealed class PaymentTests
{
    [Fact]
    public void Create_WithValidData_ShouldCreatePendingPayment()
    {
        //Arrange
        var tripId = Guid.NewGuid();
        const decimal amount = 100_000m;
        const string idempotencyKey = "payment-test-001";

        //Act
        var payment = Payment.Create(
            tripId,
            amount,
            idempotencyKey
        );

        //Assert
        Assert.NotEqual(Guid.Empty, payment.Id);
        Assert.Equal(tripId, payment.TripId);
        Assert.Equal(amount, payment.Amount);
        Assert.Equal(idempotencyKey, payment.IdempotencyKey);
        Assert.Equal(PaymentStatus.Pending, payment.Status);
        Assert.Equal(payment.CreatedAt, payment.UpdatedAt);
    }

    [Fact]
    public void Create_WithEmptyTripId_ShouldThrowArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            Payment.Create(
                Guid.Empty,
                100_000m,
                "payment-test-002"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100000)]
    public void Create_WithNonPositiveAmount_ShouldThrowArgumentOutOfRangeException(
        decimal amount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Payment.Create(
                Guid.NewGuid(),
                amount,
                "payment-test-003"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankIdempotencyKey_ShouldThrowArgumentException(
        string idempotencyKey)
    {
        Assert.Throws<ArgumentException>(() =>
            Payment.Create(
                Guid.NewGuid(),
                100_000m,
                idempotencyKey));
    }

    [Fact]
    public void MarkSucceeded_WhenPending_ShouldChangeStatusToSucceeded()
    {
        var payment = CreatePayment();

        payment.MarkSucceeded();

        Assert.Equal(PaymentStatus.Succeeded, payment.Status);
    }

    [Fact]
    public void MarkFailed_WhenPending_ShouldChangeStatusToFailed()
    {
        var payment = CreatePayment();

        payment.MarkFailed();

        Assert.Equal(PaymentStatus.Failed, payment.Status);
    }

    [Fact]
    public void MarkSucceeded_WhenAlreadySucceeded_ShouldThrowInvalidOperationException()
    {
        var payment = CreatePayment();

        payment.MarkSucceeded();

        Assert.Throws<InvalidOperationException>(
            payment.MarkSucceeded);
    }

    [Fact]
    public void Refund_WhenSucceeded_ShouldChangeStatusToRefunded()
    {
        var payment = CreatePayment();
        payment.MarkSucceeded();

        payment.Refund();

        Assert.Equal(PaymentStatus.Refunded, payment.Status);
    }

    [Fact]
    public void Refund_WhenPending_ShouldThrowInvalidOperationException()
    {
        var payment = CreatePayment();

        Assert.Throws<InvalidOperationException>(
            payment.Refund);
    }

    private static Payment CreatePayment()
    {
        return Payment.Create(
            Guid.NewGuid(),
            100_000m,
            Guid.NewGuid().ToString());
    }
}