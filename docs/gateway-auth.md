# Gateway và Authentication (Keycloak)

Hợp đồng HTTP/SignalR cho mobile: [mobile-api-contract.md](mobile-api-contract.md).

SwiftRide dùng **Keycloak** làm Identity Provider theo chuẩn **OAuth 2.0 / OpenID Connect**. Keycloak quản lý user, mật khẩu, vai trò, phiên đăng nhập và cấp access token **RS256**. API Gateway (YARP) và ba service **không phát token, không lưu mật khẩu**; chúng chỉ kiểm tra token bằng public key (JWKS) của Keycloak.

```text
Rider/Driver app ──(Authorization Code + PKCE)──► Keycloak realm "swiftride"
        │                                              │ access token RS256 (iss, aud=swiftride-api, sub, role)
        ▼                                              ▼
   API Gateway (YARP)   ── kiểm tra JWT qua JWKS ── Trip / Matching / Payment (kiểm tra lại JWT)
```

## Realm `swiftride`

Cấu hình nằm trong [keycloak/swiftride-realm.json](../keycloak/swiftride-realm.json), import tự động khi Keycloak khởi động lần đầu (`start-dev --import-realm`).

| Thành phần | Giá trị |
|---|---|
| Vai trò realm | `Rider`, `Driver`, `Admin` |
| Default role của user mới | chỉ `Rider`: role `default-roles-swiftride` khai báo trong `roles.realm` (Keycloak bỏ qua `composites` đặt dưới `defaultRole` khi import) |
| Claim trong access token | `sub` (UUID user Keycloak), `email`, `role` (mảng vai trò realm), `aud` = `swiftride-api` |
| Access token TTL | 15 phút; refresh token xoay vòng (`revokeRefreshToken`), không cho dùng lại |
| Mật khẩu | tối thiểu 12 ký tự, khác username; brute-force protection bật |
| Đăng ký | trong app Rider, qua `POST /api/auth/register` của Gateway (xem dưới). Trang đăng ký của Keycloak tắt (`registrationAllowed: false`); email làm username; user mới luôn là Rider |

Client OIDC:

| Client | Loại | Luồng | Dùng cho |
|---|---|---|---|
| `swiftride-mobile` | public | Authorization Code + **PKCE S256**; không cho password grant | App Rider/Driver (redirect `swiftride-rider://*`, `swiftride-driver://*`, Expo `exp://*`, web preview `http://localhost:8081/*`) |
| `swiftride-registration` | confidential (secret `REGISTRATION_CLIENT_SECRET`) | Client credentials; service account chỉ có `realm-management/manage-users` | Gateway tạo tài khoản Rider khi đăng ký trong app. Không đăng nhập người dùng được |
| `swiftride-postman` | public | Password grant (Direct Access Grants) | **Chỉ dev/test**: Postman/Newman, `tests/System/messaging.py`, `tests/Gateway.Security/*`. Tắt client này ở production |

Tài khoản demo (chỉ import ở môi trường dev; mật khẩu lấy từ `DEV_ACCOUNT_PASSWORD`, mặc định `SwiftRide-demo-2026!`):

- rider.demo@example.com — Rider
- rider.other@example.com — Rider (dùng để kiểm tra ownership)
- driver.demo@example.com — Driver
- driver.other@example.com — Driver
- admin.demo@example.com — Admin (refund)

Driver và Admin chỉ được cấp qua Keycloak Admin Console (`http://localhost:8180/admin`, tài khoản `KEYCLOAK_ADMIN_USER`/`KEYCLOAK_ADMIN_PASSWORD`). Không có API công khai để tự nâng vai trò. Đổi role có hiệu lực ở access token kế tiếp; token đã cấp vẫn hợp lệ tới khi hết hạn (tối đa 15 phút + 30 giây lệch giờ).

Realm chỉ được import khi chưa tồn tại. Sau khi sửa `swiftride-realm.json`, cập nhật qua Admin Console, hoặc (chỉ trên môi trường dev, chấp nhận mất user/phiên) xóa volume `keycloak-postgres-data` để import lại.

## Endpoint OIDC (thay cho `/auth/*` cũ)

Gốc: `{KEYCLOAK_PUBLIC_URL}/realms/swiftride/protocol/openid-connect`.

