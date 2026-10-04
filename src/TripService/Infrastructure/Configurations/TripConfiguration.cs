using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SwiftRide.TripService.Domain.Entities;

namespace SwiftRide.TripService.Infrastructure.Configurations;

public class TripConfiguration : IEntityTypeConfiguration<Trip>
{
    public void Configure(EntityTypeBuilder<Trip> builder)
    {
        // Đặt khóa chính
        builder.HasKey(t => t.Id);

        // Map enum Status thành chuỗi 
        builder.Property(t => t.Status)
            .HasConversion<string>()
            .HasMaxLength(50);

        // Ràng buộc chiều dài các trường địa chỉ
        builder.Property(t => t.PickupAddress)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(t => t.DropoffAddress)
            .HasMaxLength(500)
            .IsRequired();

        // Bỏ qua biến _state
        builder.Ignore("_state");
    }
}
