using SwiftRide.Contracts.Payments;
using SwiftRide.PaymentService.Application.DTOs;
using SwiftRide.PaymentService.Application.Integrations;
using SwiftRide.PaymentService.Application.Messaging;
using SwiftRide.PaymentService.Application.PaymentMethods;
using SwiftRide.PaymentService.Application.Services;
using SwiftRide.PaymentService.Domain.Enums;
using Xunit;

namespace SwiftRide.PaymentService.Tests.Application;

public sealed class PaymentMethodFactoryTests
{
    private static readonly TripSnapshot Trip = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 50_000m, default);

    private static CreatePaymentRequest Request(decimal amount, PaymentMethodType method) =>
        new(Trip.Id, amount, "key", "rider-x", "driver-y", method);

    [Theory]
    [InlineData(PaymentMethodType.Wallet, typeof(WalletPaymentMethod))]
    [InlineData(PaymentMethodType.Card, typeof(CardPaymentMethod))]
    public void Create_ReturnsMethodForType(PaymentMethodType type, Type expected)
    {
        var method = new PaymentMethodFactory().Create(type);

        Assert.IsType(expected, method);
        Assert.Equal(type, method.Type);
    }

    [Fact]
    public void Create_RejectsUnknownType() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new PaymentMethodFactory().Create((PaymentMethodType)99));

    [Fact]
    public async Task Decision_UsesMethodFromRequest()
    {
        var decisions = new PaymentMethodDecisionService(new PaymentMethodFactory());
        var overLimit = CardPaymentMethod.PerTransactionLimit + 1;

        var wallet = await decisions.DecideAsync(Trip, Request(overLimit, PaymentMethodType.Wallet), TestContext.Current.CancellationToken);
        var card = await decisions.DecideAsync(Trip, Request(overLimit, PaymentMethodType.Card), TestContext.Current.CancellationToken);

        Assert.True(wallet.Approved);
        Assert.False(card.Approved);
        Assert.Equal("PAYMENT_DECLINED", card.FailureCode);
    }

    [Theory]
    [InlineData(null, PaymentMethodType.Wallet)]
    [InlineData("Wallet", PaymentMethodType.Wallet)]
    [InlineData("card", PaymentMethodType.Card)]
    public void Mapper_ReadsPaymentMethodWithWalletDefault(string? value, PaymentMethodType expected) =>
        Assert.Equal(expected, ProcessPaymentV1Mapper.ToRequest(Message(value)).PaymentMethod);

    [Theory]
    [InlineData("Cash")]
    [InlineData("2")]
    public void Mapper_RejectsUnsupportedPaymentMethod(string value) =>
        Assert.Throws<ArgumentException>(() => ProcessPaymentV1Mapper.ToRequest(Message(value)));

    private static ProcessPaymentV1 Message(string? method)
    {
        var tripId = Guid.NewGuid();
        return new ProcessPaymentV1
        {
            CorrelationId = tripId,
            TripId = tripId,
            Amount = 100_000m,
            IdempotencyKey = $"trip-{tripId:N}-payment-v1",
            PayerAccount = $"rider-{Guid.NewGuid():D}",
            PayeeAccount = $"driver-{Guid.NewGuid():D}",
            OccurredAtUtc = DateTimeOffset.UtcNow,
            PaymentMethod = method!
        };
    }
}
