using System;
using SwiftRide.TripService.Domain.Entities;
using SwiftRide.TripService.Domain.Enums;
using Xunit;

namespace SwiftRide.TripService.Tests;

public class TripEntityTests
{
    private Trip CreateTestTrip()
    {
        var trip = Trip.Create(Guid.NewGuid(), "A", "B", 100000m, 10.0, 106.0);
        return trip;
    }

    [Fact]
    public void TripState_LateEvent_WhenPaid_ThrowsIfMarkedFailed()
    {
        // Arrange: Đưa chuyến đi đến trạng thái Paid
        var trip = CreateTestTrip();
        trip.ConfirmPrice();
        trip.AcceptByDriver(Guid.NewGuid());
        trip.StartEnRoute();
        trip.Pickup();
        trip.Dropoff();
        trip.MarkPaymentPending();
        trip.MarkPaid(); // Lúc này Status là Paid

        // Act & Assert: Thử gọi hàm thất bại khi đã Paid (Kịch bản event đến muộn)
        var exception = Assert.Throws<InvalidOperationException>(() => trip.MarkFailed());
        Assert.Equal("Chuyến đi đã kết thúc thành công.", exception.Message);
    }

    [Fact]
    public void RecordPaymentFailure_WhenNotPending_ThrowsException()
    {
        var trip = CreateTestTrip(); // Đang ở Requested

        // Act & Assert: Thử ghi lỗi thanh toán khi chuyến chưa đến bước chờ thanh toán
        Assert.Throws<InvalidOperationException>(() => trip.RecordPaymentFailure("Lỗi ngân hàng"));
    }
}