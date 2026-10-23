# Authentication nội bộ tại Ocelot Gateway

Gateway là một ASP.NET Core API duy nhất: controllers xử lý Auth, Ocelot xử lý proxy. AuthDb (PostgreSQL 18) thuộc Gateway; Users và RefreshTokens không nằm trong downstream.

Toàn bộ solution, bao gồm ApiGateway, dùng .NET 10. Gateway target net10.0, JwtBearer dùng phiên bản 10 từ Directory.Packages.props; Dockerfile dùng SDK/runtime 10. Gateway dùng EF Core 10 và Npgsql theo Directory.Packages.props, đồng bộ với các service PostgreSQL khác. Ocelot dùng phiên bản 25.0.1.

## Mã nguồn

- Data/AuthDbContext.cs: entities, unique index email/token hash, FK và index family.
- Auth/TokenService.cs: ký/validate HS256, claims sub/email/role/jti; random 64 bytes từ CSPRNG; SHA-256 hash refresh token.
- Auth/AuthController.cs: register/login/refresh-token/revoke, Identity PasswordHasher PBKDF2 210.000 iterations.
- Proxy/IdentityHeaders.cs: global DelegatingHandler strip và inject danh tính trước khi gửi HTTP request, áp dụng tất cả proxy routes.
- Program.cs: tách pipeline Auth/health khỏi Ocelot bằng MapWhen; validation cấu hình khi startup, JwtBearer, authorization, rate limit, ProblemDetails, EF/PostgreSQL và Ocelot.
- ../../examples/OrderService/GatewayIdentityMiddleware.cs: middleware mẫu, đặt trước UseAuthorization.

