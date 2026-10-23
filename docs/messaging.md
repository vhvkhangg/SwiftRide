# RabbitMQ: từ tạo chuyến đến tìm tài xế

## Đọc code theo thứ tự

1. `src/TripService/Application/DTOs/CreateTripRequest.cs`: dữ liệu client gửi, gồm tọa độ điểm đón.
2. `src/TripService/Application/Services/TripApplicationService.cs`, hàm `CreateAsync`: kiểm tra dữ liệu, lưu chuyến, tạo `TripRequestedV1`, gọi `PublishAsync`.
3. `src/SwiftRide.Messaging/MessagePublisher.cs`: gọi MassTransit để gửi message đến RabbitMQ.
4. `src/SwiftRide.Messaging/MessagingTopology.cs`: exchange `swiftride-trip-requested-v1` được nối với queue `swiftride-matching-trip-requested`.
5. `src/MatchingService/Program.cs`: đăng ký `TripRequestedConsumer` với tên queue từ `MessagingEndpoints.MatchingTripRequested`. Đăng ký tự động này nhận retry chung.
6. `src/MatchingService/Application/Consumers/TripRequestedConsumer.cs`: nhận dữ liệu, gọi `MatchAsync` để tìm tài xế trong bán kính 5 km, ghi số tài xế tìm thấy vào log. MassTransit gửi ACK khi consumer hoàn thành.

Consumer hiện tìm ứng viên bằng nghiệp vụ Matching có sẵn; chưa chọn, giữ chỗ, tự chấp nhận tài xế hay phát event kết quả. Gọi lại consumer chỉ chạy truy vấn tìm kiếm, không trừ tiền hay thay đổi trạng thái chuyến.

## Chạy local

Cần .NET 10 SDK, Docker Desktop và `.env` hợp lệ. Compose hiện yêu cầu cả biến môi trường của Auth dù chỉ chọn chạy RabbitMQ.

```powershell
docker compose up -d rabbitmq trip-postgres matching-mongodb
```

Trip database phải có schema từ migrations hiện có của NV1. Nếu database chưa có bảng, chủ TripService cần áp dụng migrations trước; thay đổi này không tạo schema hoặc migration mới.

Đặt cấu hình cho chương trình Setup theo tài khoản thực tế. Ví dụ chỉ dành cho local với cấu hình mặc định:

```powershell
$env:RabbitMq__Host = "localhost"
$env:RabbitMq__Port = "5672"
$env:RabbitMq__VirtualHost = "swiftride"
$env:RabbitMq__Username = "swiftride"
$env:RabbitMq__Password = "swiftride_dev"
dotnet run --project src/SwiftRide.Messaging.Setup
```

Mở hai terminal từ gốc repository, mỗi terminal chạy một service. Các service đã có cấu hình RabbitMQ local trong `appsettings.json`; đặt biến môi trường nếu đã đổi tài khoản.

```powershell
dotnet run --project src/TripService --no-launch-profile --urls http://localhost:5101
```

```powershell
dotnet run --project src/MatchingService --no-launch-profile --urls http://localhost:5102
```

## Gửi một chuyến để quan sát

Gửi HTTP `POST http://localhost:5101/trips/` với JSON:

```json
{
  "riderId": "d60fcbf4-705c-4eec-a027-d9b0e1da72f3",
  "pickupAddress": "Bến Thành",
  "dropoffAddress": "Sân bay Tân Sơn Nhất",
  "fareEstimate": 100000,
  "pickupLatitude": 10.7769,
  "pickupLongitude": 106.7009
}
```

Thiếu hoặc sai tọa độ: HTTP 400, chưa lưu chuyến và chưa phát message. Tọa độ chỉ dùng trong request/message, không thêm cột database.

Thành công: HTTP 201 chứa ID chuyến. Terminal Matching ghi `Đã nhận chuyến ..., tìm thấy ... tài xế gần điểm đón`. Nếu chưa có dữ liệu tài xế, số lượng bằng 0 vẫn là xử lý thành công.

Mở http://localhost:15672, chọn vhost `swiftride`, vào Queues and Streams:

- Matching đang chạy: queue `swiftride-matching-trip-requested` có consumer; message thường được xử lý quá nhanh để thấy số Ready tăng.
- Dừng Matching rồi tạo chuyến: Ready tăng vì chưa có consumer nhận.
- Chạy Matching lại: message chờ được xử lý, Ready giảm.

Không bấm Purge hoặc Delete trên queue cần giữ dữ liệu.

## Retry và lỗi

- Lỗi tạm thời trong consumer: thử lại sau 1, 3, 5 giây, tối đa 4 lần xử lý.
- `ArgumentException`, bao gồm tọa độ sai: không retry, chuyển sang `<tên-queue>_error`.
- Loại message không có handler phù hợp: MassTransit giữ cơ chế `<tên-queue>_skipped` mặc định.
- Không nuốt lỗi database trong consumer: lỗi được đưa cho MassTransit xử lý retry.
- Database lưu thành công nhưng Publish lỗi hoặc bị hủy: API trả HTTP 503 với `tripId` đã lưu. Message có thể đã đến broker dù phía gửi báo lỗi. Tra cứu chuyến bằng ID; không tự gửi lại POST tạo chuyến.

Lưu database và Publish chưa có giao dịch chung. Nếu tiến trình chết giữa hai bước, event có thể không được gửi. Chưa có cơ chế tự khôi phục phát event; Outbox/Inbox và chống trùng nghiệp vụ vẫn thuộc chủ service. Không coi HTTP 503 là lý do tạo một chuyến mới.

## Kiểm thử

```powershell
dotnet test --project tests/TripService.Tests/TripService.Tests.csproj
dotnet test --project tests/MatchingService.Tests/MatchingService.Tests.csproj
```

`RabbitMqFlowTests` là test chạy chủ động, dùng RabbitMQ thật và repository giả. Nó chứng minh message từ `CreateAsync` đến consumer đúng queue, dữ liệu tọa độ/ID được giữ nguyên, và lỗi tạm thời hai lần được retry rồi thành công. Nó không chứng minh database thật hoặc API HTTP chạy được.

Ví dụ chạy test broker trong vhost riêng, sau khi build:

```powershell
docker exec swiftride-rabbitmq-1 rabbitmqctl add_vhost swiftride-nv6-test-flow
docker exec swiftride-rabbitmq-1 rabbitmqctl set_permissions -p swiftride-nv6-test-flow swiftride ".*" ".*" ".*"
$env:RabbitMq__Host = "localhost"
$env:RabbitMq__Port = "5672"
$env:RabbitMq__VirtualHost = "swiftride-nv6-test-flow"
$env:RabbitMq__Username = "swiftride"
$env:RabbitMq__Password = "swiftride_dev"
dotnet tests/TripService.Tests/bin/Debug/net10.0/TripService.Tests.dll -explicit only
docker exec swiftride-rabbitmq-1 rabbitmqctl delete_vhost swiftride-nv6-test-flow
$env:RabbitMq__VirtualHost = "swiftride"
```

Chỉ dùng vhost test riêng cho ví dụ này. Xóa vhost test sẽ xóa toàn bộ queue và message trong vhost đó.
