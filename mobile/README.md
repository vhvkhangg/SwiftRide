# SwiftRide Mobile

Hai ứng dụng React Native + Expo + TypeScript cho **Rider** và **Driver**. Cả hai gọi backend thật: đăng nhập bằng Keycloak, mọi thao tác đi qua API Gateway tới Trip, Matching & Pricing và Payment. Không còn chế độ demo hay dữ liệu giả.

| | Rider | Driver |
|---|---|---|
| Thư mục | `apps/rider` | `apps/driver` |
| Tên cài đặt | SwiftRide Rider | SwiftRide Driver |
| Android / iOS ID | `com.swiftride.rider` | `com.swiftride.driver` |
| URL scheme (redirect đăng nhập native) | `swiftride-rider://` | `swiftride-driver://` |
| Cổng Metro / web preview | 8081 | 8082 |
| Tài khoản dev (Keycloak) | `rider.demo@example.com`, `rider.other@example.com` | `driver.demo@example.com`, `driver.other@example.com` |

Mật khẩu tài khoản dev nằm trong `keycloak/swiftride-realm.json` (biến `DEV_ACCOUNT_PASSWORD`). Rider đăng ký ngay trong app (nút **Đăng ký**); tài khoản Driver chỉ tạo được trong Keycloak Admin Console.

## Chạy

Cần backend đang chạy: ở thư mục gốc `docker compose up -d` và chờ các service healthy. Yêu cầu Node.js >= 22.13; cài dependencies **một lần tại `mobile/`**.

```powershell
cd E:\SwiftRide\mobile
npm ci
npm run preview:rider    # http://localhost:8081
npm run preview:driver   # terminal thứ hai, http://localhost:8082
```

Mặc định app gọi Gateway `http://localhost:5100` và Keycloak `http://localhost:8180/realms/swiftride`. Đổi bằng biến môi trường trước khi chạy Expo (đổi xong phải khởi động lại Expo):

```powershell
$env:EXPO_PUBLIC_GATEWAY_URL = 'http://192.168.1.10:5100'
$env:EXPO_PUBLIC_KEYCLOAK_ISSUER = 'http://192.168.1.10:8180/realms/swiftride'
npm run start:rider
```

Trên điện thoại thật, `localhost` là chính điện thoại: dùng IP LAN của máy chạy Docker và chạy stack với `KEYCLOAK_PUBLIC_URL=http://<IP>:8180` để issuer trong token khớp. URL sai định dạng sẽ hiện màn "Cấu hình chưa hợp lệ" thay vì gọi nhầm chỗ. Không đặt token hoặc bí mật vào biến `EXPO_PUBLIC_*`.

**Demo Rider và Driver trên cùng một máy:** hai app dùng chung phiên SSO của Keycloak (cookie của `localhost:8180`), nên mở app Driver trong **cửa sổ ẩn danh hoặc trình duyệt khác**. Nếu trang Keycloak hiện sẵn email của tài khoản kia, bấm **Restart login** để đổi tài khoản; app từ chối tài khoản sai role.

## Luồng

| | |
|---|---|
| Đăng ký | Chỉ app Rider. Form trong app (họ, tên, email, mật khẩu ≥ 12 ký tự, nhập lại) → `POST /api/auth/register` (Gateway, không cần token) → Gateway tạo user trong Keycloak bằng service account, user mới luôn là Rider. Xong thì nút Đăng nhập mở trang đăng nhập với email điền sẵn. Lỗi trùng email (409), sai dữ liệu (400), quá nhiều lần (429) hiện rõ trên form. |
| Đăng nhập | Keycloak, client `swiftride-mobile`, Authorization Code + PKCE (S256). Web: chuyển hướng cả trang sang Keycloak rồi quay lại (không dùng popup vì trình duyệt điện thoại hay chặn); native: trình duyệt hệ thống. App không nhận mật khẩu. Token chỉ giữ trong bộ nhớ, tự refresh trước khi hết hạn (một request refresh tại một thời điểm). Đăng xuất thu hồi refresh token và thoát phiên SSO. App Rider chỉ nhận tài khoản có role Rider, app Driver chỉ nhận Driver. |
| Bản đồ | Bản đồ OpenStreetMap thật bằng Leaflet: web dùng gói `leaflet`, native nhúng cùng mã đó trong `react-native-webview` (Leaflet 1.9.4 từ unpkg, có SRI). Chạm bản đồ để chọn điểm; tên địa chỉ lấy từ Nominatim (lỗi hoặc chậm thì hiện tọa độ). Có ô tìm địa chỉ (Nominatim, giới hạn Việt Nam) và 4 địa điểm chọn nhanh. Đường đi, quãng đường và thời gian lấy từ OSRM; OSRM lỗi thì ước tính đường chim bay × 1,3, 22 km/h và báo rõ trên màn hình. |
| Rider | Chọn điểm đón/đến trên bản đồ → `POST /api/matching/quote` (bảng giá: cước theo km, theo phút, phụ thu cao điểm, phí phụ, giảm giá, VAT) → chọn thanh toán **Ví SwiftRide** hoặc **Thẻ** (thẻ là cổng mô phỏng, app không nhập số thẻ) → `POST /api/trips/` (kèm `idempotencyKey`, tọa độ đón/đến và `paymentMethod`; đổi phương thức sẽ tạo key mới) → `POST /confirm-price` → theo dõi trạng thái, số tài xế đã từ chối / ngưỡng k → Saga thanh toán (`GET /api/trips/{id}/payment-saga`) → kết quả. Hủy được khi trạng thái cho phép. |
| Driver | Bật nhận chuyến, chọn vị trí giả lập bằng cách chạm bản đồ / tìm địa chỉ → `POST /api/drivers/location` (gửi lại mỗi 60 giây để không quá TTL vị trí) → lời mời có đếm ngược, bản đồ hiện tài xế + điểm đón/đến + đường đi → nhận / từ chối → đi đón → đã đón (xe dời tới điểm đón) → đã trả khách (xe dời tới điểm đến) → xem Saga thanh toán. |
| Khôi phục | Mở lại app: `GET /api/trips/active` lấy chuyến đang chạy. HTTP là nguồn sự thật; app hỏi lại trạng thái mỗi 2 giây (chưa dùng SignalR). |
| Lỗi | 401/403/409/5xx hiện thông báo; 409 tự tải lại chuyến. Không tự gửi lại POST. |

