# Kết quả đối chiếu Gateway/Auth — 23/10/2026

Nhánh: van. Kiểm tra theo checklist định tuyến, phân quyền Rider/Driver và xác thực downstream.

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
