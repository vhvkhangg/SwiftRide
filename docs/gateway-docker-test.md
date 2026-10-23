# Test Gateway trên Docker

<<<<<<< HEAD
Dùng duy nhất docker-compose.yml ở thư mục gốc cho toàn bộ backend. Không cần .env khi test local: Compose có giá trị mặc định cho password và JWT key, chỉ dành cho development/test. Có thể override bằng .env hoặc environment variables.

Chạy tại D:SwiftRide với Docker Desktop Linux containers:
=======
Dùng duy nhất docker-compose.yml cho toàn bộ backend. Cần Docker Desktop chạy Linux containers.

1. Copy .env.example thành .env.
2. Điền AUTH_DB_PASSWORD và JWT_SECRET_KEY. Có thể sinh key trong PowerShell 7:
   [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(64))
3. Đặt SERVICE_AUTH_SECRET_KEY khác JWT_SECRET_KEY (Compose có fallback local chỉ dành test).
4. Chạy từ thư mục D:/SwiftRide:
>>>>>>> van

~~~powershell
docker compose up --build -d
docker compose logs -f api-gateway
~~~

<<<<<<< HEAD
Chờ Gateway báo listening rồi kiểm tra bằng PowerShell 7:
=======
Chờ các service khởi động/migrate xong:
>>>>>>> van

~~~powershell
Invoke-RestMethod http://localhost:5100/health
pwsh -File tests/Gateway.Security/gateway-security.ps1
~~~

Tài khoản demo:
- rider.demo@example.com
- driver.demo@example.com
- driver.other@example.com

Mật khẩu mặc định: SwiftRide-demo-2026!; override bằng DEV_ACCOUNT_PASSWORD và tham số -SeedPassword của test. Seed chỉ tạo user chưa tồn tại, không reset tài khoản cũ.

Test rotation/logout riêng (chờ ít nhất một phút sau suite trước để không vượt auth rate limit):

~~~powershell
pwsh -File tests/ApiGateway.Smoke/auth-smoke.ps1
~~~

<<<<<<< HEAD
Smoke test kiểm tra register, duplicate email, login, refresh rotation, replay/family revocation, concurrent refresh, logout và JWT bắt buộc. Nếu chạy lại ngay bị 429, chờ ít nhất một phút.

PostgreSQL Auth:
- Host: localhost
- Port: 5435
- Database: swiftride_auth
- Username: swiftride
- Password mặc định: gateway_test_password (hoặc AUTH_DB_PASSWORD nếu đã override)

Gateway: http://localhost:5100. Trip/Matching/Payment chạy cùng stack, Gateway định tuyến theo service names trong Docker. Products/Orders vẫn là route mẫu, chưa có service trong stack. Để test forwarding với JWT hợp lệ, gọi một endpoint /api/trips/* đã được triển khai trong TripService; Ocelot giữ nguyên đường dẫn này khi proxy.

Dừng toàn bộ backend và giữ dữ liệu:

=======
AuthDb: localhost:5435, database swiftride_auth, username swiftride, password theo AUTH_DB_PASSWORD. JWT và service keys không dùng lại ở production.

Dừng containers và giữ dữ liệu:

>>>>>>> van
~~~powershell
docker compose down
~~~

<<<<<<< HEAD
Không cần down -v để chạy test; lệnh đó sẽ xóa dữ liệu tất cả database của stack. EnsureCreated chỉ tạo schema mới, không nâng cấp schema cũ.

Nếu trước đây đã chạy stack swiftride-gateway-test riêng, cần dừng các containers cũ trước vì trùng cổng 5100/5435. Volume cũ không được chuyển hoặc xóa tự động.

Production phải cung cấp secrets riêng, tắt EnsureCreated, dùng migrations và cấu hình mạng/TLS phù hợp. Cấu hình Compose đã được kiểm tra cú pháp; chưa xác nhận toàn bộ containers chạy thành công.
=======
Không chạy down -v nếu muốn giữ dữ liệu. Trip/Payment dùng migrations, Auth dùng EnsureCreated cho database mới; không cần xóa toàn bộ database để thêm bảng invitation.

Routes và thiết kế quyền chi tiết ở gateway-auth.md. Products/Orders là route mẫu, chưa có service triển khai.
>>>>>>> van
