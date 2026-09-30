using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SwiftRide.PaymentService.Domain.Enums;

namespace SwiftRide.PaymentService.Domain.Entities
{
    public class LedgerEntry
    {
        public Guid Id { get; private set; }

        public Guid PaymentId { get; private set; }

        public LedgerEntryType Type { get; private set; }

        public string Account { get; private set; } = string.Empty;

        public decimal Amount { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }
        private LedgerEntry()
        {
        }

        private LedgerEntry(
            Guid id,
            Guid paymentId,
            LedgerEntryType type,
            string account,
            decimal amount,
            DateTimeOffset createdAt)
        {
            Id = id;
            PaymentId = paymentId;
            Type = type;
            Account = account;
            Amount = amount;
            CreatedAt = createdAt;
        }

        public static LedgerEntry Create(
            Guid paymentId,
            LedgerEntryType type,
            string account,
            decimal amount)
        {
            if (paymentId == Guid.Empty)
            {
                throw new ArgumentException(
                    "ID thanh toán không được để trống.",
                    nameof(paymentId)
                );
            }

            if (string.IsNullOrWhiteSpace(account))
            {
                throw new ArgumentException(
                    "Tài khoản sổ cái không được bỏ trống.",
                    nameof(account)
                );
            }

            if (amount <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(amount),
                    "Số tiền của bút toán phải lớn hơn 0."
                );
            }

            return new LedgerEntry(
                Guid.NewGuid(),
                paymentId,
                type,
                account,
                amount,
                DateTimeOffset.UtcNow);
        }
    }
}