Không có route Ocelot /auth/*: đây là endpoints nội bộ có [AllowAnonymous]. Routes nằm trong ocelot.json. GlobalConfiguration.AuthenticationOptions.AuthenticationProviderKeys=["Bearer"] bảo vệ routes mặc định. Route /public/products/* có AllowAnonymous=true; /api/products/* và /api/orders/* yêu cầu JWT. Products/Orders là ví dụ cấu hình, chưa có service triển khai trong repository; cần chạy service ở 5104/5105 hoặc đổi destination. Các routes trips/matching/payments hiện có cũng đã được bảo vệ.

## Chạy với Docker Compose

1. Copy .env.example thành .env.
2. Điền AUTH_DB_PASSWORD và JWT_SECRET_KEY. Sinh key bằng CSPRNG, ít nhất 32 bytes; ví dụ PowerShell:

   ~~~powershell
   [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(64))
   ~~~

3. Chạy docker compose up --build.
4. Gateway ở http://localhost:5100, PostgreSQL ở localhost:5435.

Compose là cấu hình development, bật AuthDatabase__EnsureCreated. EnsureCreated tạo schema lần đầu, không cập nhật schema đã tồn tại. Không dùng EnsureCreated cùng database quản lý bằng migrations. Production tắt tùy chọn này, đặt ASPNETCORE_ENVIRONMENT=Production và áp dụng EF migrations bằng deployment job trước khi chạy Gateway.

## Chạy local và migrations

Root global.json yêu cầu SDK 10.0.401; Gateway cần ASP.NET Core runtime 10. Có thể dùng Dockerfile .NET 10 để build độc lập với SDK trên host. dotnet-ef dùng phiên bản 10.0.12 tương ứng EF Core của Gateway.

~~~powershell
dotnet user-secrets set "JwtSettings:SecretKey" "<generated-secret>" --project src/ApiGateway
dotnet user-secrets set "ConnectionStrings:AuthDb" "Host=localhost;Port=5435;Database=swiftride_auth;Username=swiftride;Password=<password>" --project src/ApiGateway
$env:ASPNETCORE_ENVIRONMENT = "Development"
# Với database mới: generate migration, review và commit source migration.
dotnet tool install --global dotnet-ef --version 10.0.12
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

Ocelot strip X-User-Id, X-User-Email, X-User-Roles cho mọi request rồi inject từ ClaimsPrincipal đã qua JWT validation. roles ngăn cách bằng dấu phẩy; mô hình User hiện có một role, có thể mở rộng bảng UserRoles và phát nhiều claim role. Authorization và Cookie không được forward.

Downstream middleware dựng ClaimsPrincipal với authentication type TrustedGateway; RequireAuthorization/RequireRole hoạt động mà không cần JwtBearer. Phải chặn truy cập trực tiếp downstream bằng network policy/firewall hoặc mTLS. IP allowlist trong mẫu kiểm tra địa chỉ peer thực, không lấy X-Forwarded-For do client gửi. Nếu có sidecar/proxy, cấu hình trust theo deployment. Docker Compose hiện publish các port service phục vụ development; production cần bỏ chúng và giới hạn mạng. Không bật ForwardedHeaders để thay peer IP trước middleware mẫu nếu chưa đánh giá trust boundary.

Dùng HTTPS ở edge trước khi gửi password/token; gateway-to-downstream nên dùng TLS/mTLS theo deployment. Refresh tokens trả trong body: client giữ bí mật, không log, không đặt query string. Nếu chuyển sang HttpOnly cookie cần thêm CSRF protection và chính sách SameSite. Giữ rows revoked đủ lâu để phát hiện replay; kế hoạch cleanup phải giữ marker family bị revoked nếu xóa lịch sử trong khi descendant còn hiệu lực.

ExceptionHandler trả ProblemDetails 500 cho lỗi bất ngờ, không trả stack trace; lỗi nhập liệu 400, credentials/token sai 401, email trùng 409, vượt rate limit 429. DB commit xong trước khi trả token.

## Kiểm chứng

~~~powershell
# PowerShell 7; Gateway + PostgreSQL phải đang chạy.
pwsh -File tests/ApiGateway.Smoke/auth-smoke.ps1
~~~

Smoke script kiểm tra register/trùng email/login sai/rotation/replay thu hồi descendant/logout idempotent và header giả không vượt protected route. Script để lại một user test email ngẫu nhiên.

Kiểm tra thêm khi có downstream echo endpoint: gửi JWT hợp lệ kèm X-User-Id giả và X-User-Roles: Admin; downstream phải thấy sub/role của JWT. Gửi header giả tới route anonymous không kèm JWT; downstream không được nhận các X-User-* đó. Kiểm tra đồng thời hai refresh cùng token: chỉ một 200, còn lại 401; refresh descendant phải 401. Cần chạy trên PostgreSQL thật vì in-memory provider không kiểm chứng FOR UPDATE.

SDK .NET 10.0.401 đã được cài tại D:\dotnet. Khi chuyển sang Ocelot cần kiểm chứng build, proxy/header forwarding và Auth trên PostgreSQL thật; xem kết quả kiểm chứng của lần thay đổi.

## Tài liệu chính thức

- https://ocelot.readthedocs.io/en/latest/features/authentication.html
- https://ocelot.readthedocs.io/en/latest/features/delegatinghandlers.html
- https://www.npgsql.org/efcore/

## Ocelot và môi trường Docker

AuthController xử lý /auth/* trong pipeline nội bộ; /health cũng nằm trong pipeline này. Các request còn lại đi vào UseOcelot. Không đưa endpoint mappings của MVC/Minimal API vào pipeline proxy.

ocelot.json được copy khi build/publish. Environment variables được nạp lại sau JSON để override cấu hình Docker: Routes__0__DownstreamHostAndPorts__0__Host và Port cho Trips, index 1 cho Matching, index 2 cho Payments. Nếu đổi thứ tự Routes phải cập nhật indices trong Compose.

Global IdentityHeaders handler đọc HttpContext theo từng request; không lưu principal trong handler được cache. Nó strip cả request/content identity headers, inject claims đã xác thực rồi xóa Authorization/Cookie. Route anonymous không chạy JWT authentication của Ocelot nên không phát danh tính từ header của client.
Kết quả kiểm chứng chuyển đổi (12/10/2026): restore Ocelot và build ApiGateway trên .NET SDK 10.0.401 thành công, 0 warnings và 0 errors. JSON/XML và git diff --check đạt. Chưa chạy integration/smoke tests với PostgreSQL hoặc xác nhận forwarding tại runtime.

## Chuyển database Auth sang PostgreSQL

Gateway dùng Npgsql/EF Core 10. Compose tạo auth-postgres riêng với database swiftride_auth, user swiftride và host port 5435 (container port 5432), không dùng chung database Trip/Payment.

Các truy vấn FOR UPDATE dùng tên bảng/cột có dấu ngoặc kép để khớp mapping EF. Transaction dùng ReadCommitted; xử lý email trùng bằng SQLSTATE 23505 trên unique index IX_Users_NormalizedEmail. DateTime token được tạo ở UTC và Npgsql lưu thành timestamp with time zone.

Đây là chuyển provider/cấu hình cho database mới, không tự copy dữ liệu MySQL. Volume MySQL cũ không bị xóa. Nếu cần giữ Users, export/import Id/email/NormalizedEmail/PasswordHash/Role sang schema PostgreSQL, giữ nguyên password hash, kiểm tra email trùng và mapping UUID/UTC. Nên yêu cầu đăng nhập lại thay vì chuyển phiên refresh đang hoạt động. Cần thực hiện việc chuyển dữ liệu trong maintenance window với kế hoạch rollback riêng.

Nếu đã có user-secrets hoặc environment ConnectionStrings__AuthDb dùng MySQL, phải cập nhật thành connection string PostgreSQL; chúng override appsettings. Database PostgreSQL mới dùng EnsureCreated trong Compose development; production tạo và review migration PostgreSQL trước deploy. Không áp dụng migrations MySQL vào PostgreSQL.
Kiểm chứng PostgreSQL (23/10/2026): restore và build Gateway thành công trên SDK 10.0.401, 0 warnings/0 errors; docker compose config --quiet, cấu hình JSON/XML và git diff --check đạt. Docker daemon chưa chạy nên chưa thực thi smoke tests trên PostgreSQL thật.
