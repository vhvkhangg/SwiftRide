# SwiftRide Mobile

Hai ứng dụng React Native + Expo + TypeScript, viết lại giao diện bằng `View`, `Text`, `Pressable`, `TextInput`, `ScrollView`, `Modal` và `Switch`. Không dùng HTML/CSS của bản web, Capacitor hay WebView. Giữ bảng màu teal và font Be Vietnam Pro của thiết kế trước.

| | Rider | Driver |
|---|---|---|
| Thư mục | `apps/rider` | `apps/driver` |
| Tên cài đặt | SwiftRide Rider | SwiftRide Driver |
| Android / iOS ID | `com.swiftride.rider` | `com.swiftride.driver` |
| Metro dev port | 8081 | 8082 |
| Tài khoản demo | `rider@swiftride.demo` | `driver@swiftride.demo` |

Mật khẩu demo: `demo1234`. Nút điền tài khoản chỉ điền form; cần bấm Đăng nhập. Không dùng mật khẩu thật. Phiên đăng nhập chỉ ở bộ nhớ; mở lại app cần đăng nhập demo lại, nhưng tiến trình chuyến vẫn lưu.

## Chạy trên điện thoại

### Chế độ demo / API

Mặc định `EXPO_PUBLIC_APP_MODE=demo`. Nút chính nằm cố định phía trên thanh tab; lỗi lưu chuyến có nút thử lưu lại, không cần thao tác lại chuyến. Các nút chặn bấm nhanh liên tiếp và hỗ trợ trạng thái chờ cho tác vụ bất đồng bộ.

Để chuẩn bị môi trường API, đặt biến môi trường trước khi chạy Expo (áp dụng riêng cho mỗi terminal):

```powershell
$env:EXPO_PUBLIC_APP_MODE = 'api'
$env:EXPO_PUBLIC_GATEWAY_URL = 'http://localhost:5000'
npm run preview:rider
```

URL trên chỉ là ví dụ; dùng địa chỉ Gateway của môi trường thực tế. Trên điện thoại, `localhost` là điện thoại, không phải máy chạy BE. Không đặt token hoặc bí mật vào biến `EXPO_PUBLIC_*`.

**API hiện bị chặn có chủ đích:** chỉ hiện thông báo chưa tích hợp, không mở đăng nhập demo, đọc/ghi chuyến demo hoặc hiện nút mô phỏng. Chưa có Auth thật hoặc endpoint nghiệp vụ được nối. Sai mode/URL cũng bị chặn thay vì âm thầm quay về demo. Đổi cấu hình cần khởi động lại Expo. Để quay lại demo: `$env:EXPO_PUBLIC_APP_MODE = 'demo'`.

`shared/gateway.ts` tập trung request, timeout, hủy và lỗi HTTP/mạng; không tự retry, không suy đoán endpoint/response nghiệp vụ. Kết quả JSON là `unknown`, cần kiểm tra theo contract khi tích hợp. Không báo thành công sau timeout; xác minh chuyến/giao dịch trước khi gửi lại. Module này chưa được gọi từ màn demo.

Yêu cầu Node.js >= 22.13, npm và Expo Go tương thích SDK 58. Cài dependencies **một lần tại thư mục `mobile/`**, không cài riêng từng app.

```powershell
cd E:\SwiftRide\mobile
npm ci
npm run start:rider
```

Mở terminal thứ hai:

```powershell
cd E:\SwiftRide\mobile
npm run start:driver
```

Điện thoại và máy tính dùng cùng mạng Wi-Fi tin cậy. Mở từng dự án bằng QR trong Expo Go. Expo Go là môi trường xem trước; **hai icon ứng dụng riêng chỉ xuất hiện khi build/cài hai app riêng**, không phải khi quét QR trong Expo Go. Nếu Expo Go không hỗ trợ SDK đang dùng, dùng bản Expo Go phù hợp hoặc build native. Chỉ mở firewall cho Metro trên mạng riêng tin cậy.

## Hai ứng dụng Android riêng

Cần Android Studio, Android SDK, JDK phù hợp React Native, cấu hình `ANDROID_HOME`, emulator đang chạy hoặc điện thoại bật USB debugging. Kiểm tra `adb devices` nhận thiết bị. Tắt Metro preview đang chiếm cùng port trước khi chạy các lệnh này.

