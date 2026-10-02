using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SwiftRide.PaymentService.Domain.Entities;

namespace SwiftRide.PaymentService.Infrastructure.Persistence.Configurations;

public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments");

        builder.HasKey(payment => payment.Id);

        builder.Property(payment => payment.Id)
            .HasColumnName("id");

        builder.Property(payment => payment.TripId)
            .HasColumnName("trip_id")
            .IsRequired();

        builder.Property(payment => payment.Amount)
            .HasColumnName("amount")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(payment => payment.IdempotencyKey)
            .HasColumnName("idempotency_key")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(payment => payment.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(payment => payment.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.HasIndex(payment => payment.IdempotencyKey)
            .IsUnique()
            .HasDatabaseName("ux_payments_idempotency_key");
        
        builder.HasIndex(payment => payment.TripId)
            .HasDatabaseName("ix_payments_trip_id");
    }
}