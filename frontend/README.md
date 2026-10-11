# SwiftRide Web

Giao diện Rider và Driver mobile-first, theo bộ mockup teal/nền sáng. React + TypeScript + Vite; CSS thuần; font Be Vietnam Pro đóng gói tại chỗ. Không cần .NET, Docker, API key hoặc backend để xem giao diện.

## Chạy local

Yêu cầu Node.js 22.12 trở lên.

```powershell
cd E:\SwiftRide\frontend
npm ci
npm run dev
```

Mở địa chỉ được Vite in ra, mặc định `http://127.0.0.1:5173`.

```powershell
npm run build
npm test
npm run preview
```

`build` kiểm tra TypeScript rồi tạo thư mục `dist/`. `test` chạy kiểm tra state machine bằng Node.js, không cần framework test.

## Tài khoản và cách demo

| Vai trò | Tài khoản | Mật khẩu |
|---|---|---|
| Rider | `rider@swiftride.demo` | `demo1234` |
| Driver | `driver@swiftride.demo` | `demo1234` |

Nút tài khoản mẫu chỉ điền form; bấm Đăng nhập để tiếp tục. Đây **không phải xác thực thật**, không dùng thông tin đăng nhập thật.

1. Rider chọn điểm đến, xem giá, xác nhận đặt xe.
2. Chuyển tab Tài xế để nhận chuyến, bắt đầu đi đón, xác nhận đón và trả khách.
3. Chuyển lại Hành khách để thanh toán mô phỏng, thử lỗi hoặc xác nhận thành công.
4. Nút **Kịch bản demo** cho xem trực tiếp A01, R01–R06, D01–D05 và các nhánh lỗi. Chọn màn hình sẽ thay thế kịch bản hiện tại.

Điều khiển “Mô phỏng” là công cụ trình diễn, không phải quyền người dùng trong hệ thống thật. Khi ở Rider có thể mô phỏng hành động tài xế mà không chuyển tab.

## Phạm vi

- 12 màn chính, với biến thể đã nhận/đang đi đón/đang di chuyển, offline, đã hủy, không có tài xế và thanh toán chưa rõ kết quả.
- Hộp thoại xác nhận hủy, đón khách và trả khách; chọn địa chỉ, tìm trong 4 địa điểm mẫu; chi tiết giá/chuyến.
- Một chuyến dùng chung giữa hai góc nhìn trong cùng tab; tiến trình lưu `localStorage` và khôi phục sau tải lại. Chưa đồng bộ nhiều tab/thiết bị.
- Bản đồ SVG minh họa có chọn điểm và zoom; địa hình, tọa độ, ETA và giá không phải dữ liệu thật. Không truy cập GPS, không tải map tiles, không có định tuyến thật.
- Giá mẫu cố định theo điểm đến, không triển khai công thức tính giá backend.
- Không gọi API, không lưu token, không gửi mật khẩu; không thực hiện giao dịch hoặc hoàn tiền thật.
- Khi Driver về trang chủ trước lúc thanh toán hoàn tất, hộp thoại giải thích rõ thao tác đặt lại kịch bản demo.

## Điểm nối backend sau này

| File | Trách nhiệm hiện tại / thay thế khi tích hợp |
|---|---|
| `src/lib/demo.ts` | Dữ liệu mẫu, kiểu trạng thái, reducer và lưu tiến trình. Thay chuyển trạng thái demo bằng kết quả API/server. |
| `src/components/Login.tsx` | Form đăng nhập giả lập. Thay kiểm tra tài khoản mẫu bằng xác thực thật. |
| `src/components/Rider.tsx` | Trình bày luồng Rider, nhận state và phát action. |
| `src/components/Driver.tsx` | Trình bày luồng Driver, nhận state và phát action. |
| `src/components/CityMap.tsx` | Bản đồ minh họa; thay bằng provider bản đồ, geocoding và route đã chọn. |
| `src/App.tsx` | Chọn vai trò, hiển thị màn theo trạng thái, dialog và bảng điều khiển demo. |
| `src/styles.css` | Layout responsive và thành phần dùng chung. |

Không coi reducer frontend là nguồn dữ liệu nghiệp vụ chính hoặc cơ chế bảo mật. Backend phải quyết định vai trò, quyền truy cập, báo giá, nhận/hủy chuyến, trạng thái và kết quả thanh toán. Khi tích hợp, bỏ chuyển vai trò tự do và các nút mô phỏng khỏi môi trường thật.

Frontend chưa giả định hợp đồng API mới. Team cần thống nhất DTO/trạng thái và gọi thông qua API Gateway; không gọi database từ trình duyệt. `paymentKey` chỉ minh họa một khóa ổn định giữa các lần thử lại, không phải cơ chế thanh toán production.

## Kiểm tra thủ công

- Thử đăng nhập sai rồi dùng tài khoản mẫu.
- Không cho báo giá khi thiếu điểm đến hoặc hai điểm trùng nhau.
- Thử tìm địa điểm không có kết quả; đóng dialog bằng Escape.
- Hoàn thành luồng cả hai vai trò; tải lại giữa chuyến.
- Hủy rồi xác nhận/quay lại; thử 3 lần tài xế từ chối.
- Thử ngoại tuyến, payment lỗi, kiểm tra lại và thành công.
- Kiểm tra 390px, 768px, 1440px và điều hướng bàn phím.
