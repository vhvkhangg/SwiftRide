# Mobile API contract - 2026-10-26

Base URL: Gateway, local `http://localhost:5100`. Thiet bi that dung IP LAN cua may chay Gateway, khong dung localhost. Production bat buoc HTTPS/WSS.

Tai lieu mo ta code hien tai, khong coi API du kien la da co. HTTP/SignalR transport da duoc test qua Gateway that voi Trip test host; nghiep vu DB/broker that van can smoke full-stack.

## Authentication (Keycloak OIDC)

Token do Keycloak cap (realm `swiftride`, issuer `{KEYCLOAK_PUBLIC_URL}/realms/swiftride`, local `http://localhost:8180/realms/swiftride`). Gateway khong con /auth/*. Moi business HTTP request gui `Authorization: Bearer <access_token>`. Khong gui token qua query string cho HTTP API. Khong dung X-User-* lam danh tinh.

App dung client public `swiftride-mobile` voi **Authorization Code + PKCE (S256)**, mo trang dang nhap cua Keycloak trong trinh duyet he thong. Dang ky Rider lam ngay trong app qua Gateway (`POST /api/auth/register`, khong can token); trang dang ky cua Keycloak da tat. Redirect URI hop le: `swiftride-rider://*`, `swiftride-driver://*`, `exp://*` (Expo Go), `http://localhost:8081/*` (web preview). Client nay khong cho password grant.

| Viec | Endpoint (goc `/realms/swiftride/protocol/openid-connect`) | Ket qua |
|---|---|---|
| Dang nhap | GET /auth?client_id=swiftride-mobile&response_type=code&scope=openid&code_challenge=…&code_challenge_method=S256&redirect_uri=… | Redirect ve app kem `code` |
| Doi code lay token | POST /token: grant_type=authorization_code, client_id, code, code_verifier, redirect_uri | 200: access_token, refresh_token, expires_in |
| Lam moi token | POST /token: grant_type=refresh_token, client_id, refresh_token | 200: cap token moi; refresh token cu het hieu luc |
| Danh tinh | GET /userinfo + Bearer | 200: sub, email, role[] |
| Dang xuat | POST /logout: client_id, refresh_token | 204 |

Dang ky (Gateway, khong can token): `POST /api/auth/register` body `{ "email", "password", "firstName", "lastName" }` -> 201 `{ "email", "role": "Rider" }`; 400 problem (`errors` theo field: mat khau 12-128 ky tu va khac email, ho/ten bat buoc); 409 email da ton tai; 429 qua nhieu lan; 503 Keycloak loi (gui lai co the nhan 409 neu user da duoc tao). Sau do dang nhap PKCE nhu tren, co the gui them `login_hint=<email>`.

Access token song 15 phut. Chi cho mot refresh request chay tai mot thoi diem va thay ca cap token; refresh token cu bi tu choi (400 invalid_grant). Logout dong SignalR va xoa token tren client; access token da cap van co the hop le den expiry.

Driver/Admin chi duoc cap trong Keycloak Admin Console; dang ky cong khai khong tu cap Driver/Admin. Demo development: rider.demo@example.com, rider.other@example.com, driver.demo@example.com, driver.other@example.com, admin.demo@example.com.

Trang thai app hien tai: app `mobile/` (khong con che do demo) dang nhap bang client `swiftride-mobile` (PKCE) va goi cac endpoint ben duoi qua Gateway (xem `mobile/README.md`). Web preview can Gateway bat CORS cho origin cua Expo (`Cors:AllowedOrigins`, mac dinh localhost:8081/8082).

## Rider flow

1. GET /userinfo cua Keycloak (hoac doc claim `role` trong token), xac nhan role Rider.
2. POST /api/matching/quote de hien gia tham khao. Body: distance (km), estimatedTime (phut), rideType, promoCode (optional). Response: base, distance, time, surge, toll, discount, tax, total - deu la SO TIEN (distance = cuoc theo km, time = cuoc theo phut, surge = tien phu thu cao diem, khong phai he so).
3. POST /api/trips/ de tao chuyen va lay gia do Trip goi Pricing noi bo. Khong gui fareEstimate; RiderId trong body bi ghi de boi JWT.
4. Hien gia `fareEstimate` tu chuyen vua tao; Rider dong y thi POST /api/trips/{id}/confirm-price, body `{}`, response 202.
5. Cho `tripChanged` hoac poll GET /api/trips/{id}; 202 chi la da nhan yeu cau, khong co nghia da co tai xe.
6. Cancel: POST /api/trips/{id}/cancel, body `{}`, response 204 neu state cho phep.
7. Khi dropoff, cho PaymentPending/Paid. Mobile KHONG POST /api/payments de thanh toan lan nua.

Body tao chuyen:

```json
{
  "pickupAddress": "Diem don",
  "dropoffAddress": "Diem den",
  "pickupLatitude": 10.77,
  "pickupLongitude": 106.69,
  "distance": 2,
  "estimatedTime": 5,
  "rideType": "standard",
  "promoCode": null,
  "dropoffLatitude": 10.7798,
  "dropoffLongitude": 106.6990
}
```

`dropoffLatitude`/`dropoffLongitude` la tuy chon (de app ve ban do); neu gui thi phai gui ca hai va hop le, va chung tham gia hash idempotency. Trip detail va danh sach loi moi tra them `pickupLatitude, pickupLongitude, dropoffLatitude, dropoffLongitude` (co the null voi chuyen cu).

Response 201 va GET detail: `id, riderId, driverId, pickupAddress, dropoffAddress, fareEstimate, status, createdAt, updatedAt`. DriverId/updatedAt co the null. Khong coi gia tham khao truoc khi tao chuyen la gia da chot. Don vi tien va tinh hop le distance/time can duoc nhom Pricing chot truoc su dung tien that; khong tu nhan 1000 tren UI.

## Driver flow

| Endpoint | Body / response | Quyen |
|---|---|---|
| POST /api/drivers/location | latitude, longitude, isAvailable; 200: driverId, isAvailable, updatedAt | Driver; ID lay tu JWT |
| GET /api/trips/driver/invitations | 200: array {id, expiresAtUtc, pickupAddress, dropoffAddress, fareEstimate} | Chi loi moi cua chinh Driver |
| POST /api/trips/{id}/driver-accept | {}; 204 | Driver co loi moi con han |
| POST /api/trips/{id}/driver-reject | {}; 202 | Driver co loi moi con han |
| GET /api/trips/{id} | Trip detail | Driver da duoc giao chuyen |
| POST /api/trips/{id}/enroute | {}; 204 | Driver cua chuyen |
| POST /api/trips/{id}/pickup | {}; 204 | Driver cua chuyen |
| POST /api/trips/{id}/dropoff | {}; 202 | Driver cua chuyen |

Luu y danh sach loi moi dung truong `id`; SignalR driverInvitation dung `tripId`. Khong goi GET detail cho Driver chua nhan chuyen: policy hien tai chi cho Driver da duoc giao xem detail.

POST /api/matching/match chi cho Rider cua chuyen PriceConfirmed xem ung vien. Endpoint nay KHONG tao loi moi; luong chinh phai di qua confirm-price va messaging.

## Status va payment

Trip public HTTP va tripChanged hien serialize enum thanh SO:

| So | Ten |
|---|---|
| 0 | Requested |
| 1 | PriceConfirmed |
| 2 | DriverAccepted |
| 3 | EnRoute |
| 4 | PickedUp |
| 5 | DroppedOff |
| 6 | PaymentPending |
| 7 | Paid |
| 8 | Cancelled |
| 9 | Failed |

Mobile map enum tai mot noi, khong dung mapping cu truoc khi them PriceConfirmed. State khong biet phai hien trang thai an toan va dong bo lai, khong tu coi la Paid.

GET /api/payments/trip/{tripId} va GET /api/payments/{id}: Rider chu chuyen (hoac Admin), response `id, tripId, amount, status, idempotencyKey, createdAt, updatedAt, isTripSynced`. Payment status la CHUOI. 404 co the do payment chua duoc tao, can poll co gioi han. Dung Trip Paid lam ket qua cuoi luong messaging; khong doi isTripSynced=true, field nay thuoc duong HTTP sync cu.

Refund chi Admin. User JWT khong goi duoc pay/fail/payment-pending cua Trip. /internal/* khong public qua Gateway.

## SignalR qua Gateway

- Hub URL: /api/hubs/trips. Chi Rider/Driver. Client khong duoc chon user group.
- Transport ho tro qua route hien tai: WebSockets. Cau hinh client WebSockets; khong dua vao fallback SSE/LongPolling.
- Co the negotiate bang POST /api/hubs/trips/negotiate?negotiateVersion=1 voi Bearer header, hoac skip negotiation khi client ho tro WebSockets truc tiep.
- WebSocket handshake chap nhan Authorization header (native client) hoac access_token query (browser). Query token chi duoc doc tren dung hub path va WebSocket upgrade, khong dung cho negotiate/REST.
- Gateway xac thuc truoc proxy; Trip xac thuc lai. Hub yeu cau dong ket noi khi authentication het han; client lay token moi va reconnect.
- Khong log query access_token, khong chia se URL handshake. Reverse proxy/observability phai redact token.

| Event | Payload | Nguoi nhan |
|---|---|---|
| driverInvitation | tripId, pickupAddress, dropoffAddress, fareEstimate, expiresAtUtc | Driver duoc moi |
| tripChanged | Trip detail, status dang so | Rider va Driver da duoc giao |

Dang ky listeners truoc khi bat dau thao tac. SignalR khong co durable replay: sau reconnect, Rider GET detail theo ID da luu, Driver GET invitations va detail chuyen da nhan. HTTP la nguon dong bo lai; khong tu suy dien thanh cong khi mat ket noi.

## Loi va khoang trong con lai

- 400: du lieu khong hop le; 401: thieu/sai/het han token; 403: sai role/ownership/loi moi.
- 404: khong co tai nguyen; 409: state/conflict concurrency, can reload; 429: rate limit.
- 502/503/504: dependency/gateway loi; hien retry co gioi han. Error body hien co ca ProblemDetails va {error}; khong parse quyen/state tu noi dung chuoi loi.
- Tao chuyen chua co idempotency: khong tu dong retry POST create khi mat response. Active-trip recovery API chua co (task 2).
- Detail chua co vehicle/profile tai xe va paymentFailureReason; khong hien du lieu gia.
- Reservation/freshness Matching va business payment failure con task 3/4.
- Blocker build /internal/quote da sua sang CalculateAsync; freshness/reservation Matching van thuoc task 3.

## Kiem thu

```powershell
dotnet test tests/TripService.Tests --filter FullyQualifiedName~GatewayTransportTests
pwsh -File tests/Gateway.Security/gateway-security.ps1
```

Transport test can PowerShell 7 (`pwsh`), dung process Gateway that, Trip endpoints/hub that, EF InMemory va application service gia. Test cung chay signalr-helpers.ps1 tren WebSocket that. Test nay khong thay the DB/RabbitMQ smoke.

Smoke nghiep vu can stack da build, migrate, seed demo, DB test rieng va khong co tai xe khac gan diem test. Script thay doi vi tri/trang thai hai demo Driver, de ho offline khi ket thuc, tao user/chuyen/payment test va khong xoa du lieu. Khong chay tren production.

### Stack test tach biet

Chay tu repository root. Docker Engine phai hoat dong. Compose overlay doi ports; project name rieng tao volumes rieng, khong dung database dang phat trien. `!override` can Docker Compose 2.24.4 tro len. Chi dung credentials demo trong moi truong test local.

```powershell
docker compose -p swiftride-task1-test --env-file .env.example -f docker-compose.yml -f tests/Gateway.Security/compose.test.yml up --build -d
docker compose -p swiftride-task1-test --env-file .env.example -f docker-compose.yml -f tests/Gateway.Security/compose.test.yml ps
pwsh -NoProfile -File tests/Gateway.Security/gateway-security.ps1 -BaseUrl http://127.0.0.1:15100 -KeycloakUrl http://127.0.0.1:18180 -DirectTripUrl http://127.0.0.1:15101
docker compose -p swiftride-task1-test --env-file .env.example -f docker-compose.yml -f tests/Gateway.Security/compose.test.yml stop
```

Cho health/readiness cua services truoc khi chay smoke. Stop giu nguyen test volumes. Khong dung down -v de sua loi migration. Smoke nay kiem tra ca event driverInvitation/tripChanged qua Gateway va khong gui event sang Rider khac; khong chi test handshake.

Ket qua 26/10/2026: Auth smoke voi PostgreSQL 18 that dat; solution tests 144/144 dat khi Payment integration dung DB test that. Full business smoke chua chay vi Docker backend khong khoi dong duoc (socket dockerInference khong truy cap duoc). Khong coi Task 1 da nghiem thu full-stack cho den khi hai smoke tren stack tach biet dat.