| Việc | Endpoint |
|---|---|
| Discovery | `GET /realms/swiftride/.well-known/openid-configuration` |
| Đăng nhập (mobile) | `GET .../auth?client_id=swiftride-mobile&response_type=code&code_challenge=…&code_challenge_method=S256&redirect_uri=…` |
| Đổi code / refresh / password grant (test) | `POST .../token` (form-urlencoded) |
| Thông tin user | `GET .../userinfo` với Bearer token → `sub`, `email`, `role` |
| Đăng xuất | `POST .../logout` với `client_id` + `refresh_token` → 204 |
| Public keys | `GET .../certs` (JWKS) |

Gateway không còn `/auth/login`, `/auth/me`, `/auth/refresh-token`, `/auth/revoke`, không còn AuthDb; Gateway không bao giờ nhận mật khẩu đăng nhập.

### Đăng ký Rider trong app: `POST /api/auth/register`

Endpoint duy nhất của Gateway không cần token. Body `{ "email", "password", "firstName", "lastName" }`.

1. Gateway kiểm tra dữ liệu theo đúng chính sách realm (email hợp lệ, mật khẩu 12–128 ký tự và khác email, họ/tên 1–100 ký tự) → sai thì 400 kèm `errors`.
2. Lấy token client credentials của `swiftride-registration`, gọi `POST /admin/realms/swiftride/users` (Keycloak lưu mật khẩu; Gateway không lưu, không ghi log).
3. Request tạo user không có trường role/group: role chỉ đến từ default role, nên không thể đăng ký thành Driver/Admin.

| Kết quả | HTTP |
|---|---|
| Tạo xong | 201 `{ "email", "role": "Rider" }` |
| Email đã có | 409 problem `detail: "Email này đã được đăng ký."` |
| Dữ liệu sai / Keycloak từ chối mật khẩu | 400 problem |
| Quá 10 lần / 15 phút / IP | 429 (rate limit; trong Docker mọi client đi qua cùng IP bridge nên giới hạn gần như toàn cục) |
| Keycloak lỗi, chưa cấu hình secret | 503. Nếu lỗi xảy ra sau khi Keycloak đã tạo user, gửi lại sẽ nhận 409 |

Sau khi đăng ký, app mở trang đăng nhập (PKCE) với `login_hint` = email vừa tạo. Cấu hình: `Registration:KeycloakUrl` (trong Compose `http://keycloak:8080`), `Registration:ClientSecret`, `Registration:PermitsPerWindow`. Keycloak đã có dữ liệu từ trước được service một lần `keycloak-setup` (`keycloak/ensure-registration.sh`) bổ sung client, secret và quyền này mỗi lần `docker compose up`; Gateway chờ bước này xong mới chạy.

## Kiểm tra token ở Gateway và service

Cấu hình chung (`Authentication:*`, xem `KeycloakTokens` trong [ServiceSecurity.cs](../src/Shared/ServiceSecurity.cs)):

| Khóa | Ý nghĩa |
|---|---|
| `Authentication:Authority` | Issuer công khai, ví dụ `http://localhost:8180/realms/swiftride`. Token phải có đúng `iss` này |
| `Authentication:MetadataAddress` | Địa chỉ discovery nội bộ (trong Compose: `http://keycloak:8080/...`) |
| `Authentication:Audience` | `swiftride-api` |
| `Authentication:RequireHttpsMetadata` | `false` chỉ cho dev; production dùng HTTPS |

Token chỉ hợp lệ khi đủ: chữ ký RS256 khớp JWKS, đúng issuer, đúng audience, còn hạn, `sub` là GUID khác rỗng. Token HS256, `alg: none`, ký bằng khóa khác hoặc bị sửa payload đều bị từ chối (401).

Keycloak đặt `KC_HOSTNAME` = `KEYCLOAK_PUBLIC_URL` nên `iss` luôn cố định dù client gọi qua cổng nào; container lấy JWKS qua backchannel `keycloak:8080` (`KC_HOSTNAME_BACKCHANNEL_DYNAMIC=true`). Khi chạy app trên điện thoại thật, đặt `KEYCLOAK_PUBLIC_URL` thành địa chỉ LAN của máy (ví dụ `http://192.168.1.10:8180`) để token có issuer mà điện thoại truy cập được.

Gateway xác thực JWT trước khi proxy. IdentityHeaders handler luôn xóa `X-User-*` do client gửi; header Authorization được giữ để mỗi service tự xác thực lại. Service không lấy quyền từ `X-User-*`. `GET /health` là liveness; `GET /health/ready` của Gateway trả 503 khi không đọc được discovery của Keycloak.

## Quyền nghiệp vụ