```powershell
cd E:\SwiftRide\mobile
npm run android:rider
```

Trong terminal khác, chạy `npm run android:driver`. `expo run:android` tạo native project nếu chưa có, build debug và cài app. Hai app có application ID khác nhau nên không ghi đè nhau. Native project sinh tự động trong mỗi thư mục app và được Git bỏ qua; sửa cấu hình Expo, không sửa tay mã nguồn sinh tự động.

Build APK chia sẻ bằng EAS (tùy chọn, cần tài khoản Expo, mạng và quota build):

```powershell
cd E:\SwiftRide\mobile\apps\rider
npx eas-cli login
npx eas-cli build --platform android --profile preview
```

Lặp lại từ `mobile/apps/driver`. Lần đầu EAS yêu cầu liên kết/tạo project riêng cho từng app và cấu hình signing. Không đưa keystore, mật khẩu signing hoặc token vào Git. Chưa tạo project EAS hoặc tải source lên dịch vụ thay người dùng. Profile `preview` tạo APK; `production` tạo AAB. Đổi application ID trước khi phát hành chính thức nếu cần namespace khác.

iOS có bundle ID riêng; build cục bộ cần macOS/Xcode. Chưa kiểm thử trên thiết bị iOS.

## Xem nhanh giao diện trên máy tính

```powershell
npm run preview:rider
npm run preview:driver
```

Địa chỉ mặc định: `http://localhost:8081` và `http://localhost:8082`. Đây là **React Native Web phục vụ kiểm tra bố cục**, không phải bản web Vite cũ và không thay thế kiểm thử native. Hai lệnh cần hai terminal. Không dùng đồng thời `start` và `preview` cho cùng app trên cùng port.

## Luồng đã có

- Rider: đăng nhập, chọn/tìm điểm đón và đến, xem giá, xác nhận/từ chối giá, tìm tài xế, theo dõi nhận chuyến/đi đón/đang đi, hủy trước khi đón, thanh toán, kết quả chuyến.
- Driver: đăng nhập, bật/tắt nhận chuyến, lời mời nhận/từ chối, đi đón, xác nhận đã đón/đã trả, hủy trước đón, chờ và xem kết quả thanh toán.
- Hai app có màn tài khoản, đăng xuất, bảng kịch bản riêng; không có nút đổi Rider/Driver.
- Bộ chọn địa điểm, xác nhận thao tác và kịch bản dùng native Modal. Có safe area, tránh bàn phím, nhãn accessibility, vùng chạm >= 48px và xử lý nút Back Android cho màn chính.
- AsyncStorage lưu chuyến, không lưu mật khẩu/token. Key và dữ liệu riêng từng app; snapshot được kiểm tra version, vai trò, kiểu dữ liệu và trạng thái. Không đọc được bộ nhớ thì chặn ghi để tránh ghi đè chuyến cũ.
- Payment giữ nguyên mã giao dịch khi thử lại, tối đa 3 lần thử; hết lượt vẫn chờ xử lý, không báo thành công giả.

## Giới hạn demo

- **Chưa gọi API, kể cả Auth ở Gateway đã có trong repo. Không có xác thực thật.**
- Hai app **chưa đồng bộ chuyến**. Dùng điều khiển có nhãn Mô phỏng để tạo phản hồi phía còn lại. Không xem đây là demo end-to-end qua backend.
- Bản đồ SVG minh họa; chỉ có 4 địa điểm mẫu, không có GPS, map tiles, dẫn đường, push hoặc background tracking.
- Giá/ETA/quãng đường là dữ liệu mẫu, không triển khai công thức giá của BE; không có giao dịch, ví hoặc thu nhập thực.
- Không thêm chat, cuộc gọi, lịch sử, đăng ký hoặc dashboard thu nhập ngoài phạm vi luồng đã có.
- `expo export` kiểm tra bundle JavaScript/Hermes, **không tạo APK và không chứng minh native build chạy trên thiết bị**.

## Cấu trúc và nối BE sau

