# Kết quả chạy NV6 — 16/10/2026

## Khởi động

```powershell
docker compose up -d --build rabbitmq messaging-setup trip-service matching-service payment-service
```

Lệnh trên chạy RabbitMQ, ba service Trip/Matching/Payment và ba database phụ thuộc. ApiGateway/Auth không thuộc lần chạy NV6 này.

Dockerfile của bốn chương trình .NET đã ghim SDK `10.0.401`, khớp `global.json`. Image `sdk:10.0` lấy tại lần kiểm tra có file `/usr/share/dotnet/sdk/10.0.402/dotnet.runtimeconfig.json` rỗng, gây lỗi trước khi biên dịch. Image `10.0.401` chạy được và cả bốn Docker build đã thành công.

## Kết quả kiểm tra

- Build solution local: 0 lỗi, 0 cảnh báo.
- TripService.Tests: 9 test thường pass.
- MatchingService.Tests: 25 test pass.
- RabbitMqFlowTests: 1 test chủ động pass trên broker thật trong vhost test riêng; nhận đúng queue, đúng ID/tọa độ, lỗi tạm thời hai lần rồi thành công lần thứ ba. Repository database trong test là giả. Vhost test đã xóa sau khi chạy.
- Messaging.Setup: tạo exchange/queue/binding thành công và kết thúc với exit code 0. Setup không cần chạy liên tục.
- Ba endpoint `/health/ready` trên cổng 5101, 5102, 5103 trả HTTP 200.
- RabbitMQ và ba database healthy; Management UI ở http://localhost:15672/.
- Queue `swiftride-matching-trip-requested` có 1 consumer. Năm queue còn lại chưa có business consumer.
- Gửi một message smoke qua Management API đến exchange `swiftride-trip-requested-v1`: routed=true, consumer Docker ghi nhận ID `adb109ae-0c18-4dbc-a283-ad2404a11c24`, tìm thấy 0 tài xế. Queue sau xử lý có messages=0 và messages_unacknowledged=0.

## Giới hạn

- Smoke qua Management API kiểm tra exchange/binding/queue và consumer Docker thực sự truy vấn MongoDB. Nó không phải thao tác tạo chuyến qua HTTP.
- Trip và Payment database local hiện chưa có bảng. Chưa kiểm thử luồng nghiệp vụ HTTP hoàn chỉnh; schema/migrations do chủ service quản lý, không được thay đổi trong lần chạy NV6 này.
- Chưa có test hiện có để chạy cho fan-out hai queue, Send chỉ đến một queue, hết retry vào `_error`, `_skipped` và giữ message sau restart. Không coi lần chạy này là nghiệm thu đủ toàn bộ checklist NV6.
- Consumer Matching mới tìm ứng viên và ghi log; chưa phát kết quả hoặc chọn tài xế. Outbox/Inbox chưa được triển khai.

Hướng dẫn đọc code và gửi request mẫu: [messaging.md](messaging.md).
