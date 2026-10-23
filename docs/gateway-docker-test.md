# Test Gateway Auth với PostgreSQL

Dùng duy nhất docker-compose.yml ở thư mục gốc cho toàn bộ backend. Không cần .env khi test local: Compose có giá trị mặc định cho password và JWT key, chỉ dành cho development/test. Có thể override bằng .env hoặc environment variables.

Chạy tại D:SwiftRide với Docker Desktop Linux containers:

~~~powershell
docker compose up --build -d
docker compose logs -f api-gateway
~~~

Chờ Gateway báo listening rồi kiểm tra bằng PowerShell 7:

~~~powershell
Invoke-RestMethod http://localhost:5100/health
pwsh -File tests/ApiGateway.Smoke/auth-smoke.ps1
~~~

Smoke test kiểm tra register, duplicate email, login, refresh rotation, replay/family revocation, concurrent refresh, logout và JWT bắt buộc. Nếu chạy lại ngay bị 429, chờ ít nhất một phút.

PostgreSQL Auth:
- Host: localhost
- Port: 5435
- Database: swiftride_auth
- Username: swiftride
- Password mặc định: gateway_test_password (hoặc AUTH_DB_PASSWORD nếu đã override)

Gateway: http://localhost:5100. Trip/Matching/Payment chạy cùng stack, Gateway định tuyến theo service names trong Docker. Products/Orders vẫn là route mẫu, chưa có service trong stack. Để test forwarding với JWT hợp lệ, gọi một endpoint /api/trips/* đã được triển khai trong TripService; Ocelot giữ nguyên đường dẫn này khi proxy.

Dừng toàn bộ backend và giữ dữ liệu:

~~~powershell
docker compose down
~~~

Không cần down -v để chạy test; lệnh đó sẽ xóa dữ liệu tất cả database của stack. EnsureCreated chỉ tạo schema mới, không nâng cấp schema cũ.

Nếu trước đây đã chạy stack swiftride-gateway-test riêng, cần dừng các containers cũ trước vì trùng cổng 5100/5435. Volume cũ không được chuyển hoặc xóa tự động.

Production phải cung cấp secrets riêng, tắt EnsureCreated, dùng migrations và cấu hình mạng/TLS phù hợp. Cấu hình Compose đã được kiểm tra cú pháp; chưa xác nhận toàn bộ containers chạy thành công.
