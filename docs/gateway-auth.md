# Authentication nội bộ tại YARP Gateway

Gateway là một ASP.NET Core API duy nhất: controllers xử lý Auth, YARP xử lý proxy. AuthDb (MySQL 8.4/InnoDB) thuộc Gateway; Users và RefreshTokens không nằm trong downstream.

Toàn bộ solution, bao gồm ApiGateway, dùng .NET 10. Gateway target net10.0, JwtBearer dùng phiên bản 10 từ Directory.Packages.props; Dockerfile dùng SDK/runtime 10. EF Core/Design/Relational của Gateway vẫn được pin riêng ở 9.0.10 cùng Pomelo 9.0.0 để giữ tương thích MySQL provider; EF Core 9 chạy được trên .NET 10. Các service khác giữ EF Core 10 theo cấu hình tập trung. Không trộn EF Core 10 với Pomelo 9 trong Gateway. YARP giữ phiên bản 2.3.0.

## Mã nguồn

- Data/AuthDbContext.cs: entities, unique index email/token hash, FK và index family.
- Auth/TokenService.cs: ký/validate HS256, claims sub/email/role/jti; random 64 bytes từ CSPRNG; SHA-256 hash refresh token.
- Auth/AuthController.cs: register/login/refresh-token/revoke, Identity PasswordHasher PBKDF2 210.000 iterations.
- Proxy/IdentityHeaders.cs: strip và inject danh tính, áp dụng tất cả proxy routes.
- Program.cs: validation cấu hình khi startup, JwtBearer, authorization, rate limit, ProblemDetails, EF/MySQL và YARP.
- ../../examples/OrderService/GatewayIdentityMiddleware.cs: middleware mẫu, đặt trước UseAuthorization.