- Rider tạo chuyến: RiderId lấy từ `sub` của token, body bị bỏ qua.
- Rider chỉ xem/hủy chuyến của mình; state machine vẫn kiểm tra trạng thái được hủy.
- Driver chỉ xem/cập nhật chuyến đã được giao, không tạo chuyến thay Rider.
- Vị trí Driver luôn dùng DriverId từ token; DriverId trong body bị bỏ qua.
- Rider xác nhận giá qua `/api/trips/{id}/confirm-price` trước khi Trip gửi matching command. `/api/matching/match` chỉ trả ứng viên.
- `GET /api/trips/driver/invitations` chỉ trả lời mời chưa xử lý, chưa hết hạn của Driver đang đăng nhập.
- Driver nhận/từ chối chuyến phải có invitation còn hạn, Trip `PriceConfirmed` và chưa có Driver. Tài xế đã từ chối bị loại khỏi lần tìm kế tiếp.
- Driver dropoff nhận 202; Trip lưu `PaymentPending` và payment command qua transaction/outbox.
- Rider chỉ xem payment của chuyến mình. Refund chỉ Admin.
- User token không gọi được pay/fail/payment-pending của Trip; các policy này yêu cầu credential nội bộ.

Payment là mô phỏng ledger trong bài tập, chưa tích hợp nhà cung cấp thanh toán thật.

## Service-to-service

Gọi nội bộ giữa service vẫn dùng token HS256 riêng (`ServiceAuth:SecretKey`, issuer `SwiftRide.Services`, audience `SwiftRide.Internal`, TTL 1 phút), tách hoàn toàn khỏi token người dùng:

- payment-service: đọc internal Trip snapshot và gọi `/internal/trips/{id}/pay`.
- matching-service: đọc snapshot và tạo `/internal/trips/{id}/invitations`.
- trip-service: gọi `/internal/quote` của Matching.

Endpoints `/internal/*` không có route trong YARP nên không ra được ngoài Gateway. Hướng nâng cấp: chuyển sang Keycloak client credentials (mỗi service một confidential client).

## Chạy Docker

```powershell
docker compose up --build -d
docker compose logs -f keycloak api-gateway
```

Compose có giá trị mặc định chỉ dành cho local/test. Copy `.env.example` thành `.env` để override `KEYCLOAK_PUBLIC_URL`, `KEYCLOAK_DB_PASSWORD`, `KEYCLOAK_ADMIN_PASSWORD`, `SERVICE_AUTH_SECRET_KEY`, `DEV_ACCOUNT_PASSWORD`. Không commit `.env`.

| Thành phần | Địa chỉ local |
|---|---|
| API Gateway | http://localhost:5100 |
| Keycloak (issuer, login, admin console) | http://localhost:8180 |
| Keycloak database | container `keycloak-postgres` (không mở cổng ra host) |

Production: chạy Keycloak bằng `start` (không `start-dev`) sau HTTPS, tắt client `swiftride-postman`, không import user demo, đặt `RequireHttpsMetadata=true`.

## Chạy service ngoài Docker

```powershell
$env:Authentication__Authority = "http://localhost:8180/realms/swiftride"
$env:Authentication__Audience = "swiftride-api"
$env:Authentication__RequireHttpsMetadata = "false"
$env:ServiceAuth__SecretKey = "<generated-internal-key-at-least-32-bytes>"
dotnet run --project src/TripService --urls http://localhost:5101
```

Keycloak vẫn chạy trong Compose (`docker compose up -d keycloak`).

## Kiểm chứng

- Unit test: `tests/Shared/TestOidcIssuer.cs` giả lập realm (RS256 + discovery/JWKS) để kiểm tra Gateway và service thật từ chối token sai khóa, sai issuer, sai audience, hết hạn.
- Postman: [postman/README.md](../postman/README.md) — đăng nhập qua Keycloak, kiểm tra token giả và toàn bộ luồng nghiệp vụ.
- Full stack: `pwsh -NoProfile -File tests/Gateway.Security/task1.ps1` (Compose project riêng, cổng 15100-15103 và Keycloak 18180) kiểm tra token qua restart Gateway/Keycloak, readiness 503 khi Keycloak dừng, logout vô hiệu refresh token, rồi chạy `gateway-security.ps1`.

Nguồn chính thức:
- https://www.keycloak.org/server/importExport
- https://www.keycloak.org/server/hostname
- https://learn.microsoft.com/aspnet/core/fundamentals/servers/yarp/yarp-overview
- https://learn.microsoft.com/aspnet/core/security/authentication/configure-jwt-bearer-authentication
