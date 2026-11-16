using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SwiftRide.TripService.Application.Sagas;

namespace SwiftRide.TripService.Infrastructure.Configurations;

/// <summary>Bảng TripPaymentSagas trong TripDB, dùng chung DbContext với Trip và Outbox.</summary>
public sealed class TripPaymentSagaMap : SagaClassMap<TripPaymentSagaState>
{
    protected override void Configure(EntityTypeBuilder<TripPaymentSagaState> entity, ModelBuilder model)
    {
        entity.ToTable("TripPaymentSagas");
        entity.Property(x => x.CurrentState).HasMaxLength(64).IsRequired();
        entity.Property(x => x.Amount).HasPrecision(18, 2);
        entity.Property(x => x.Currency).HasMaxLength(3);
        entity.Property(x => x.IdempotencyKey).HasMaxLength(128);
        entity.Property(x => x.PayerAccount).HasMaxLength(64);
        entity.Property(x => x.PayeeAccount).HasMaxLength(64);
        entity.Property(x => x.PaymentMethod).HasMaxLength(16);
        entity.Property(x => x.FailureReason).HasMaxLength(256);
        // uint + IsRowVersion ⇒ Npgsql ánh xạ vào cột hệ thống xmin (optimistic concurrency).
        entity.Property(x => x.RowVersion).IsRowVersion();
        // Worker timeout quét theo (trạng thái, hạn chót).
        entity.HasIndex(x => new { x.CurrentState, x.DeadlineUtc });
    }
}