```text
mobile/
  apps/rider/       entry, UI Rider, app.json, eas.json, icon
  apps/driver/      entry, UI Driver, app.json, eas.json, icon
  shared/          native UI dùng chung, domain demo, lưu chuyến
  tests/           kiểm tra Node.js, không thêm test framework
```

Mỗi app chỉ import màn hình thuộc vai trò của mình. Shared không import ngược màn Rider/Driver; chỉ chia sẻ thành phần nền. `metro.config.js` bổ sung thư mục `shared/` vào watchFolders vì đây không phải một workspace package, bảo đảm sửa component dùng chung cũng cập nhật bundle.

Khi nối BE, thay các hành động mô phỏng bằng lời gọi API Gateway và trạng thái do server trả về. Không dùng reducer hoặc tài khoản demo làm nguồn xác thực nghiệp vụ. Token thật cần kho bảo mật native, không AsyncStorage. Điện thoại thật phải gọi địa chỉ LAN/HTTPS của Gateway, không phải `localhost` trên máy tính; kiểm tra quyền truy cập tài nguyên tại server.

## Kiểm tra

```powershell
cd E:\SwiftRide\mobile
npm run typecheck
npm test
npm run export:android
```

Test bao phủ luồng Rider/Driver, offline, địa điểm không hợp lệ, chuyển trạng thái, hủy, giới hạn từ chối/retry, chống settle lặp, snapshot hỏng/khác app, đăng nhập demo riêng và cấu hình hai ứng dụng.

`tests/ui.test.mjs` thêm kiểm tra component bằng React/React DOM thật, React Native Web và jsdom (chỉ dev dependency), chạy trong `npm test`. TypeScript có sẵn chuyển TSX khi chạy test; không sinh file build. AsyncStorage, SVG và safe-area được thay bằng test doubles. Test bao phủ bấm lặp, khóa/loading, tác vụ thất bại và thử lại, giữ form đăng nhập, ẩn/hiện mật khẩu, nút theo vai trò/trạng thái, xác nhận trả khách, lỗi đọc không ghi đè dữ liệu, lỗi lưu/thử lưu, thứ tự ghi và unmount khi đang đọc.

jsdom không có layout engine hoặc bàn phím native; cảnh báo thiếu ResizeObserver trong test không phải bằng chứng lỗi bố cục. Các test này không chứng minh safe area, VoiceOver, Dynamic Type hoặc trải nghiệm iPhone thật đã đạt.

### Checklist nghiệm thu UI trên thiết bị

- [ ] iPhone thật: mở bàn phím ở đăng nhập/tìm địa điểm; trường đang nhập và nút đóng vẫn truy cập được, không mất dữ liệu khi đóng bàn phím.
- [ ] Safe area: thử máy có notch/home indicator và xoay ngang; nút chính, tab và sheet không bị che.
- [ ] Chữ lớn: tăng cỡ chữ hệ thống, kiểm tra tên địa điểm dài, giá, nút xác nhận và thông báo lỗi; nội dung cuộn được, không cắt chữ.
- [ ] VoiceOver/TalkBack: tên nút rõ, đọc được lỗi và trạng thái đang bận, focus không thoát khỏi sheet đang mở.
- [ ] Android Back: đóng sheet trước, từ tài khoản về chuyến, từ báo giá về sửa hành trình; thoát không tự hủy chuyến.
- [ ] Đóng/mở lại app ở chuyến đang chạy hoặc thanh toán chờ: giữ đúng chuyến, không tự chuyển thành đã thanh toán.

Kiểm tra browser ngày 21/10/2026: nút xác nhận Rider nằm trong viewport ở 320/390/430 × 568, không tràn ngang; sheet tìm địa điểm có trạng thái không tìm thấy ở 320 × 568. Đây là kiểm tra web, không đánh dấu thay các mục native phía trên.

`npm audit` ngày 12/10/2026 còn báo cảnh báo phụ thuộc gián tiếp của Expo/Metro (gồm `braces`, `node-forge`, `uuid`). Registry chưa có bản sửa tương thích cho toàn bộ chuỗi; không dùng `npm audit fix --force` để hạ Expo/React Native sang major cũ. Chưa đánh giá khả năng khai thác cho bản phát hành; rà soát lại trước khi deploy/build phân phối. Không đưa dev server ra Internet.
