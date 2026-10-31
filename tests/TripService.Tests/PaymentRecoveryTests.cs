using Microsoft.Extensions.Options;
using Moq;
using SwiftRide.Contracts.Payments;
using SwiftRide.Messaging;
using SwiftRide.TripService.Application.Interfaces;
using SwiftRide.TripService.Application.Services;
using SwiftRide.TripService.Domain.Entities;
using SwiftRide.TripService.Domain.Enums;
using SwiftRide.TripService.Domain.Interfaces;
using Xunit;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace SwiftRide.TripService.Tests;

public sealed class PaymentRecoveryTests
{
    [Fact]
    public async Task MessageLogFilter_LogsIdentifiersWithoutAccountOrPayload()
    {
        var message = new ProcessPaymentV1
        {
            TripId = Guid.NewGuid(), CorrelationId = Guid.NewGuid(), IdempotencyKey = "test-key",
            PayerAccount = "secret-account-not-for-logs"
        };
        var context = new Mock<ConsumeContext<ProcessPaymentV1>>();
        context.SetupGet(item => item.Message).Returns(message);
        context.SetupGet(item => item.MessageId).Returns(Guid.NewGuid());
        var logger = new Mock<ILogger<MessageLogFilter<ProcessPaymentV1>>>();
        Dictionary<string, object?>? scope = null;
        logger.Setup(item => item.BeginScope(It.IsAny<Dictionary<string, object?>>()))
            .Callback<Dictionary<string, object?>>(value => scope = value);
        var next = new Mock<IPipe<ConsumeContext<ProcessPaymentV1>>>();
        next.Setup(item => item.Send(context.Object)).Returns(Task.CompletedTask);
        await new MessageLogFilter<ProcessPaymentV1>(logger.Object).Send(context.Object, next.Object);
        Assert.NotNull(scope);
        Assert.Equal(message.TripId, scope["TripId"]);
        Assert.Equal(context.Object.MessageId, scope["MessageId"]);
        Assert.Contains("Attempt", scope.Keys);
        Assert.Contains("RetryAttempt", scope.Keys);
        Assert.DoesNotContain(message.PayerAccount, string.Join(" ", scope.Values));
        foreach (var invocation in logger.Invocations.Where(item => item.Method.Name == "Log"))
            Assert.DoesNotContain(message.PayerAccount, invocation.Arguments[2].ToString()!);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TechnicalFault_RemainsPending_ThenCanSucceedOrDecline(bool declined)
    {
        var trip = Trip.Create(Guid.NewGuid(), "A", "B", 50_000);
        trip.ConfirmPrice(); trip.AcceptByDriver(Guid.NewGuid()); trip.StartEnRoute();
        trip.Pickup(); trip.Dropoff(); trip.MarkPaymentPending();
        var reader = new Mock<ITripReader>();
        reader.Setup(item => item.GetByIdAsync(trip.Id, It.IsAny<CancellationToken>())).ReturnsAsync(trip);
        var writer = new Mock<ITripWriter>();
        var service = new TripApplicationService(writer.Object, reader.Object,
            Mock.Of<ITripWorkflowRepository>(), Mock.Of<ITripUnitOfWork>(), Mock.Of<ITripNotifier>(),
            Mock.Of<IMatchingPricingClient>(), Mock.Of<ITripMessageBus>(), Options.Create(new TripMatchingOptions()));
        var command = new ProcessPaymentV1
        {
            TripId = trip.Id, CorrelationId = trip.Id, IdempotencyKey = $"trip-{trip.Id:N}-payment-v1"
        };
        var ct = TestContext.Current.CancellationToken;
        await service.HandlePaymentFaultAsync(command, ct);
        Assert.Equal(TripStatus.PaymentPending, trip.Status);
        Assert.Contains("_error", trip.PaymentFailureReason);
        if (declined)
        {
            var result = new PaymentFailedV1
            {
                TripId = trip.Id, CorrelationId = trip.Id, PaymentId = Guid.NewGuid(), Amount = 50_000,
                IdempotencyKey = command.IdempotencyKey, FailureCode = "PAYMENT_DECLINED", Reason = "Declined",
                OccurredAtUtc = DateTimeOffset.UtcNow
            };
            await service.HandlePaymentFailedAsync(result, ct);
            await service.HandlePaymentFailedAsync(result, ct);
            Assert.Equal(TripStatus.Failed, trip.Status);
        }
        else
        {
            var result = new PaymentSucceededV1
            {
                TripId = trip.Id, CorrelationId = trip.Id, PaymentId = Guid.NewGuid(), Amount = 50_000,
                Currency = "VND", OccurredAtUtc = DateTimeOffset.UtcNow
            };
            await service.HandlePaymentSucceededAsync(result, ct);
            await service.HandlePaymentSucceededAsync(result, ct);
            Assert.Equal(TripStatus.Paid, trip.Status);
        }
        writer.Verify(item => item.SaveAsync(trip, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}
