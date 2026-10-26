# Kết quả đối chiếu Gateway/Auth — 23/10/2026

## Task 1 - 26/10/2026

### Trang thai truoc commit

- Docker Engine da khoi dong; stack test rieng da tao PostgreSQL, MongoDB va RabbitMQ.
- Build image ung dung bi dung khi o C con khoang 642 MB va Docker tra loi 500. Chua chay business smoke full-stack/restart; can tiep tuc sau khi giai phong dung luong.
- Commit theo yeu cau nguoi dung de luu tien do, khong phai xac nhan nghiem thu full-stack.

### Kiểm tra bổ sung trên database thật

- Đã sửa blocker /internal/quote sang CalculateAsync, truyền cancellation token. Solution test chạy thành công: **144 passed, 0 failed, 0 skipped**, Payment integration dùng PostgreSQL 18 test riêng.
- Auth smoke chạy với ApiGateway thật và PostgreSQL 18 riêng: register/role isolation, /auth/me, Cache-Control no-store, duplicate email, invalid login, refresh rotation/concurrency, replay family revocation, logout và protected route đều đạt.
- GatewayTransportTests bổ sung chạy PowerShell SignalR helper trên WebSocket thật: handshake, event nhận đúng và kiểm tra không rò event sang người khác đều đạt.
- Business smoke đã bổ sung kiểm tra SignalR invitation/tripChanged, chưa chạy trọn luồng trên stack thật. Thêm compose.test.yml với ports riêng; hướng dẫn chạy ở mobile-api-contract.md.
- **Chưa nghiệm thu full-stack:** Docker Desktop khởi động lỗi tại socket dockerInference (`The file cannot be accessed by the system`). Không thay đổi/xóa Docker volumes hay database hiện có. Chưa chạy MongoDB/RabbitMQ business smoke; không đồng nhất 144 test đạt với toàn bộ nghiệp vụ đạt.
- Cảnh báo NU1903 của SQLite test dependency vẫn còn, thuộc task 5.

Các bullet dưới ghi lại lần kiểm tra trước khi bổ sung DB thật.

- Sửa route WebSocket sang ws ở Ocelot và Compose; xác thực JWT/role trước proxy WebSocket, chỉ chấp nhận query token tại hub upgrade, đóng hub khi authentication hết hạn.
- Thêm GatewayTransportTests: chạy process ApiGateway thật, Trip HTTP endpoints/hub thật trên Kestrel, DB InMemory và application service giả. Kiểm tra HTTP ownership/role, lời mời riêng từng Driver, negotiate, WebSocket header/query token, token hết hạn và cô lập SignalR recipients.
- Trip test suite: 29 passed, 0 failed, 0 skipped. Không dùng kết quả này để khẳng định business workflow trên PostgreSQL/RabbitMQ đã đạt.
- Gateway security smoke đã đổi sang confirm-price, chờ lời mời và Paid từ messaging, không tạo payment lần hai. Tài liệu mobile ở mobile-api-contract.md.
- Docker Engine chưa chạy; chưa chạy lại Auth refresh/logout smoke hoặc business smoke trên stack thật. Matching còn lỗi build Calculate/CalculateAsync thuộc task 3. Cảnh báo NU1903 của SQLite test dependency thuộc task 5 vẫn còn.

Các phần bên dưới lưu kết quả lịch sử trước task 1.

Nhánh: van. Kiểm tra theo checklist định tuyến, phân quyền Rider/Driver và xác thực downstream.

## Kiểm tra lại sau merge trên khanh — 23/10/2026

- Đã sửa conflict ở project/config/docs, bổ sung shared security cho Matching, anchor secrets và volume RabbitMQ trong Compose.
- Đã gắn TripAccessFilter, giới hạn tạo chuyến cho Rider và lấy RiderId từ JWT. User JWT phải có sub là GUID khác rỗng; user/service signing keys phải khác nhau.
- Build solution thành công. Test solution: 105 passed, 0 failed, 7 skipped vì chưa cấu hình database/broker integration.
- TripSecurityTests chạy HTTP trên Kestrel với application service giả, kiểm tra role, danh tính từ JWT, token sai/hết hạn và cách ly service token; không xác nhận ownership bằng database thật.
- docker compose config --quiet và git diff --check đạt. Docker Engine chưa chạy, chưa chạy lại smoke Gateway/Auth hoặc full-stack end-to-end.
- Smoke script đã bổ sung tọa độ bắt buộc khi tạo chuyến. Bỏ kỳ vọng API danh sách lời mời chưa triển khai; vẫn kiểm tra accept bị chặn trước khi mời và thành công sau khi Matching tạo lời mời.

