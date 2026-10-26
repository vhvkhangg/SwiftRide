# Mobile API contract - 2026-10-26

Base URL: Gateway, local `http://localhost:5100`. Thiet bi that dung IP LAN cua may chay Gateway, khong dung localhost. Production bat buoc HTTPS/WSS.

Tai lieu mo ta code hien tai, khong coi API du kien la da co. HTTP/SignalR transport da duoc test qua Gateway that voi Trip test host; nghiep vu DB/broker that van can smoke full-stack.

## Authentication

Moi business HTTP request gui `Authorization: Bearer <accessToken>`. Khong gui token qua query string cho HTTP API. Khong dung X-User-* lam danh tinh.

| Endpoint | Body | Thanh cong |
|---|---|---|
| POST /auth/register | email, password | 201: id, email; luon tao Rider |
| POST /auth/login | email, password | 200: accessToken, refreshToken, accessTokenExpiresAtUtc |
| GET /auth/me | Khong body; Bearer token | 200: id, email, roles[] |
| POST /auth/refresh-token | refreshToken | 200: cap token moi |
| POST /auth/revoke | refreshToken | 204 |

Password 12-128 ky tu. Email trung: 409. Dang nhap sai/refresh khong hop le: 401. Rate limit Auth: 20 requests/phut/IP/process, vuot nguong: 429.

Chi cho mot refresh request chay tai mot thoi diem. Thay ca cap token sau refresh; khong retry bang refresh token cu. Replay token cu thu hoi family. Logout dong SignalR va xoa token tren client; access token da cap van co the hop le den expiry.

Driver duoc provision boi backend/admin; public register khong tu cap Driver/Admin. Demo development: rider.demo@example.com, driver.demo@example.com, driver.other@example.com.

## Rider flow

1. GET /auth/me, xac nhan role Rider.
2. POST /api/matching/quote de hien gia tham khao. Body: distance (km), estimatedTime (phut), rideType, promoCode (optional). Response: base, distance, time, surge, toll, discount, tax, total.
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
  "promoCode": null
}
```

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
# Cho it nhat mot phut giua hai suite Auth de tranh 429.
pwsh -File tests/ApiGateway.Smoke/auth-smoke.ps1
```

Transport test can PowerShell 7 (`pwsh`), dung process Gateway that, Trip endpoints/hub that, EF InMemory va application service gia. Test cung chay signalr-helpers.ps1 tren WebSocket that. Test nay khong thay the DB/RabbitMQ smoke.

Smoke nghiep vu can stack da build, migrate, seed demo, DB test rieng va khong co tai xe khac gan diem test. Script thay doi vi tri/trang thai hai demo Driver, de ho offline khi ket thuc, tao user/chuyen/payment test va khong xoa du lieu. Khong chay tren production.

### Stack test tach biet

Chay tu repository root. Docker Engine phai hoat dong. Compose overlay doi ports; project name rieng tao volumes rieng, khong dung database dang phat trien. `!override` can Docker Compose 2.24.4 tro len. Chi dung credentials demo trong moi truong test local.

```powershell
docker compose -p swiftride-task1-test --env-file .env.example -f docker-compose.yml -f tests/Gateway.Security/compose.test.yml up --build -d
docker compose -p swiftride-task1-test --env-file .env.example -f docker-compose.yml -f tests/Gateway.Security/compose.test.yml ps
pwsh -NoProfile -File tests/Gateway.Security/gateway-security.ps1 -BaseUrl http://127.0.0.1:15100 -DirectTripUrl http://127.0.0.1:15101
Start-Sleep -Seconds 65
pwsh -NoProfile -File tests/ApiGateway.Smoke/auth-smoke.ps1 -BaseUrl http://127.0.0.1:15100
docker compose -p swiftride-task1-test --env-file .env.example -f docker-compose.yml -f tests/Gateway.Security/compose.test.yml stop
```

Cho health/readiness cua services truoc khi chay smoke. Stop giu nguyen test volumes. Khong dung down -v de sua loi migration. Smoke nay kiem tra ca event driverInvitation/tripChanged qua Gateway va khong gui event sang Rider khac; khong chi test handshake.

Ket qua 26/10/2026: Auth smoke voi PostgreSQL 18 that dat; solution tests 144/144 dat khi Payment integration dung DB test that. Full business smoke chua chay vi Docker backend khong khoi dong duoc (socket dockerInference khong truy cap duoc). Khong coi Task 1 da nghiem thu full-stack cho den khi hai smoke tren stack tach biet dat.
