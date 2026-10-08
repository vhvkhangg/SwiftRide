using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualBasic;
using SwiftRide.PaymentService.Application.DTOs;
using SwiftRide.PaymentService.Domain.Entities;
using SwiftRide.PaymentService.Domain.Enums;
using SwiftRide.PaymentService.Domain.Repositories;
using SwiftRide.PaymentService.Application.Integrations;

namespace SwiftRide.PaymentService.Application.Services
{
    public sealed class PaymentApplicationService
        : IPaymentApplicationService
    {
        private readonly IPaymentRepository _paymentRepository;
        private readonly ITripServiceClient _tripServiceClient;

        public PaymentApplicationService(
            IPaymentRepository paymentRepository,
            ITripServiceClient tripServiceClient
        )
        {
            _paymentRepository = paymentRepository;
            _tripServiceClient = tripServiceClient;
        }

        public async Task<PaymentResponse> CreatePaymentAsync(
            CreatePaymentRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (request.TripId == Guid.Empty)
            {
                throw new ArgumentException("ID chuyến đi không được để trống.");
            }

            if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
            {
                throw new ArgumentException("IdempotencyKey không được để trống.");
            }

            var existingPayment =
                await _paymentRepository.GetByIdempotencyKeyAsync(
                    request.IdempotencyKey,
                    cancellationToken
                );

            if (existingPayment is not null)
            {
                if (existingPayment.TripId != request.TripId || existingPayment.Amount != request.Amount)
                {
                    throw new InvalidOperationException("IdempotencyKey đã được sử dụng cho giao dịch khác.");
                }

                if (existingPayment.Status == PaymentStatus.Succeeded)
                {
                    await _tripServiceClient.MarkPaidAsync(
                        existingPayment.TripId,
                        cancellationToken
                    );
                }

                return PaymentResponse.From(existingPayment);
            }

            var trip = await _tripServiceClient.GetByIdAsync(
                request.TripId,
                cancellationToken
            );

            if (trip is null)
            {
                throw new KeyNotFoundException($"Không tìm thấy chuyến đi '{request.TripId}'.");
            }

            if (trip.Status != TripStatusCode.PaymentPending)
            {
                throw new InvalidOperationException($"Chuyến đi chưa đủ điều kiện thanh toán: {trip.Status}.");
            }

            if (trip.DriverId is null || trip.DriverId == Guid.Empty)
            {
                throw new InvalidOperationException(
                    "Chuyến đi chưa có tài xế hợp lệ.");
            }

            if (trip.RiderId == Guid.Empty)
            {
                throw new InvalidOperationException(
                    "Chuyến đi chưa có hành khách hợp lệ.");
            }

            if (trip.FareEstimate is null || trip.FareEstimate <= 0)
            {
                throw new InvalidOperationException(
                    "Chuyến đi chưa có số tiền hợp lệ.");
            }

            if (request.Amount != trip.FareEstimate.Value)
            {

                throw new InvalidOperationException(
                    "Số tiền thanh toán không khớp với TripService.");
            }

            var payerAccount = $"rider-{trip.RiderId:D}";
            var payeeAccount = $"driver-{trip.DriverId.Value:D}";

            if (!string.Equals(request.PayerAccount, payerAccount, StringComparison.Ordinal) ||
            !string.Equals(
                        request.PayeeAccount,
                        payeeAccount,
                        StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Tài khoản thanh toán không khớp với chuyến đi.");
            }
            var payment = Payment.Create(
                request.TripId,
                request.Amount,
                request.IdempotencyKey
            );

            var debitEntry = LedgerEntry.Create(
                payment.Id,
                LedgerEntryType.Debit,
                request.PayerAccount,
                payment.Amount
            );

            var creditEntry = LedgerEntry.Create(
                payment.Id,
                LedgerEntryType.Credit,
                request.PayeeAccount,
                payment.Amount
            );

            payment.MarkSucceeded();

            await _paymentRepository.AddAsync(
                payment,
                cancellationToken
            );

            await _paymentRepository.AddLedgerEntriesAsync(
                new[]
                {
                    debitEntry,
                    creditEntry
                },
                cancellationToken
            );

            await _paymentRepository.SaveChangesAsync(
                cancellationToken
            );

            await _tripServiceClient.MarkPaidAsync(
                trip.Id,
                cancellationToken
            );

            return PaymentResponse.From(payment);
        }

        public async Task<PaymentResponse?> GetPaymentByIdAsync(
    Guid paymentId,
    CancellationToken cancellationToken = default)
        {
            var payment =
                await _paymentRepository.GetByIdAsync(
                    paymentId,
                    cancellationToken
            );

            return payment is null
                ? null
                : PaymentResponse.From(payment);
        }

        public async Task<RefundPaymentResponse> RefundPaymentAsync(
            Guid paymentId,
            CancellationToken cancellationToken = default
        )
        {
            var payment =
                await _paymentRepository.GetByIdAsync(
            paymentId,
            cancellationToken);

            if (payment is null)
            {
                throw new KeyNotFoundException(
                    $"Không tìm thấy thanh toán '{paymentId}'.");
            }

            var originalEntries = await _paymentRepository.GetLedgerEntriesByPaymentIdAsync(
                paymentId,
                cancellationToken
            );

            if (originalEntries.Count == 0)
            {
                throw new InvalidOperationException(
                "Không tìm thấy bút toán của giao dịch.");
            }

            payment.Refund();

            var reversalEntries = originalEntries
                .Select(entry =>
                    LedgerEntry.Create(
                        payment.Id,
                        Reverse(entry.Type),
                        entry.Account,
                        entry.Amount
                    )
                )
                .ToArray();

            await _paymentRepository.AddLedgerEntriesAsync(
                reversalEntries,
                cancellationToken
            );

            await _paymentRepository.SaveChangesAsync(
                cancellationToken);

            return RefundPaymentResponse.From(payment);
        }

        private static LedgerEntryType Reverse(
            LedgerEntryType type
        )
        {
            return type switch
            {
                LedgerEntryType.Debit => LedgerEntryType.Credit,
                LedgerEntryType.Credit => LedgerEntryType.Debit,
                _ => throw new ArgumentOutOfRangeException(nameof(type))
            };
        }
    }
}