Các kết quả bên dưới là báo cáo cũ của nhánh van, không phải xác nhận cho bản merge hiện tại.

## Đã bổ sung

- Ocelot map /api/trips/* → /trips/*, /api/payments/* → /payments/*.
- Explicit routes cho /api/matching/quote, /api/matching/match và /api/drivers/location.
- Public register chỉ tạo Rider; GET /auth/me; Development seed Rider và hai Driver.
- Forward bearer JWT để từng service xác thực độc lập; X-User-* không dùng làm nguồn quyền.
- Trip ownership, Driver invitations còn hạn 2 phút, xmin concurrency ngăn double acceptance.
- Matching kiểm tra chủ chuyến, vị trí Driver lấy từ JWT; tạo invitation qua API nội bộ.
- Payment kiểm tra chủ chuyến trước create/read/retry; refund/reconcile chỉ Admin.
- Dedicated internal JWT cho Payment/Matching → Trip. User không tự gọi pay/fail/payment-pending.
- Driver dropoff chuyển sang PaymentPending cùng một lần lưu.
- Migration DriverInvitationsAndConcurrency, cấu hình local migration/seed trong Compose.
- Bỏ ví dụ tin identity header, thay bằng ví dụ validate JWT.
- .dockerignore tránh gửi database/cache/secrets local vào Docker build context.

## Kiểm chứng thực tế

SDK 10.0.401; Gateway/Ocelot + Trip + Payment + Matching chạy local với PostgreSQL 18 và MongoDB 8.2.12 thật. Database test riêng, không sửa dữ liệu vận hành.

- Build cả bốn service: thành công, 0 errors/0 warnings.
- tests/Gateway.Security/gateway-security.ps1: 47 HTTP checks đạt.
- Các kiểm tra qua Gateway gồm: chưa đăng nhập bị chặn; /auth/me; không tự cấp Admin; body RiderId/DriverId không thay danh tính; chặn đọc/hủy/thanh toán chuyến người khác; location đúng Driver; quote/match/invitation; không nhận chuyến khi chưa được mời; Driver khác không cập nhật chuyến đã giao; hai Driver cạnh tranh chỉ một 204/một 409; owner tự hủy; thanh toán đồng bộ qua service JWT; chặn refund/system transitions với user JWT; /internal/* không public.
- Gọi trực tiếp Trip bằng X-User-* giả, không JWT: 401.
- tests/ApiGateway.Smoke/auth-smoke.ps1: đạt register/login/rotation/replay/concurrent refresh/logout.
- Test solution: 77 succeeded, 0 failed, 0 skipped; bao gồm Payment integration tests với PostgreSQL _test riêng.
- docker compose config --quiet, PowerShell parser và git diff --check: đạt.

Test suite Payment ban đầu có race khi hai lớp cùng migrate database mới; đã đưa hai lớp vào cùng xUnit collection để serialize setup. Những races Task.WhenAll trong từng test vẫn chạy để kiểm tra concurrency thật.

Auth rate limit 20 requests/phút/IP: chạy nhiều suite liên tiếp có thể trả 429. Smoke Auth được chạy lại với cửa sổ rate limiter mới và đạt.

## Giới hạn

Docker Engine không chạy nên chưa build/start toàn bộ Compose stack trong lần kiểm tra này. Các luồng nghiệp vụ đã kiểm tra với processes local và database thật, không dùng echo thay cho service.

Driver production cần provisioning sau xác minh bằng công cụ quản trị tin cậy; demo seed không thay thế onboarding thật. Payment là mô phỏng ledger, chưa có PSP thật; FareEstimate hiện còn nhận từ create-trip, cần ràng buộc với giá server trước triển khai tài chính thực tế. Access token chưa có cơ chế revoke tức thời; quyền cũ có thể còn hiệu lực tới expiry.

Tài khoản/chuyến/payment test được giữ trong .data và database test riêng (gitignored). Đã dừng các tiến trình test do lần làm việc này tạo. Không xóa volume/container/database của người dùng.
