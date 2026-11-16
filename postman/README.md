# SwiftRide - Cham API bang Postman

## Pham vi

Collection co 73 request: readiness, register/login (ca Admin seed), role isolation, token gia/het han, quote, GPS, Trip idempotency, invitation/accept, enroute/pickup/dropoff, cho Paid, doc Payment, Admin refund, Driver reject doi ung vien, cancel, refresh/logout. Chi goi API Gateway; khong ep trang thai noi bo hoac tao Payment lan hai.

Canh bao: chi chay tren dev/test rieng. Moi lan chay tao 2 Rider, 3 Trip va 1 Payment (Payment bi refund o cuoi luong); doi online/offline cua 2 Driver demo. Khong xoa du lieu/volume. Neu run dung giua chung, xu ly Trip con dang chay va dat Driver offline bang request 68 va 60 (hoac 22). Khong export collection sau run kem token that.

## Chuan bi backend

1. Mo Docker Desktop. Tai root repository, tao `.env` tu `.env.example` neu chua co; giu nguyen file da co. Dien secret theo huong dan repository, khong commit `.env`.
2. Chay `docker compose up -d --build`. Doi `GET http://localhost:5100/health/ready` tra 200.
3. Dung database dev/test sach, khong co Driver khac online gan (10.77, 106.69), khong co Trip dang chiem 2 Driver demo. Khong dung `down -v` de sua loi.
4. Compose seed `driver.demo@example.com`, `driver.other@example.com` va `admin.demo@example.com` (Admin, chi khi Environment=Development). Neu volume Auth da co tu truoc, restart `api-gateway` de seed them Admin. `seedPassword` phai khop `DEV_ACCOUNT_PASSWORD`; mac dinh dev la `SwiftRide-demo-2026!`. Seed khong reset password tai khoan da ton tai.

## Chay trong Postman

1. Import `SwiftRide.postman_collection.json` va `SwiftRide.local.postman_environment.json`.
2. Chon environment **SwiftRide Local**. Sua `baseUrl` neu can, khong co dau `/` cuoi; mac dinh `http://localhost:5100`.
3. Mo Collection Runner, chon **toan bo collection**, **1 iteration**, chay tu 00 den 72, dat **Delay 500 ms**. Khong chay song song.
4. Xem ket qua assertions. Token, Trip ID, Payment ID duoc lay tu response va luu trong collection variables; environment chi giu config.

Nut Send chi gui mot request. `setNextRequest` chi dieu khien flow trong Runner/Newman. Request 31, 52, 56 lap cho invitation va request 40 lap cho Paid, toi da `pollTimeoutSeconds` (mac dinh 45, cho phep 5..120). Sau loi HTTP hoac assertion, Runner dung; khong coi request 202 la da thanh toan. Neu run lai lien tiep gap 429, doi het cua so Auth rate limit 1 phut.

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
- Token sua role thanh Admin, token alg none va token co exp da qua deu bi 401.
- Rider khac bi chan doc Trip/Payment, Rider khong duoc refund hay force Paid.
- Admin refund Payment tra 200 `Refunded`; refund lan hai bi 409; Rider doc lai thay `Refunded`.
- Trip thu hai: Driver chinh nhan loi moi roi reject (202); Driver nay khong accept lai duoc (403); loi moi chuyen sang Driver phu, Trip van `PriceConfirmed` chua co Driver; Rider cancel va Driver phu ve offline.
- Tao Trip rieng de cancel; cuoi cung rotate/revoke refresh va dat Driver offline. Logout thu hoi refresh family, khong revoke access token ngay lap tuc.

## Gioi han

- Tai khoan Admin chi duoc seed o Development de cham refund; production van phai cap Admin qua kenh quan tri tin cay. Register cong khai khong tu cap role.
- Token het han duoc gia lap bang cach doi `exp` va giu chu ky cu, nen Gateway tu choi vi sai chu ky truoc khi kiem tra lifetime. Muon thay dung loi het han, dung access token that sau `Jwt:ExpiryTime` (mac dinh 15 phut) + 30 giay skew.
- Invitation timeout, message replay, broker/DB recovery, concurrent accept, ledger row count va SignalR WebSocket van dung `tests/System/messaging.py` va `tests/Gateway.Security/task1.ps1`; khong thay the bang polling HTTP.
- Payment la ledger simulation, khong phai giao dich ngan hang/PSP. Khong co public POST /payments de thanh toan thu cong.
- Khong tao endpoint history/profile/vehicle gia.
- Test offline chi xac minh JSON/script va polling, khong chung minh BE chay dung. Can chay tren stack that de nghiem thu end-to-end.
