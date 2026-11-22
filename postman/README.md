# SwiftRide - Cham API bang Postman

## Pham vi

Collection co 76 request: readiness, Keycloak discovery, dang ky Rider trong app qua Gateway (`POST /api/auth/register`, email moi moi lan chay), dang nhap qua Keycloak (Rider, Driver, Admin), role isolation, token gia/het han, quote, GPS, Trip idempotency, invitation/accept, enroute/pickup/dropoff, cho Paid, doc Payment, Admin refund, Driver reject doi ung vien, cancel, refresh/logout. Token lay tu Keycloak (`{{keycloakUrl}}/realms/{{realm}}/protocol/openid-connect/*`, client dev-only `swiftride-postman`, password grant); API nghiep vu chi goi qua API Gateway. Khong ep trang thai noi bo hoac tao Payment lan hai.

Canh bao: chi chay tren dev/test rieng. Moi lan chay dung 2 Rider demo co san, tao 3 Trip va 1 Payment (Payment bi refund o cuoi luong); doi online/offline cua 2 Driver demo. Khong xoa du lieu/volume. Neu run dung giua chung, xu ly Trip con dang chay va dat Driver offline bang request 71 va 60 (hoac 22). Khong export collection sau run kem token that.

## Chuan bi backend

1. Mo Docker Desktop. Tai root repository, tao `.env` tu `.env.example` neu chua co; giu nguyen file da co. Dien secret theo huong dan repository, khong commit `.env`.
2. Chay `docker compose up -d --build`. Doi `GET http://localhost:5100/health/ready` tra 200.
3. Dung database dev/test sach, khong co Driver khac online gan (10.77, 106.69), khong co Trip dang chiem 2 Driver demo. Khong dung `down -v` de sua loi.
4. Realm Keycloak import san `rider.demo@example.com`, `rider.other@example.com`, `driver.demo@example.com`, `driver.other@example.com` va `admin.demo@example.com`. Keycloak o `http://localhost:8180` (bien `keycloakUrl`). `seedPassword` phai khop `DEV_ACCOUNT_PASSWORD`; mac dinh dev la `SwiftRide-demo-2026!`. Realm chi import lan dau, doi `DEV_ACCOUNT_PASSWORD` sau do khong doi mat khau da co.

## Chay trong Postman

1. Import `SwiftRide.postman_collection.json` va `SwiftRide.local.postman_environment.json`.
2. Chon environment **SwiftRide Local**. Sua `baseUrl` neu can, khong co dau `/` cuoi; mac dinh `http://localhost:5100`.
3. Mo Collection Runner, chon **toan bo collection**, **1 iteration**, chay tu 00 den 75, dat **Delay 500 ms**. Khong chay song song.
4. Xem ket qua assertions. Token, Trip ID, Payment ID duoc lay tu response va luu trong collection variables; environment chi giu config.

Nut Send chi gui mot request. `setNextRequest` chi dieu khien flow trong Runner/Newman. Request 31, 52, 56 lap cho invitation va request 40 lap cho Paid, toi da `pollTimeoutSeconds` (mac dinh 45, cho phep 5..120). Sau loi HTTP hoac assertion, Runner dung; khong coi request 202 la da thanh toan. Keycloak khoa tam tai khoan sau 10 lan sai mat khau lien tiep (brute-force protection), tu mo sau toi da 15 phut.

Trang dang ky Keycloak (request 03) chi duoc kiem tra la mo duoc; user tu dang ky luon chi co role Rider. Request 16 xac nhan client mobile khong dung duoc password grant. Khong dung password production. Neu environment cu co token/ID trung ten, xoa cac bien do de khong che collection variables.

## Newman

Lenh tai Newman vao npm cache neu chua co, khong them dependency vao repository:

```powershell
npm exec --yes --package=newman@6.2.3 -- newman run postman/SwiftRide.postman_collection.json -e postman/SwiftRide.local.postman_environment.json --iteration-count 1 --bail --delay-request 500 --timeout-request 20000 --timeout-script 5000 --timeout 360000 --reporters cli,junit --reporter-junit-export TestResults/postman.xml
```

Override URL neu can: them `--env-var baseUrl=http://127.0.0.1:15100`. Khong truyen password that tren command line/log; sua gia tri local trong Postman. Khong public report co thong tin nhay cam. Khong dat iteration count lon hon 1 cho demo cham.

Kiem tra script khong can backend:

```powershell
node --test postman/collection.test.cjs
```

## Trinh tu nghiep vu

- Kiem tra discovery Keycloak; Rider/Driver/Admin dang nhap qua Keycloak; `/userinfo` tra dung role, Rider khong co role Driver/Admin.
- Driver phu offline, Driver chinh online; Rider tao Trip o Requested.
- Gui lai cung idempotency key/payload tra cung Trip; doi payload bi 409.
- Confirm price tra 202; polling invitation, Driver accept, enroute, pickup.
- Dropoff tra 202; Trip cho Payment qua RabbitMQ; polling den Paid.
- Payment Succeeded phai khop gia Trip va canonical idempotency key.
- Token sua role thanh Admin, token alg none va token co exp da qua deu bi 401.
- Rider khac bi chan doc Trip/Payment, Rider khong duoc refund hay force Paid.
- Admin refund Payment tra 200 `Refunded`; refund lan hai bi 409; Rider doc lai thay `Refunded`.
- Trip thu hai: Driver chinh nhan loi moi roi reject (202); Driver nay khong accept lai duoc (403); loi moi chuyen sang Driver phu, Trip van `PriceConfirmed` chua co Driver; Rider cancel va Driver phu ve offline.
- Tao Trip rieng de cancel.
- Chuyen da huy: khong co Payment (`GET /api/payments/trip/{id}` 404); thao tac khong con hop le (confirm-price) bi 409; doc lai van `Cancelled`, `updatedAt` khong doi.
- Cuoi cung refresh token qua Keycloak, logout (204), refresh cu bi 400 `invalid_grant`, dat Driver offline. Logout khong thu hoi access token da cap ngay lap tuc.

## Gioi han

- Tai khoan Admin chi co trong realm dev de cham refund; production cap Admin trong Keycloak Admin Console. Dang ky cong khai khong tu cap role. Client `swiftride-postman` (password grant) phai tat o production.
- Token het han duoc gia lap bang cach doi `exp` va giu chu ky cu, nen Gateway tu choi vi sai chu ky truoc khi kiem tra lifetime. Muon thay dung loi het han, dung access token that sau 15 phut (Access Token Lifespan cua realm) + 30 giay skew.
- Invitation timeout, message replay, broker/DB recovery, concurrent accept, ledger row count va SignalR WebSocket van dung `tests/System/messaging.py` va `tests/Gateway.Security/task1.ps1`; khong thay the bang polling HTTP.
- Payment la ledger simulation, khong phai giao dich ngan hang/PSP. Khong co public POST /payments de thanh toan thu cong.
- Khong tao endpoint history/profile/vehicle gia.
- Test offline chi xac minh JSON/script va polling, khong chung minh BE chay dung. Can chay tren stack that de nghiem thu end-to-end.
