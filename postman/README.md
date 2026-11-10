# SwiftRide - Cham API bang Postman

## Pham vi

Collection co 52 request: readiness, register/login, role isolation, quote, GPS, Trip idempotency, invitation/accept, enroute/pickup/dropoff, cho Paid, doc Payment, cancel, refresh/logout. Chi goi API Gateway; khong ep trang thai noi bo hoac tao Payment lan hai.

Canh bao: chi chay tren dev/test rieng. Moi lan chay tao 2 Rider, 2 Trip va 1 Payment; doi online/offline cua 2 Driver demo. Khong xoa du lieu/volume. Neu run dung giua chung, xu ly Trip con dang chay va dat Driver offline bang request 48 va 17. Khong export collection sau run kem token that.

## Chuan bi backend

1. Mo Docker Desktop. Tai root repository, tao `.env` tu `.env.example` neu chua co; giu nguyen file da co. Dien secret theo huong dan repository, khong commit `.env`.
2. Chay `docker compose up -d --build`. Doi `GET http://localhost:5100/health/ready` tra 200.
3. Dung database dev/test sach, khong co Driver khac online gan (10.77, 106.69), khong co Trip dang chiem 2 Driver demo. Khong dung `down -v` de sua loi.
4. Compose seed `driver.demo@example.com` va `driver.other@example.com`. `seedPassword` phai khop `DEV_ACCOUNT_PASSWORD`; mac dinh dev la `SwiftRide-demo-2026!`. Seed khong reset password tai khoan da ton tai.

## Chay trong Postman

1. Import `SwiftRide.postman_collection.json` va `SwiftRide.local.postman_environment.json`.
2. Chon environment **SwiftRide Local**. Sua `baseUrl` neu can, khong co dau `/` cuoi; mac dinh `http://localhost:5100`.
3. Mo Collection Runner, chon **toan bo collection**, **1 iteration**, chay tu 00 den 51, dat **Delay 500 ms**. Khong chay song song.
4. Xem ket qua assertions. Token, Trip ID, Payment ID duoc lay tu response va luu trong collection variables; environment chi giu config.

Nut Send chi gui mot request. `setNextRequest` chi dieu khien flow trong Runner/Newman. Request 26 lap cho invitation va request 35 lap cho Paid, toi da `pollTimeoutSeconds` (mac dinh 45, cho phep 5..120). Sau loi HTTP hoac assertion, Runner dung; khong coi request 202 la da thanh toan. Neu run lai lien tiep gap 429, doi het cua so Auth rate limit 1 phut.

Collection tao Rider moi voi email random; role Admin gia trong body register phai bi bo qua. `testPassword` chi la mat khau test. Khong dung password production. Neu environment cu co token/ID trung ten, xoa cac bien do de khong che collection variables.

## Newman

Lenh tai Newman vao npm cache neu chua co, khong them dependency vao repository:

```powershell
npm exec --yes --package=newman@6.2.3 -- newman run postman/SwiftRide.postman_collection.json -e postman/SwiftRide.local.postman_environment.json --delay-request 500 --timeout-request 15000 --timeout-script 5000 --timeout 240000 --reporters cli,junit --reporter-junit-export TestResults/postman.xml
```

Override URL neu can: them `--env-var baseUrl=http://127.0.0.1:15100`. Khong truyen password that tren command line/log; sua gia tri local trong Postman. Khong public report co thong tin nhay cam. Khong dat iteration count lon hon 1 cho demo cham.

Kiem tra script khong can backend:

```powershell
node --test postman/collection.test.cjs
```

## Trinh tu nghiep vu

- Rider register/login; Driver login bang tai khoan seed.
- Driver phu offline, Driver chinh online; Rider tao Trip o Requested.
- Gui lai cung idempotency key/payload tra cung Trip; doi payload bi 409.
- Confirm price tra 202; polling invitation, Driver accept, enroute, pickup.
- Dropoff tra 202; Trip cho Payment qua RabbitMQ; polling den Paid.
- Payment Succeeded phai khop gia Trip va canonical idempotency key.
- Rider khac bi chan doc Trip/Payment, Rider khong duoc refund hay force Paid.
- Tao Trip rieng de cancel; cuoi cung rotate/revoke refresh va dat Driver offline. Logout thu hoi refresh family, khong revoke access token ngay lap tuc.

## Gioi han

- Refund thanh cong can Admin duoc cap qua kenh quan tri tin cay; collection chi test Rider refund bi 403. Khong seed Admin/tu cap role.
- Driver reject/timeout, message replay, broker/DB recovery, concurrent accept, ledger row count va SignalR WebSocket van dung `tests/System/messaging.py` va `tests/Gateway.Security/task1.ps1`; khong thay the bang polling HTTP.
- Payment la ledger simulation, khong phai giao dich ngan hang/PSP. Khong co public POST /payments de thanh toan thu cong.
- Khong tao endpoint history/profile/vehicle gia.
- Test offline chi xac minh JSON/script va polling, khong chung minh BE chay dung. Can chay tren stack that de nghiem thu end-to-end.
