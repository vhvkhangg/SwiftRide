
using MassTransit;
using Moq;
using SwiftRide.Contracts.Payments;
using SwiftRide.PaymentService.Application.Consumers;
using SwiftRide.PaymentService.Application.DTOs;
using SwiftRide.PaymentService.Application.Services;
using Xunit;
using SwiftRide.PaymentService.Application.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;

namespace SwiftRide.PaymentService.Tests.Application;

public sealed class ProcessPaymentConsumerTests
{
    private readonly Mock<IPaymentApplicationService> _service = new();

    private static ProcessPaymentV1 NewMessage()
    {
        var rider = Guid.NewGuid();
        var driver = Guid.NewGuid();

        return new ProcessPaymentV1
        {
            CorrelationId = Guid.NewGuid(),
            TripId = Guid.NewGuid(),
            Amount = 100_000m,
            Currency = "VND",
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            PayerAccount = $"rider-{rider:D}",
            PayeeAccount = $"driver-{driver:D}",
            OccurredAtUtc = DateTimeOffset.UtcNow
        };
    }

    private static Mock<ConsumeContext<ProcessPaymentV1>>
        CreateContext(ProcessPaymentV1 message)
    {
        var context = new Mock<ConsumeContext<ProcessPaymentV1>>();

        context.SetupGet(x => x.Message).Returns(message);

        context.SetupGet(x => x.CancellationToken)
            .Returns(CancellationToken.None);

        context.Setup(x => x.Publish(
                It.IsAny<PaymentSucceededV1>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        context.Setup(x => x.Publish(
                It.IsAny<PaymentFailedV1>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return context;
    }

    [Fact]
    public async Task ValidPayment_ShouldPublishPaymentSucceeded()
    {
        var message = NewMessage();
        var paymentId = Guid.NewGuid();

        var response = new PaymentResponse(
            paymentId,
            message.TripId,
            message.Amount,
            "Succeeded",
            message.IdempotencyKey,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            false);

        _service.Setup(x => x.ProcessPaymentCommandAsync(
                It.IsAny<CreatePaymentRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var context = CreateContext(message);
        var consumer = new ProcessPaymentConsumer(_service.Object, NullLogger<ProcessPaymentConsumer>.Instance);

        await consumer.Consume(context.Object);

        _service.Verify(x => x.ProcessPaymentCommandAsync(
            It.Is<CreatePaymentRequest>(request =>
                request.TripId == message.TripId &&
                request.Amount == message.Amount &&
                request.IdempotencyKey == message.IdempotencyKey),
            It.IsAny<CancellationToken>()), Times.Once);

        context.Verify(x => x.Publish(
            It.Is<PaymentSucceededV1>(e =>
                e.CorrelationId == message.CorrelationId &&
                e.TripId == message.TripId &&
                e.PaymentId == paymentId &&
                e.Amount == message.Amount &&
                e.Currency == "VND"),
            It.IsAny<CancellationToken>()), Times.Once);

        context.Verify(x => x.Publish(
            It.IsAny<PaymentFailedV1>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task InvalidMessage_ShouldNotProcessPayment()
    {
        var message = NewMessage();
        message.Currency = "USD";

        var context = CreateContext(message);
        var consumer = new ProcessPaymentConsumer(_service.Object, NullLogger<ProcessPaymentConsumer>.Instance);

        await Assert.ThrowsAsync<ArgumentException>(
            () => consumer.Consume(context.Object));

        _service.VerifyNoOtherCalls();

        context.Verify(x => x.Publish(
            It.IsAny<PaymentSucceededV1>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessingFailure_ShouldNotPublishSuccess()
    {
        var message = NewMessage();

        _service.Setup(x => x.ProcessPaymentCommandAsync(
                It.IsAny<CreatePaymentRequest>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(
                "Simulated payment processing failure"));

        var context = CreateContext(message);
        var consumer = new ProcessPaymentConsumer(_service.Object, NullLogger<ProcessPaymentConsumer>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => consumer.Consume(context.Object));

        context.Verify(x => x.Publish(
            It.IsAny<PaymentSucceededV1>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }



    [Fact]
    public async Task BusinessConflict_ShouldNotPublishPaymentResult()
    {
        var message = NewMessage();
        var context = CreateContext(message);

        _service.Setup(x => x.ProcessPaymentCommandAsync(
                It.IsAny<CreatePaymentRequest>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PaymentCommandRejectedException(
                "IdempotencyKey đã được sử dụng."));

        var consumer = new ProcessPaymentConsumer(
            _service.Object,
            NullLogger<ProcessPaymentConsumer>.Instance);

        await Assert.ThrowsAsync<PaymentCommandRejectedException>(
            () => consumer.Consume(context.Object));

        context.Verify(x => x.Publish(
            It.IsAny<PaymentSucceededV1>(),
            It.IsAny<CancellationToken>()), Times.Never);

        context.Verify(x => x.Publish(
            It.IsAny<PaymentFailedV1>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }


    [Fact]
    public async Task InfrastructureFailure_ShouldNotPublishPaymentFailed()
    {
        var message = NewMessage();
        var context = CreateContext(message);

        _service.Setup(x => x.ProcessPaymentCommandAsync(
                It.IsAny<CreatePaymentRequest>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException(
                "Simulated database timeout"));

        var consumer = new ProcessPaymentConsumer(_service.Object, NullLogger<ProcessPaymentConsumer>.Instance);

        await Assert.ThrowsAsync<TimeoutException>(
            () => consumer.Consume(context.Object));

        context.Verify(x => x.Publish(
            It.IsAny<PaymentFailedV1>(),
            It.IsAny<CancellationToken>()), Times.Never);

        context.Verify(x => x.Publish(
            It.IsAny<PaymentSucceededV1>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
