# Test Gateway Auth với PostgreSQL

File độc lập: docker-compose.gateway-test.yml ở thư mục gốc. Không cần file .env; credentials và JWT key công khai trong file chỉ dành cho test local. Chạy lệnh tại D:SwiftRide. Cần Docker Desktop đang chạy Linux containers.

~~~powershell
docker compose -f docker-compose.gateway-test.yml up --build -d
docker compose -f docker-compose.gateway-test.yml logs -f api-gateway
~~~

Chờ log Gateway báo listening trên port 8080, sau đó:

~~~powershell
Invoke-RestMethod http://localhost:5100/health
pwsh -File tests/ApiGateway.Smoke/auth-smoke.ps1
~~~

Script yêu cầu PowerShell 7 và kiểm tra đăng ký, trùng email, login, refresh rotation, replay/family revocation, logout và JWT bắt buộc. Rate limit là 20 requests/phút/IP: nếu chạy script lại ngay bị 429, chờ ít nhất một phút.

PostgreSQL cho pgAdmin/DBeaver:
- Host: localhost
- Port: 5435
- Database: swiftride_auth
- Username: swiftride
- Password: gateway_test_password

Compose chỉ chạy Gateway + PostgreSQL, chưa khởi động downstream services. Vì vậy protected request không JWT trả 401; request JWT hợp lệ tới route proxy có thể trả 502 do downstream chưa chạy. Không dùng file này để xác nhận forwarding/header enrichment. Các endpoint /auth/* và /health chạy nội bộ.

Dừng containers và giữ dữ liệu:
~~~powershell
docker compose -f docker-compose.gateway-test.yml down
~~~

Xóa database test để chạy từ đầu (mất toàn bộ user/token của stack test):
~~~powershell
docker compose -f docker-compose.gateway-test.yml down -v
~~~

Stack dùng project name và volume riêng, không dùng database của docker-compose.yml chính. Cổng 5100/5435 trùng stack chính: dừng stack chính trước nếu đang chạy. EnsureCreated chỉ tạo schema mới; nếu schema test cũ không tương thích, dùng database test mới hoặc reset volume test.

Cấu hình đã được kiểm tra bằng docker compose config --quiet. Chưa chạy containers trong môi trường tạo file.