Không có route YARP /auth/*: đây là endpoints nội bộ có [AllowAnonymous]. Route public minh họa là /public/products/* với AuthorizationPolicy=anonymous; /api/products/* và /api/orders/* dùng policy authenticated. Products/Orders là ví dụ cấu hình, chưa có service triển khai trong repository; cần chạy service ở 5104/5105 hoặc đổi destination. Các routes trips/matching/payments hiện có cũng đã được bảo vệ.

## Chạy với Docker Compose

1. Copy .env.example thành .env.
2. Điền AUTH_DB_PASSWORD, AUTH_DB_ROOT_PASSWORD và JWT_SECRET_KEY. Sinh key bằng CSPRNG, ít nhất 32 bytes; ví dụ PowerShell:

   ~~~powershell
   [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(64))
   ~~~

3. Chạy docker compose up --build.
4. Gateway ở http://localhost:5100, MySQL ở localhost:3306.

Compose là cấu hình development, bật AuthDatabase__EnsureCreated. EnsureCreated tạo schema lần đầu, không cập nhật schema đã tồn tại. Không dùng EnsureCreated cùng database quản lý bằng migrations. Production tắt tùy chọn này, đặt ASPNETCORE_ENVIRONMENT=Production và áp dụng EF migrations bằng deployment job trước khi chạy Gateway.

## Chạy local và migrations

Root global.json yêu cầu SDK 10.0.401; Gateway cần ASP.NET Core runtime 10. Có thể dùng Dockerfile .NET 10 để build độc lập với SDK trên host. dotnet-ef vẫn dùng phiên bản 9.0.10 tương ứng EF Core của Gateway.

~~~powershell
dotnet user-secrets set "JwtSettings:SecretKey" "<generated-secret>" --project src/ApiGateway
dotnet user-secrets set "ConnectionStrings:AuthDb" "Server=localhost;Port=3306;Database=swiftride_auth;User=swiftride;Password=<password>" --project src/ApiGateway
$env:ASPNETCORE_ENVIRONMENT = "Development"
# Với database mới: generate migration, review và commit source migration.
dotnet tool install --global dotnet-ef --version 9.0.10
dotnet ef migrations add InitialAuth --project src/ApiGateway
dotnet ef database update --project src/ApiGateway
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project src/ApiGateway --urls http://localhost:5100
~~~

Connection string trong appsettings là mẫu; SecretKey trống khiến Gateway fail startup cho đến khi được cung cấp qua secrets/environment. User-secrets chỉ được nạp ở Development. Không commit secrets. Chạy migrations với cùng cấu hình connection string/secret và Environment=Development như lúc chạy local; đặt biến môi trường này trước lệnh migrations nếu host của bạn không mặc định Development.

## API

~~~http
POST /auth/register
Content-Type: application/json

{"email":"rider@example.com","password":"Strong-password-2026!"}
~~~

Trả 201 với id/email; email trùng trả 409. Đăng ký luôn tạo role User; không nhận role từ client. Password 12–128 ký tự. Email được trim/normalize để kiểm tra trùng.

~~~http
POST /auth/login
Content-Type: application/json

{"email":"rider@example.com","password":"Strong-password-2026!"}
~~~

Trả 200:

~~~json
{
  "accessToken": "<jwt>",
  "refreshToken": "<random-secret>",
  "accessTokenExpiresAtUtc": "2026-10-12T00:15:00Z"
}
~~~

~~~http
POST /auth/refresh-token
Content-Type: application/json

{"refreshToken":"<current-refresh-token>"}
~~~

Trả cặp token mới; client phải thay token cũ atomically và serialize refresh requests. Transaction dùng SELECT ... FOR UPDATE trên unique token hash: một token chỉ rotation thành công một lần. Replay token đã revoked thu hồi tất cả active tokens cùng FamilyId rồi trả 401. Hai refresh đồng thời có thể khiến cặp mới của request thành công bị thu hồi bởi request thứ hai; đây là hành vi nghiêm ngặt có chủ đích. Mỗi login là một family riêng. Không tự retry refresh sau khi mất response; strict replay có thể yêu cầu đăng nhập lại.

~~~http
POST /auth/revoke
Content-Type: application/json

{"refreshToken":"<refresh-token>"}
~~~

Trả 204 kể cả token không tồn tại; thu hồi toàn bộ family. Không cần access token vì possession của refresh secret xác nhận quyền logout. JWT đã phát hành tiếp tục hợp lệ tới khi hết hạn (mặc định 15 phút, clock skew 30 giây); logout tức thời access token cần thêm session validation/denylist.

Gọi protected routes với Authorization: Bearer <jwt>. Thiếu/sai JWT trả 401 trước khi proxy. Auth endpoints bị giới hạn 20 requests/phút/IP/process; khi scale nhiều replicas cần rate limit tập trung ở edge và chính sách chống brute-force theo tài khoản. Rate limit dùng peer IP; nếu sau load balancer hãy cấu hình trusted ForwardedHeaders trước rate limiter.

## Downstream và trust boundary

YARP strip X-User-Id, X-User-Email, X-User-Roles cho mọi request rồi inject từ ClaimsPrincipal đã qua JWT validation. roles ngăn cách bằng dấu phẩy; mô hình User hiện có một role, có thể mở rộng bảng UserRoles và phát nhiều claim role. Authorization và Cookie không được forward.

Downstream middleware dựng ClaimsPrincipal với authentication type TrustedGateway; RequireAuthorization/RequireRole hoạt động mà không cần JwtBearer. Phải chặn truy cập trực tiếp downstream bằng network policy/firewall hoặc mTLS. IP allowlist trong mẫu kiểm tra địa chỉ peer thực, không lấy X-Forwarded-For do client gửi. Nếu có sidecar/proxy, cấu hình trust theo deployment. Docker Compose hiện publish các port service phục vụ development; production cần bỏ chúng và giới hạn mạng. Không bật ForwardedHeaders để thay peer IP trước middleware mẫu nếu chưa đánh giá trust boundary.

Dùng HTTPS ở edge trước khi gửi password/token; gateway-to-downstream nên dùng TLS/mTLS theo deployment. Refresh tokens trả trong body: client giữ bí mật, không log, không đặt query string. Nếu chuyển sang HttpOnly cookie cần thêm CSRF protection và chính sách SameSite. Giữ rows revoked đủ lâu để phát hiện replay; kế hoạch cleanup phải giữ marker family bị revoked nếu xóa lịch sử trong khi descendant còn hiệu lực.

ExceptionHandler trả ProblemDetails 500 cho lỗi bất ngờ, không trả stack trace; lỗi nhập liệu 400, credentials/token sai 401, email trùng 409, vượt rate limit 429. DB commit xong trước khi trả token.

## Kiểm chứng

~~~powershell
# PowerShell 7; Gateway + MySQL phải đang chạy.
pwsh -File tests/ApiGateway.Smoke/auth-smoke.ps1
~~~

Smoke script kiểm tra register/trùng email/login sai/rotation/replay thu hồi descendant/logout idempotent và header giả không vượt protected route. Script để lại một user test email ngẫu nhiên.

Kiểm tra thêm khi có downstream echo endpoint: gửi JWT hợp lệ kèm X-User-Id giả và X-User-Roles: Admin; downstream phải thấy sub/role của JWT. Gửi header giả tới route anonymous không kèm JWT; downstream không được nhận các X-User-* đó. Kiểm tra đồng thời hai refresh cùng token: chỉ một 200, còn lại 401; refresh descendant phải 401. Cần chạy trên MySQL thật vì in-memory provider không kiểm chứng FOR UPDATE.

Tại môi trường thực hiện thay đổi: chưa có .NET SDK và Docker daemon không chạy nên chưa build hoặc chạy integration tests. Đã kiểm tra cấu hình JSON/XML, diff và source tĩnh; không coi đây là xác nhận runtime.

## Tài liệu chính thức

- https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/yarp/authn-authz
- https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/yarp/transforms-request
- https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql
