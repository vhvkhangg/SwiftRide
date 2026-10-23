# Test Gateway trên Docker

Dùng duy nhất docker-compose.yml cho toàn bộ backend. Cần Docker Desktop chạy Linux containers.

1. Copy .env.example thành .env.
2. Điền AUTH_DB_PASSWORD và JWT_SECRET_KEY. Có thể sinh key trong PowerShell 7:
   [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(64))
3. Đặt SERVICE_AUTH_SECRET_KEY khác JWT_SECRET_KEY (Compose có fallback local chỉ dành test).
4. Chạy từ thư mục gốc repository (ví dụ E:/SwiftRide):

~~~powershell
docker compose up --build -d
docker compose logs -f api-gateway
~~~

Chờ các service khởi động/migrate xong:

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

AuthDb: localhost:5435, database swiftride_auth, username swiftride, password theo AUTH_DB_PASSWORD. JWT và service keys không dùng lại ở production.

Dừng containers và giữ dữ liệu:

~~~powershell
docker compose down
~~~

Không chạy down -v nếu muốn giữ dữ liệu. Trip/Payment dùng migrations, Auth dùng EnsureCreated cho database mới; không cần xóa toàn bộ database để thêm bảng invitation.

Routes và thiết kế quyền chi tiết ở gateway-auth.md. Products/Orders là route mẫu, chưa có service triển khai.