Giá hiển thị đúng con số Pricing trả về, không nhân thêm. Vị trí tài xế vẫn là GPS giả lập (chọn trên bản đồ), chưa đọc GPS thiết bị và chưa dẫn đường. Dịch vụ OSM công cộng (tile, Nominatim, OSRM demo) chỉ phù hợp cho bài tập/demo: Nominatim giới hạn 1 request/giây, app chỉ gọi khi người dùng chạm hoặc bấm Tìm.

Web preview gọi Gateway khác origin nên Gateway bật CORS cho `http://localhost:8081` và `http://localhost:8082` (`Cors:AllowedOrigins`). Keycloak đã có dữ liệu từ trước cần thêm `http://localhost:8082/*` vào redirect URI của client `swiftride-mobile` (hoặc tạo lại DB Keycloak) để app Driver bản web đăng nhập được.

Đăng ký trong app cần client `swiftride-registration` (đã có trong `swiftride-realm.json`). Keycloak import realm từ trước thì thêm bằng lệnh sau (PowerShell, ở thư mục gốc, giữ nguyên dữ liệu), hoặc xóa volume `keycloak-postgres-data` để import lại:

```powershell
docker compose exec -T keycloak /opt/keycloak/bin/kcadm.sh config credentials --server http://localhost:8080 --realm master --user admin --password keycloak-admin-dev
docker compose exec -T keycloak /opt/keycloak/bin/kcadm.sh create clients -r swiftride -s clientId=swiftride-registration -s publicClient=false -s secret=swiftride-registration-dev-secret -s standardFlowEnabled=false -s directAccessGrantsEnabled=false -s serviceAccountsEnabled=true
docker compose exec -T keycloak /opt/keycloak/bin/kcadm.sh add-roles -r swiftride --uusername service-account-swiftride-registration --cclientid realm-management --rolename manage-users
docker compose exec -T keycloak /opt/keycloak/bin/kcadm.sh add-roles -r swiftride --rname default-roles-swiftride --rolename Rider
docker compose exec -T keycloak /opt/keycloak/bin/kcadm.sh update realms/swiftride -s registrationAllowed=false
docker compose up -d --build api-gateway
```

## Cấu trúc

```text
mobile/
  apps/rider/       entry Rider, app.json, eas.json, icon
  apps/driver/      entry Driver, app.json, eas.json, icon
  shared/           khung app, cấu hình, client Gateway, UI dùng chung, 4 địa điểm chọn nhanh
  shared/map/       bản đồ Leaflet (LiveMap.web.tsx cho web, LiveMap.tsx qua WebView cho native), Nominatim, OSRM
  shared/api/       đăng nhập Keycloak, màn Rider/Driver gọi Gateway, helper thuần (trip.ts)
  tests/            test Node.js
```

`metro.config.js` thêm `shared/` vào watchFolders để sửa code dùng chung cũng cập nhật bundle. `shared/gateway.ts` tập trung request, timeout, hủy và lỗi HTTP/mạng; không tự retry.

## Hai ứng dụng Android riêng

Cần Android Studio, Android SDK, JDK phù hợp React Native, `ANDROID_HOME`, emulator hoặc điện thoại bật USB debugging (`adb devices` nhận thiết bị). Tắt Metro preview đang chiếm cùng cổng trước.

```powershell
cd E:\SwiftRide\mobile
npm run android:rider
```

Terminal khác: `npm run android:driver`. Hai app có application ID khác nhau nên không ghi đè nhau; native project sinh tự động và được Git bỏ qua. Build APK chia sẻ bằng EAS (tùy chọn): `npx eas-cli build --platform android --profile preview` trong từng thư mục app; không đưa keystore hay token vào Git. iOS có bundle ID riêng; build cục bộ cần macOS/Xcode, chưa kiểm thử trên iOS.

## Kiểm tra

```powershell
cd E:\SwiftRide\mobile
npm run typecheck
npm test
```

Test bao phủ: nhãn phương thức thanh toán, form đăng ký (thứ tự lỗi, gửi đúng field không kèm token/role, thông báo 409/400/429/503/mạng, không tự gửi lại), ánh xạ trạng thái chuyến (giá trị lạ không bao giờ bị coi là đã xong), ước tính quãng đường, đường đi OSRM và dự phòng khi lỗi, rút gọn địa chỉ OSM, tìm địa chỉ, tọa độ chuyến từ server, hiển thị giá, đọc claim từ token Keycloak, cấu hình URL, client Gateway (giữ body/header, chặn đường dẫn lạ, lỗi HTTP rõ ràng, body rỗng của 202, không tự retry), nút chống bấm lặp và trạng thái bận, bản đồ có nhãn accessibility, và hai app tách biệt định danh. Luồng đầu-cuối được kiểm tra thủ công trên Docker (Rider đặt xe → Driver nhận → đón → trả → Saga Completed; hủy chuyến; đăng xuất).

`npm audit` còn báo cảnh báo phụ thuộc gián tiếp của Expo/Metro; không dùng `npm audit fix --force` để hạ Expo/React Native. Không đưa dev server ra Internet.
