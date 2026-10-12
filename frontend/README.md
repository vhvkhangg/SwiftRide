# SwiftRide Frontend

> Bản web tham chiếu cũ. Hướng phát triển chính đã chuyển sang hai ứng dụng React Native trong `../mobile/`. Xem `../mobile/README.md`. Mobile không import hoặc chạy mã nguồn web này.

Hai ứng dụng web độc lập trong cùng repository: **Rider** và **Driver**. Mỗi app có `package.json`, HTML entry, React entry, màn nghiệp vụ, cấu hình Vite, port và output build riêng. Không chọn ứng dụng bằng `/rider` hoặc `/driver` nữa.

React + TypeScript + Vite; CSS thuần; font Be Vietnam Pro đóng gói tại chỗ. Chỉ chia sẻ component nền, style và logic demo để tránh lặp mã. Không cần .NET, Docker hoặc API key để chạy giao diện.

## Cài dependency

Yêu cầu Node.js 22.12 trở lên. Cài một lần từ thư mục frontend bằng npm workspaces:

```powershell
cd E:\SwiftRide\frontend
npm ci
```

Không chạy `npm ci` riêng trong từng app; dependency và lockfile được quản lý ở thư mục frontend.

## Chạy hai ứng dụng

Mở hai terminal tại `E:\SwiftRide\frontend`:

```powershell
# Terminal 1: Rider — http://127.0.0.1:5173/
npm run dev:rider
```

```powershell
# Terminal 2: Driver — http://127.0.0.1:5174/
npm run dev:driver
```

Cũng có thể chạy `npm run dev` trong `apps/rider` hoặc `apps/driver`. `npm run dev` ở thư mục frontend chỉ chạy Rider. Cấu hình `strictPort` báo lỗi khi port đang bị chiếm, không tự đổi port làm nhầm ứng dụng. Dừng một app không dừng app còn lại.

## Build và triển khai riêng

```powershell
npm run build:rider
npm run build:driver
```

| Ứng dụng | Output để deploy | Preview bản build |
|---|---|---|
| Rider | `apps/rider/dist/` | `npm run preview:rider` — port 4173 |
| Driver | `apps/driver/dist/` | `npm run preview:driver` — port 4174 |

`npm run build` build cả hai. Mỗi thư mục `dist` chứa đủ HTML, JavaScript, CSS, font và favicon cho app đó; deploy lên hai site/domain riêng. Rider không import màn nghiệp vụ Driver và ngược lại. Không deploy thư mục `frontend/dist` cũ nếu còn tồn tại từ bản demo chung.

Giữ cùng repo không có nghĩa dùng cùng ứng dụng chạy thật: hai process dev, hai entry và hai artifact deploy riêng. Component dùng chung được đóng gói vào từng artifact, không cần một server frontend thứ ba.

## Tài khoản demo

| App | Tài khoản | Mật khẩu |
|---|---|---|
| Rider — port 5173 | `rider@swiftride.demo` | `demo1234` |
| Driver — port 5174 | `driver@swiftride.demo` | `demo1234` |

Mỗi app chỉ có nút điền tài khoản mẫu của mình và từ chối tài khoản bên kia. Đây **không phải xác thực hoặc phân quyền thật**. Không nhập thông tin đăng nhập thật.

Rider mô phỏng đặt xe, tài xế nhận/đón/trả khách rồi thanh toán. Driver mô phỏng lời mời, nhận/đón/trả khách và nhận kết quả thanh toán. Bảng **Kịch bản demo** chỉ liệt kê màn của app đang mở.

Hai demo **chưa đồng bộ chuyến**: đặt xe ở Rider không tạo lời mời ở Driver. Đồng bộ sẽ do backend cung cấp sau. Điều khiển “Mô phỏng” là công cụ trình diễn, không phải quyền người dùng trong hệ thống thật.

## Cấu trúc

```text
frontend/
  apps/
    rider/
      src/App.tsx, main.tsx, Rider.tsx, screens.ts
      index.html, package.json, tsconfig.json, vite.config.ts
    driver/
      src/App.tsx, main.tsx, Driver.tsx, screens.ts
      index.html, package.json, tsconfig.json, vite.config.ts
  shared/
    TripShell.tsx
    components/           # Login, CityMap, thanh phan UI
    lib/                  # State machine, du lieu va tai khoan demo
    fonts.css, styles.css
  public/favicon.svg
  tests/
  package.json, package-lock.json, tsconfig.json
```

Sửa màn Rider ở `apps/rider/src/Rider.tsx`; sửa màn Driver ở `apps/driver/src/Driver.tsx`. Hai app chủ động truyền màn của mình vào `shared/TripShell.tsx`; shell không import hoặc điều hướng giữa hai màn nghiệp vụ.

## Giới hạn và nối backend

- Phiên/tiến trình dùng khóa `swiftride-rider-ui-v1` và `swiftride-driver-ui-v1`. Đăng xuất/đặt lại một app không ảnh hưởng app còn lại. Chưa đồng bộ nhiều tab/thiết bị. Dữ liệu cũ không bị xóa hoặc tự chuyển giữa các origin.
- Bản đồ SVG, 4 địa điểm, ETA và giá đều là dữ liệu mẫu; không GPS, geocoding hoặc định tuyến thật.
- Không gọi API, không lưu token, không gửi mật khẩu hoặc trừ tiền. Giá cố định, chưa thực hiện công thức backend.
- Bỏ tài khoản mẫu và điều khiển mô phỏng khi tích hợp. Backend quyết định quyền truy cập, báo giá, matching, chuyển trạng thái và kết quả thanh toán; frontend không phải cơ chế bảo mật.
- Cả hai app gọi API Gateway. Khi nối BE, cấu hình origin/CORS hoặc reverse proxy cho từng app. Chưa tạo hợp đồng API giả định.
- `paymentKey` chỉ minh họa khóa ổn định giữa các lần thử lại; không phải cơ chế thanh toán production.

## Kiểm tra

```powershell
npm test

# Sau khi build, kiem tra them bundle khong chua man cua app con lai:
$env:CHECK_BUILDS = '1'
npm test
Remove-Item Env:CHECK_BUILDS
```

Kiểm tra thủ công hai URL cùng lúc: đăng nhập sai ứng dụng, reset/logout độc lập, xem danh sách màn demo riêng, hoàn thành từng luồng, reload giữa chuyến, thử hủy/từ chối/thanh toán lỗi và kiểm tra responsive 390px/1440px.
