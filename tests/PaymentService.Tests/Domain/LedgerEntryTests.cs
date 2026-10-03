using SwiftRide.PaymentService.Domain.Entities;
using SwiftRide.PaymentService.Domain.Enums;
using Xunit;

namespace SwiftRide.PaymentService.Tests.Domain;

public sealed class LedgerEntryTests
{
    [Fact]
    public void Create_WithValidData_ShouldCreateLedgerEntry()
    {
        // Arrange
        var paymentId = Guid.NewGuid();

        // Act
        var entry = LedgerEntry.Create(
            paymentId,
            LedgerEntryType.Debit,
            "RiderWallet",
            100_000m);

        // Assert
        Assert.NotEqual(Guid.Empty, entry.Id);
        Assert.Equal(paymentId, entry.PaymentId);
        Assert.Equal(LedgerEntryType.Debit, entry.Type);
        Assert.Equal("RiderWallet", entry.Account);
        Assert.Equal(100_000m, entry.Amount);
    }

    [Fact]
    public void Create_WithEmptyPaymentId_ShouldThrowArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            LedgerEntry.Create(
                Guid.Empty,
                LedgerEntryType.Debit,
                "RiderWallet",
                100_000m));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankAccount_ShouldThrowArgumentException(
        string account)
    {
        Assert.Throws<ArgumentException>(() =>
            LedgerEntry.Create(
                Guid.NewGuid(),
                LedgerEntryType.Debit,
                account,
                100_000m));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100000)]
    public void Create_WithNonPositiveAmount_ShouldThrowArgumentOutOfRangeException(
        decimal amount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LedgerEntry.Create(
                Guid.NewGuid(),
                LedgerEntryType.Credit,
                "DriverWallet",
                amount));
    }
}