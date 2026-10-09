using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SwiftRide.PaymentService.Domain.Entities;

namespace SwiftRide.PaymentService.Infrastructure.Persistence.Configurations;

public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TripId).HasColumnName("trip_id").IsRequired();
        builder.Property(x => x.Amount).HasColumnName("amount").HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status")
            .HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(x => x.IdempotencyKey).HasColumnName("idempotency_key")
            .HasMaxLength(128).IsRequired();
        builder.Property(x => x.PayerAccount).HasColumnName("payer_account")
            .HasMaxLength(128).IsRequired();
        builder.Property(x => x.PayeeAccount).HasColumnName("payee_account")
            .HasMaxLength(128).IsRequired();
        builder.Property(x => x.IsTripSynced).HasColumnName("is_trip_synced")
            .HasDefaultValue(false).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.HasIndex(x => x.IdempotencyKey).IsUnique()
            .HasDatabaseName("ux_payments_idempotency_key");
        builder.HasIndex(x => x.TripId).HasDatabaseName("ix_payments_trip_id");
    }
}
