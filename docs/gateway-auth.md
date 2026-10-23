# Gateway và Authentication

SwiftRide dùng Ocelot trên .NET 10; Auth nằm ngay trong ApiGateway, database Auth là PostgreSQL. Gateway phát JWT HS256 và refresh token rotation. EF Core/Npgsql dùng phiên bản tập trung trong Directory.Packages.props.

## URL công khai

| Client gọi Gateway | Downstream |
|---|---|
| /api/trips/* | TripService /trips/* |
| /api/payments/* | PaymentService /payments/* |
| POST /api/matching/quote | MatchingService /quote |
| POST /api/matching/match | MatchingService /match |
| POST /api/drivers/location | MatchingService /api/drivers/location |
| /auth/* | Controllers nội bộ Gateway |

Products/Orders vẫn là route minh họa, chưa có service trong Compose. Không có route public vào /internal/*.

Routes nằm trong src/ApiGateway/ocelot.json. Compose override host/port theo index 0=Trip, 1=Quote, 2=Payment, 6=Match, 7=DriverLocation. Khi đổi thứ tự Routes phải cập nhật Compose.

## Xác thực nhất quán

<<<<<<< HEAD
1. Copy .env.example thành .env.
2. Có thể điền AUTH_DB_PASSWORD và JWT_SECRET_KEY để override các giá trị test mặc định trong Compose. Sinh key bằng CSPRNG, ít nhất 32 bytes; ví dụ PowerShell:
=======
Gateway xác thực JWT trước khi proxy. IdentityHeaders handler luôn strip X-User-Id/Email/Roles do client gửi và có thể gắn lại claims đã xác thực để quan sát. Header Authorization được giữ để mỗi downstream tự validate JWT qua src/Shared/ServiceSecurity.cs. Services hoàn toàn không lấy quyền từ X-User-*.
>>>>>>> van

Các service dùng cùng JwtSettings:SecretKey/Issuer/Audience với Gateway. HS256 là mô hình tin cậy chung cho các service trong demo; production có thể chuyển sang ký bất đối xứng để downstream chỉ giữ public key. TLS cần được cấu hình ở edge và mạng giữa services theo deployment.

Mọi business endpoint yêu cầu authentication. Health cho phép anonymous. Truy cập trực tiếp service bằng header giả mà không có JWT trả 401.

## Tài khoản và vai trò

POST /auth/register nhận email/password và luôn tạo Rider; role/ID client gửi không được dùng để cấp quyền. Password 12–128 ký tự, Identity PasswordHasher PBKDF2. Email được normalize, có unique index.

POST /auth/login trả accessToken, refreshToken và accessTokenExpiresAtUtc. Legacy role User được chuyển thành Rider khi đăng nhập; token cũ phải thay bằng token mới.

GET /auth/me cần JWT, trả id/email/roles từ user trong AuthDb. Không trả password hash hoặc refresh token. Role mới dùng cho authorization có hiệu lực khi cấp access token mới.

Driver chỉ được cấp qua quy trình tin cậy: môi trường Development có seed demo; production cần nhân sự vận hành xác minh tài xế rồi provision Role=Driver trong AuthDb bằng công cụ quản trị hạn chế quyền. Không có public API tự nâng vai trò. Khi đổi role/thu hồi quyền cần revoke refresh families và xử lý access tokens hiện có (TTL mặc định 15 phút, skew 30 giây); chưa có cơ chế vô hiệu hóa JWT tức thời.

Seed chỉ chạy khi Environment=Development và DevelopmentAccounts:Enabled=true. Tạo tài khoản chưa tồn tại, không thay mật khẩu/role của user đã tồn tại:
- rider.demo@example.com — Rider
- driver.demo@example.com — Driver
- driver.other@example.com — Driver

Mật khẩu lấy từ DevelopmentAccounts:Password; Compose local mặc định SwiftRide-demo-2026!, có thể override DEV_ACCOUNT_PASSWORD. Tắt seed ở production. Nếu email demo đã bị đăng ký trước, seed không tự nâng quyền; cần kiểm tra tài khoản bằng /auth/me.

## Refresh token

POST /auth/refresh-token và POST /auth/revoke nhận {"refreshToken":"<secret>"}. Refresh secret sinh bằng CSPRNG 64 bytes, database chỉ lưu SHA-256 hash. Transaction ReadCommitted và SELECT FOR UPDATE đảm bảo một token chỉ rotate thành công một lần. Dùng lại token revoked thu hồi cả family. Client serialize các refresh requests và thay cặp token atomically; strict replay có thể yêu cầu đăng nhập lại nếu response bị mất.

Revoke idempotent trả 204 và thu hồi family, không thu hồi các phiên đăng nhập khác. JWT cũ vẫn hợp lệ tới khi hết hạn. Response token có Cache-Control: no-store. Không log token hoặc đặt trong URL.

## Quyền nghiệp vụ

- Rider tạo chuyến: RiderId được ghi đè từ sub của JWT.
- Rider chỉ xem/hủy chuyến của mình; state machine vẫn kiểm tra trạng thái được hủy.
- Driver chỉ xem/cập nhật chuyến đã được giao. Không được tạo chuyến thay Rider.
- Vị trí Driver luôn dùng DriverId từ JWT; DriverId trong body bị bỏ qua.
- Rider gọi /api/matching/match cho chuyến của mình. Matching tìm driver gần đó và tạo invitation bằng API nội bộ có authentication.
- GET /api/trips/invitations dành cho Driver, chỉ trả lời mời của chính mình còn hạn.
- Driver nhận chuyến chỉ khi có invitation còn hạn (2 phút), Trip Requested và chưa có Driver. ID ở body không được dùng. PostgreSQL xmin concurrency token ngăn hai Driver nhận cùng một chuyến; loser trả 409.
- Driver được giao dropoff sẽ chuyển chuyến sang PaymentPending trong cùng SaveChanges, không cần client gọi system transition.
- Rider chỉ tạo/xem payment cho chuyến mình. Ownership được kiểm tra trước cả nhánh retry idempotency.
- Refund/reconcile chỉ Admin. Chưa có public API cấp Admin.
- User JWT không gọi được pay/fail/payment-pending của Trip. Các policy này yêu cầu credential nội bộ; không thể giả role bằng header.

Payment hiện là mô phỏng ledger/thanh toán trong bài tập, chưa tích hợp nhà cung cấp thanh toán thật. Giá chuyến hiện vẫn theo FareEstimate trong API tạo chuyến; việc ràng buộc giá với quote được ký phía server là phần nghiệp vụ cần bổ sung trước khi sử dụng tiền thật.

## Service-to-service

ServiceAuth:SecretKey tách khỏi JwtSettings:SecretKey. Service JWT có issuer SwiftRide.Services, audience SwiftRide.Internal, TTL 1 phút; không phát cho FE:
- payment-service: đọc internal Trip snapshot và gọi /internal/trips/{id}/pay.
- matching-service: đọc snapshot và tạo /internal/trips/{id}/invitations.

InternalTripHandler cấp token theo từng request. Endpoints /internal/* không được proxy bởi Ocelot. Secrets nội bộ phải giới hạn trong mạng backend. Mô hình shared secret cho trusted services; nếu cần cô lập từng service, triển khai asymmetric credentials hoặc workload identity riêng.

## Chạy Docker

Copy .env.example thành .env, đặt AUTH_DB_PASSWORD và JWT_SECRET_KEY (CSPRNG >=32 bytes); nên đặt SERVICE_AUTH_SECRET_KEY riêng. Docker Compose vẫn yêu cầu secrets này, không commit file .env.

~~~powershell
docker compose up --build -d
docker compose logs -f api-gateway
pwsh -File tests/Gateway.Security/gateway-security.ps1
~~~

Compose bật DevelopmentAccounts và EnsureCreated cho Auth. Trip/Payment dùng Database:Migrate=true để áp dụng migrations khi khởi động local. Production áp dụng migration bằng deployment job, tắt auto-migrate/seed và thiết lập secrets/TLS/network policy.

PostgreSQL Auth local: localhost:5435, database swiftride_auth, username swiftride, password AUTH_DB_PASSWORD. Trip:5433, Payment:5434, Mongo:27017.

## Chạy local

Cần SDK/runtime .NET 10. Cấu hình ConnectionStrings của từng service và JwtSettings/ServiceAuth bằng environment hoặc user-secrets. Nếu đổi connection string trong appsettings, lưu ý user-secrets/environment có độ ưu tiên cao hơn.

~~~powershell
$env:JwtSettings__SecretKey = "<generated-user-key>"
$env:ServiceAuth__SecretKey = "<different-generated-internal-key>"
dotnet run --project src/TripService --urls http://localhost:5101
~~~

Đặt Database__Migrate=true khi cần áp dụng migrations local. Gateway Auth với database mới có thể bật AuthDatabase__EnsureCreated=true trong Development. EnsureCreated không nâng cấp schema cũ, không kết hợp với migrations trên cùng database.

Migration DriverInvitationsAndConcurrency thêm bảng invitation và mapping xmin của PostgreSQL. Không xóa volume/database cũ để nâng cấp; áp dụng migration Trip.

## Kiểm chứng

~~~powershell
# PowerShell 7, stack backend chạy và seed Development được bật
pwsh -File tests/Gateway.Security/gateway-security.ps1
# Regression refresh/logout; chờ một phút giữa các suite nếu cùng IP
pwsh -File tests/ApiGateway.Smoke/auth-smoke.ps1
~~~

Rate limit Auth 20 requests/phút/IP/process; chạy nhiều suite liên tiếp có thể bị 429. Script tạo tài khoản/chuyến/payment test, không xóa dữ liệu nghiệp vụ sẵn có.

Nguồn chính thức:
- https://ocelot.readthedocs.io/en/latest/features/authentication.html
- https://ocelot.readthedocs.io/en/latest/features/delegatinghandlers.html
- https://learn.microsoft.com/aspnet/core/security/authentication/configure-jwt-bearer-authentication
- https://www.npgsql.org/efcore/modeling/concurrency.html
