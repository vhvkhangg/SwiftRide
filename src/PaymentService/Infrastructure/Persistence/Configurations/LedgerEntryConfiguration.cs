using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SwiftRide.PaymentService.Domain.Entities;

namespace SwiftRide.PaymentService.Infrastructure.Persistence.Configurations;

public sealed class LedgerEntryConfiguration
    : IEntityTypeConfiguration<LedgerEntry>
{
    public void Configure(EntityTypeBuilder<LedgerEntry> builder)
    {
        builder.ToTable("ledger_entries");

        builder.HasKey(entry => entry.Id);

        builder.Property(entry => entry.Id)
            .HasColumnName("id");

        builder.Property(entry => entry.PaymentId)
            .HasColumnName("payment_id")
            .IsRequired();

        builder.Property(entry => entry.Type)
            .HasColumnName("type")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(entry => entry.Account)
            .HasColumnName("account")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(entry => entry.Amount)
            .HasColumnName("amount")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(entry => entry.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.HasOne<Payment>()
            .WithMany()
            .HasForeignKey(entry => entry.PaymentId)
            .OnDelete(DeleteBehavior.Restrict);
        
        builder.HasIndex(entry => entry.PaymentId)
            .HasDatabaseName("ix_ledger_entries_payment_id");
    }
}