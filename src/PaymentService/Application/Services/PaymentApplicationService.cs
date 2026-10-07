using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualBasic;
using SwiftRide.PaymentService.Application.DTOs;
using SwiftRide.PaymentService.Domain.Entities;
using SwiftRide.PaymentService.Domain.Enums;
using SwiftRide.PaymentService.Domain.Repositories;

namespace SwiftRide.PaymentService.Application.Services
{
    public sealed class PaymentApplicationService
        : IPaymentApplicationService
    {
        private readonly IPaymentRepository _paymentRepository;

        public PaymentApplicationService(
            IPaymentRepository paymentRepository)
        {
            _paymentRepository = paymentRepository;
        }

        public async Task<PaymentResponse> CreatePaymentAsync(
            CreatePaymentRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            var existingPayment =
                await _paymentRepository.GetByIdempotencyKeyAsync(
                    request.IdempotencyKey,
                    cancellationToken
                );

            if (existingPayment is not null)
            {
                return PaymentResponse.From(existingPayment);
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