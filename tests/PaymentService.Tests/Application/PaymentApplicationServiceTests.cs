using Moq;
using SwiftRide.PaymentService.Application.DTOs;
using SwiftRide.PaymentService.Application.Integrations;
using SwiftRide.PaymentService.Application.Services;
using SwiftRide.PaymentService.Domain.Entities;
using SwiftRide.PaymentService.Domain.Enums;
using SwiftRide.PaymentService.Domain.Repositories;
using Xunit;

namespace SwiftRide.PaymentService.Tests.Application;

public sealed class PaymentApplicationServiceTests
{
    private readonly Mock<IPaymentRepository> _repositoryMock;
    private readonly PaymentApplicationService _service;
    private readonly Mock<ITripServiceClient> _tripServiceClientMock = new();

    public PaymentApplicationServiceTests()
    {
        _repositoryMock = new Mock<IPaymentRepository>();

        _service = new PaymentApplicationService(
            _repositoryMock.Object,
            _tripServiceClientMock.Object
        );
    }

    [Fact]
    public async Task CreatePaymentAsync_WithNewKey_ShouldCreatePaymentAndLedger()
    {
        // Arrange
        var tripId = Guid.NewGuid();
        var riderId = Guid.NewGuid();
        var driverId = Guid.NewGuid();

        var payerAccount = $"rider-{riderId:D}";
        var payeeAccount = $"driver-{driverId:D}";

        var request = new CreatePaymentRequest(
            tripId,
            100_000m,
            "payment-001",
            payerAccount,
            payeeAccount);

        _repositoryMock
            .Setup(repository =>
                repository.GetByIdempotencyKeyAsync(
                    request.IdempotencyKey,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync((Payment?)null);

        _repositoryMock
            .Setup(repository =>
                repository.AddAsync(
                    It.IsAny<Payment>(),
                    It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _repositoryMock
            .Setup(repository =>
                repository.AddLedgerEntriesAsync(
                    It.IsAny<IEnumerable<LedgerEntry>>(),
                    It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _repositoryMock
            .Setup(repository =>
                repository.SaveChangesAsync(
                    It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _tripServiceClientMock
            .Setup(client => client.GetByIdAsync(
                tripId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TripSnapshot(
                tripId,
                riderId,
                driverId,
                100_000m,
                TripStatusCode.PaymentPending));

        _tripServiceClientMock
            .Setup(client => client.MarkPaidAsync(
                tripId,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result =
            await _service.CreatePaymentAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(PaymentStatus.Succeeded.ToString(), result.Status);
        Assert.Equal(request.Amount, result.Amount);
        Assert.Equal(request.TripId, result.TripId);

        _repositoryMock.Verify(
            repository =>
                repository.AddAsync(
                    It.Is<Payment>(
                        payment =>
                            payment.Status ==
                            PaymentStatus.Succeeded),
                    It.IsAny<CancellationToken>()),
            Times.Once);

        _repositoryMock.Verify(
            repository =>
                repository.AddLedgerEntriesAsync(
                    It.Is<IEnumerable<LedgerEntry>>(
                        entries =>
                            entries.Count() == 2 &&
                            entries.Any(
                                entry =>
                                    entry.Type ==
                                    LedgerEntryType.Debit &&
                                    entry.Account ==
                                    payerAccount) &&
                            entries.Any(
                                entry =>
                                    entry.Type ==
                                    LedgerEntryType.Credit &&
                                    entry.Account ==
                                    payeeAccount)),
                    It.IsAny<CancellationToken>()),
            Times.Once);

        _repositoryMock.Verify(
            repository =>
                repository.SaveChangesAsync(
                    It.IsAny<CancellationToken>()),
            Times.Once);

        _tripServiceClientMock.Verify(
            client => client.MarkPaidAsync(
                tripId,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreatePaymentAsync_WithExistingKey_ShouldReturnExistingPayment()
    {
        var existingPayment = Payment.Create(
            Guid.NewGuid(),
            100_000m,
            "payment-existing");

        existingPayment.MarkSucceeded();

        var request = new CreatePaymentRequest(
            existingPayment.TripId,
            existingPayment.Amount,
            existingPayment.IdempotencyKey,
            "rider-001",
            "driver-001");

        _repositoryMock
            .Setup(repository =>
                repository.GetByIdempotencyKeyAsync(
                    request.IdempotencyKey,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPayment);

        var result =
            await _service.CreatePaymentAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(existingPayment.Id, result.Id);

        _repositoryMock.Verify(
            repository =>
                repository.AddAsync(
                    It.IsAny<Payment>(),
                    It.IsAny<CancellationToken>()),
            Times.Never);

        _repositoryMock.Verify(
            repository =>
                repository.AddLedgerEntriesAsync(
                    It.IsAny<IEnumerable<LedgerEntry>>(),
                    It.IsAny<CancellationToken>()),
            Times.Never);

        _repositoryMock.Verify(
            repository =>
                repository.SaveChangesAsync(
                    It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RefundPaymentAsync_WithSucceededPayment_ShouldReverseLedger()
    {
        var payment = Payment.Create(
            Guid.NewGuid(),
            100_000m,
            "payment-refund");

        payment.MarkSucceeded();

        var originalEntries = new[]
        {
        LedgerEntry.Create(
            payment.Id,
            LedgerEntryType.Debit,
            "rider-001",
            100_000m),

        LedgerEntry.Create(
            payment.Id,
            LedgerEntryType.Credit,
            "driver-001",
            100_000m)
    };

        _repositoryMock
            .Setup(repository =>
                repository.GetByIdAsync(
                    payment.Id,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);

        _repositoryMock
            .Setup(repository =>
                repository.GetLedgerEntriesByPaymentIdAsync(
                    payment.Id,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(originalEntries);

        _repositoryMock
            .Setup(repository =>
                repository.AddLedgerEntriesAsync(
                    It.IsAny<IEnumerable<LedgerEntry>>(),
                    It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _repositoryMock
            .Setup(repository =>
                repository.SaveChangesAsync(
                    It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result =
            await _service.RefundPaymentAsync(payment.Id, TestContext.Current.CancellationToken);

        Assert.Equal(
            PaymentStatus.Refunded.ToString(),
            result.Status);

        _repositoryMock.Verify(
            repository =>
                repository.AddLedgerEntriesAsync(
                    It.Is<IEnumerable<LedgerEntry>>(
                        entries =>
                            entries.Any(
                                entry =>
                                    entry.Account == "rider-001" &&
                                    entry.Type ==
                                    LedgerEntryType.Credit) &&
                            entries.Any(
                                entry =>
                                    entry.Account == "driver-001" &&
                                    entry.Type ==
                                    LedgerEntryType.Debit)),
                    It.IsAny<CancellationToken>()),
            Times.Once);

        _repositoryMock.Verify(
            repository =>
                repository.SaveChangesAsync(
                    It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RefundPaymentAsync_WhenPaymentDoesNotExist_ShouldThrowKeyNotFoundException()
    {
        var paymentId = Guid.NewGuid();

        _repositoryMock
            .Setup(repository =>
                repository.GetByIdAsync(
                    paymentId,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync((Payment?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.RefundPaymentAsync(paymentId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RefundPaymentAsync_WhenPaymentIsPending_ShouldThrowInvalidOperationException()
    {
        var payment = Payment.Create(
            Guid.NewGuid(),
            100_000m,
            "payment-pending");

        var entries = new[]
        {
        LedgerEntry.Create(
            payment.Id,
            LedgerEntryType.Debit,
            "rider-001",
            100_000m)
    };

        _repositoryMock
            .Setup(repository =>
                repository.GetByIdAsync(
                    payment.Id,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);

        _repositoryMock
            .Setup(repository =>
                repository.GetLedgerEntriesByPaymentIdAsync(
                    payment.Id,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.RefundPaymentAsync(payment.Id, TestContext.Current.CancellationToken));
    }
}