using Moq;
using SwiftRide.PaymentService.Application.DTOs;
using SwiftRide.PaymentService.Application.Integrations;
using SwiftRide.PaymentService.Application.Services;
using SwiftRide.PaymentService.Domain.Entities;
using SwiftRide.PaymentService.Domain.Enums;
using SwiftRide.PaymentService.Domain.Exceptions;
using SwiftRide.PaymentService.Domain.Repositories;
using Xunit;

namespace SwiftRide.PaymentService.Tests.Application;

public sealed class PaymentApplicationServiceTests
{
    private readonly Mock<IPaymentRepository> _repo = new();
    private readonly Mock<ITripServiceClient> _trip = new();
    private readonly PaymentApplicationService _service;

    public PaymentApplicationServiceTests()
    {
        _service = new PaymentApplicationService(_repo.Object, _trip.Object);
        _repo.Setup(x => x.AddAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _repo.Setup(x => x.AddLedgerEntriesAsync(
                It.IsAny<IEnumerable<LedgerEntry>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _repo.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private static (CreatePaymentRequest request, TripSnapshot trip) NewRequest()
    {
        var tripId = Guid.NewGuid();
        var rider = Guid.NewGuid();
        var driver = Guid.NewGuid();
        return (new CreatePaymentRequest(tripId, 100_000m, Guid.NewGuid().ToString("N"),
                $"rider-{rider:D}", $"driver-{driver:D}"),
            new TripSnapshot(tripId, rider, driver, 100_000m, TripStatusCode.PaymentPending));
    }

    [Fact]
    public async Task Create_NewPayment_ShouldCommitLedgerThenSyncTrip()
    {
        var (request, snapshot) = NewRequest();
        _trip.Setup(x => x.GetByIdAsync(request.TripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);
        _trip.Setup(x => x.MarkPaidAsync(request.TripId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _service.CreatePaymentAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(PaymentStatus.Succeeded.ToString(), result.Status);
        Assert.True(result.IsTripSynced);
        _repo.Verify(x => x.AddAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()), Times.Once);
        _repo.Verify(x => x.AddLedgerEntriesAsync(
            It.Is<IEnumerable<LedgerEntry>>(e => e.Count() == 2 &&
                e.Any(x => x.Account == request.PayerAccount && x.Type == LedgerEntryType.Debit) &&
                e.Any(x => x.Account == request.PayeeAccount && x.Type == LedgerEntryType.Credit)),
            It.IsAny<CancellationToken>()), Times.Once);
        _repo.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        _trip.Verify(x => x.MarkPaidAsync(request.TripId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExistingSyncedPayment_ShouldNotInsertOrCallback()
    {
        var (request, _) = NewRequest();
        var existing = Payment.Create(request.TripId, request.Amount, request.IdempotencyKey,
            request.PayerAccount, request.PayeeAccount);
        existing.MarkSucceeded();
        existing.MarkTripSynced();
        _repo.Setup(x => x.GetByIdempotencyKeyAsync(request.IdempotencyKey,
            It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        var response = await _service.CreatePaymentAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(existing.Id, response.Id);
        _repo.Verify(x => x.AddAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()), Times.Never);
        _repo.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _trip.Verify(x => x.MarkPaidAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExistingKeyDifferentPayer_ShouldThrow()
    {
        var (request, _) = NewRequest();
        var existing = Payment.Create(request.TripId, request.Amount, request.IdempotencyKey,
            "some-other-payer", request.PayeeAccount);
        existing.MarkSucceeded();
        _repo.Setup(x => x.GetByIdempotencyKeyAsync(request.IdempotencyKey,
            It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CreatePaymentAsync(request, TestContext.Current.CancellationToken));
        _repo.Verify(x => x.AddAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CallbackFails_ThenRetry_ShouldNotAddLedgerTwice()
    {
        var (request, snapshot) = NewRequest();
        Payment? persisted = null;
        _repo.Setup(x => x.GetByIdempotencyKeyAsync(request.IdempotencyKey,
            It.IsAny<CancellationToken>())).ReturnsAsync(() => persisted);
        _repo.Setup(x => x.AddAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()))
            .Callback<Payment, CancellationToken>((p, _) => persisted = p)
            .Returns(Task.CompletedTask);
        _trip.Setup(x => x.GetByIdAsync(request.TripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);
        _trip.SetupSequence(x => x.MarkPaidAsync(request.TripId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Simulated callback failure"))
            .Returns(Task.CompletedTask);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            _service.CreatePaymentAsync(request, TestContext.Current.CancellationToken));
        var saved = Assert.IsType<Payment>(persisted);
        Assert.False(saved.IsTripSynced);
        var retry = await _service.CreatePaymentAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(saved.Id, retry.Id);
        Assert.True(retry.IsTripSynced);
        _repo.Verify(x => x.AddAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()), Times.Once);
        _repo.Verify(x => x.AddLedgerEntriesAsync(
            It.IsAny<IEnumerable<LedgerEntry>>(), It.IsAny<CancellationToken>()), Times.Once);
        _repo.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        _trip.Verify(x => x.MarkPaidAsync(request.TripId, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task DuplicateInsert_WhenOtherRequestWins_ShouldReloadSamePayment()
    {
        var (request, snapshot) = NewRequest();
        var winner = Payment.Create(request.TripId, request.Amount, request.IdempotencyKey,
            request.PayerAccount, request.PayeeAccount);
        winner.MarkSucceeded();
        _repo.SetupSequence(x => x.GetByIdempotencyKeyAsync(request.IdempotencyKey,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Payment?)null)
            .ReturnsAsync(winner);
        _trip.Setup(x => x.GetByIdAsync(request.TripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);
        _trip.Setup(x => x.MarkPaidAsync(request.TripId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _repo.SetupSequence(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DuplicateIdempotencyKeyException(new Exception("23505")))
            .Returns(Task.CompletedTask);

        var result = await _service.CreatePaymentAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(winner.Id, result.Id);
        Assert.True(result.IsTripSynced);
        _repo.Verify(x => x.AddLedgerEntriesAsync(
            It.IsAny<IEnumerable<LedgerEntry>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Refund_SucceededPayment_ShouldAppendReversals()
    {
        var payment = Payment.Create(Guid.NewGuid(), 100_000m, "refund-key", "rider-001", "driver-001");
        payment.MarkSucceeded();
        var entries = new[]
        {
            LedgerEntry.Create(payment.Id, LedgerEntryType.Debit, "rider-001", 100_000m),
            LedgerEntry.Create(payment.Id, LedgerEntryType.Credit, "driver-001", 100_000m)
        };
        _repo.Setup(x => x.GetByIdAsync(payment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);
        _repo.Setup(x => x.GetLedgerEntriesByPaymentIdAsync(payment.Id,
            It.IsAny<CancellationToken>())).ReturnsAsync(entries);

        var result = await _service.RefundPaymentAsync(payment.Id, TestContext.Current.CancellationToken);
        Assert.Equal(PaymentStatus.Refunded.ToString(), result.Status);
        _repo.Verify(x => x.AddLedgerEntriesAsync(
            It.Is<IEnumerable<LedgerEntry>>(e => e.Count() == 2 &&
                e.Any(x => x.Type == LedgerEntryType.Credit && x.Account == "rider-001") &&
                e.Any(x => x.Type == LedgerEntryType.Debit && x.Account == "driver-001")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Reconcile_SucceededUnsynced_ShouldMarkSynced()
    {
        var p = Payment.Create(Guid.NewGuid(), 100_000m, "reconcile-key", "rider-001", "driver-001");
        p.MarkSucceeded();
        _repo.Setup(x => x.GetByIdAsync(p.Id, It.IsAny<CancellationToken>())).ReturnsAsync(p);
        _trip.Setup(x => x.MarkPaidAsync(p.TripId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _service.ReconcilePaymentAsync(p.Id, TestContext.Current.CancellationToken);
        Assert.True(result.IsTripSynced);
        _repo.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Refund_MissingPayment_ShouldThrow()
    {
        var id = Guid.NewGuid();
        _repo.Setup(x => x.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Payment?)null);
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.RefundPaymentAsync(id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetById_ExistingPayment_ShouldReturn()
    {
        var p = Payment.Create(Guid.NewGuid(), 100_000m, "get-key", "rider-001", "driver-001");
        _repo.Setup(x => x.GetByIdAsync(p.Id, It.IsAny<CancellationToken>())).ReturnsAsync(p);
        var result = await _service.GetPaymentByIdAsync(p.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal(p.Id, result.Id);
    }

    [Fact]
    public async Task Refund_PendingPayment_ShouldThrow()
    {
        var p = Payment.Create(Guid.NewGuid(), 100_000m, "pending", "rider-001", "driver-001");
        _repo.Setup(x => x.GetByIdAsync(p.Id, It.IsAny<CancellationToken>())).ReturnsAsync(p);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.RefundPaymentAsync(p.Id, TestContext.Current.CancellationToken));
    }
}
