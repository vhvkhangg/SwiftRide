# Test Gateway trên Docker

Dùng duy nhất docker-compose.yml cho toàn bộ backend, gồm cả Keycloak. Cần Docker Desktop chạy Linux containers.

1. (Tùy chọn) Copy .env.example thành .env để đổi `KEYCLOAK_PUBLIC_URL`, `KEYCLOAK_DB_PASSWORD`, `KEYCLOAK_ADMIN_PASSWORD`, `SERVICE_AUTH_SECRET_KEY`, `DEV_ACCOUNT_PASSWORD`. Compose có giá trị mặc định chỉ dành cho test local.
2. Chạy từ thư mục gốc repository (ví dụ E:/SwiftRide):

~~~powershell
docker compose up --build -d
docker compose logs -f keycloak api-gateway
~~~

Keycloak import realm `swiftride` lần đầu khởi động (khoảng 20–40 giây). Gateway chỉ báo ready khi đọc được discovery của Keycloak:

~~~powershell
Invoke-RestMethod http://localhost:5100/health/ready
pwsh -File tests/Gateway.Security/gateway-security.ps1
~~~

Tài khoản demo (realm import):
- rider.demo@example.com, rider.other@example.com — Rider
- driver.demo@example.com, driver.other@example.com — Driver
- admin.demo@example.com — Admin (refund)

Mật khẩu mặc định: SwiftRide-demo-2026!; override bằng DEV_ACCOUNT_PASSWORD (chỉ áp dụng khi realm được import lần đầu) và tham số -SeedPassword của test.

Lấy token thủ công (client test `swiftride-postman`, chỉ dev):

~~~powershell
$token = (Invoke-RestMethod http://localhost:8180/realms/swiftride/protocol/openid-connect/token -Method Post `
  -ContentType application/x-www-form-urlencoded `
  -Body @{grant_type='password'; client_id='swiftride-postman'; username='rider.demo@example.com'; password='SwiftRide-demo-2026!'}).access_token
Invoke-RestMethod http://localhost:5100/api/trips/active -Headers @{Authorization="Bearer $token"}
~~~

Keycloak Admin Console: http://localhost:8180/admin (KEYCLOAK_ADMIN_USER / KEYCLOAK_ADMIN_PASSWORD, mặc định admin / keycloak-admin-dev). Dùng để cấp vai trò Driver/Admin.

Dừng containers và giữ dữ liệu:

~~~powershell
docker compose down
~~~

Không chạy down -v nếu muốn giữ dữ liệu. Muốn import lại realm sau khi sửa `keycloak/swiftride-realm.json` trên máy dev, chỉ xóa volume `keycloak-postgres-data` (mất user tự đăng ký và phiên đăng nhập), không xóa volume Trip/Payment/Mongo.

Volume `auth-postgres-data` của phiên bản cũ (Gateway tự cấp JWT) không còn được dùng; có thể giữ để tham khảo hoặc tự xóa khi chắc chắn không cần.

Routes và thiết kế quyền chi tiết ở gateway-auth.md. Products/Orders là route mẫu, chưa có service triển khai.
