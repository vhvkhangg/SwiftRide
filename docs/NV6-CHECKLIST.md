# Checklist triển khai và bàn giao NV6 — RabbitMQ + MassTransit

Ngày lập: 14/10/2026. Workspace: `D:\SwiftRide`.

Đây là checklist thực hiện, không phải báo cáo đã triển khai. Chỉ đánh dấu một mục khi có file tương ứng và bằng chứng kiểm tra. Các đường dẫn dưới đây tính từ gốc repository. Tên file mới là cấu trúc đề xuất.

## 1. Phạm vi và hiện trạng

- [x] Kiểm tra repository: .NET 10, quản lý NuGet tập trung qua `Directory.Packages.props`.
- [x] Xác nhận chưa có RabbitMQ trong Compose, chưa có project Contracts/Messaging và chưa có dependency MassTransit.
- [x] Xác nhận Dockerfile của service restore sau khi chỉ copy project của chính service; cần copy thêm project dùng chung khi có reference.
- [x] Kiểm tra health hiện tại: Trip/Matching trả trạng thái tĩnh; Payment có kiểm tra kết nối database.
- [ ] Xây dựng broker, cấu hình transport/conventions, contracts dùng chung, routing, retry/error handling, smoke/integration tests và tài liệu.

NV6 không viết business consumer, không sửa schema/database, không tạo migration, không triển khai Outbox/Inbox hoặc tự đổi luồng HTTP nghiệp vụ hiện có. Consumer kỹ thuật để kiểm tra messaging thuộc phạm vi NV6. NV1–NV3 sở hữu thời điểm phát message, business consumers và idempotency của service.

## 2. Thứ tự thực hiện

| Bước | Công việc | Điều kiện chuyển bước |
|---|---|---|
| 1 | Chốt contracts/routing với NV1–NV3 | Có danh sách message, trường dữ liệu, publisher/subscriber và endpoint |
| 2 | Chạy RabbitMQ độc lập | Broker healthy, đăng nhập Management UI được |
| 3 | Tạo projects và dependencies | Restore/build thành công |
| 4 | Viết Contracts | Contracts độc lập, nhóm thống nhất schema |
| 5 | Viết cấu hình Messaging dùng chung | Có cấu hình host, naming, retry và validate options |
| 6 | Viết app smoke | Publish/consume qua RabbitMQ thật thành công |
| 7 | Viết integration tests | Routing, retry và error/skipped tests thành công |
| 8 | Tích hợp phần hạ tầng vào 3 service | References, startup, Docker build và bus readiness hoạt động |
| 9 | Viết tài liệu và chạy nghiệm thu | Có kết quả kiểm tra, hướng dẫn tích hợp và bàn giao |

Trong lúc chờ thống nhất contracts nghiệp vụ, có thể làm bước 2–3 và dùng contract kỹ thuật cho bước 5–7.

## 3. Bước 1 — Chốt hợp đồng

**File tạo:** `docs/messaging-contracts.md`.

- [ ] Ghi cho từng message: ý nghĩa, chủ sở hữu, thời điểm phát, trường bắt buộc, kiểu dữ liệu, version và bên nhận.
- [ ] Ghi rõ event dùng Publish, command có đích xác định dùng Send.
- [ ] Thống nhất kiểu TripId: Trip/Payment hiện dùng Guid, DTO Matching hiện dùng string.
- [ ] Giải quyết yêu cầu tọa độ: Matching cần tọa độ, DTO tạo Trip hiện chỉ có địa chỉ. NV1/NV2 quyết định nguồn dữ liệu; NV6 không tự sửa database để bổ sung.
- [ ] Với command thanh toán, thống nhất Amount, currency, khóa idempotency và dữ liệu tài khoản với NV3; không suy ra số tiền thanh toán từ FareEstimate khi chưa thống nhất.
- [ ] Thống nhất MessageId cho lần phát và CorrelationId cho luồng liên quan; không coi CorrelationId là khóa chống trùng cho mọi message.
- [ ] Ghi quy tắc tương thích: thay đổi phá vỡ tương thích dùng contract/type V2.
- [ ] Ghi bằng chứng thống nhất của nhóm trong tài liệu; chưa thống nhất phải ghi rõ là đề xuất.

Danh sách gợi ý để thảo luận, không phải quyết định đã được chốt:

| Message | Publisher/đích gửi đề xuất | Subscriber/đích nhận đề xuất | Cách truyền |
|---|---|---|---|
| TripRequestedV1 | Trip | Matching | Publish |
| DriverMatchedV1 | Matching | Trip | Publish |
| DriverMatchFailedV1 | Matching | Trip | Publish |
| ProcessPaymentV1 | Bên điều phối do nhóm quyết định | Payment | Send |
| PaymentSucceededV1 | Payment | Trip | Publish |
| PaymentFailedV1 | Payment | Trip | Publish |

**Đạt khi:** schema và routing được NV1–NV3 thống nhất; có thể dùng làm đầu vào cho code contracts.

## 4. Bước 2 — Hạ tầng RabbitMQ

| File | Thao tác | Nội dung |
|---|---|---|
| `docker-compose.yml` | Sửa | Thêm service rabbitmq, image có management, hostname ổn định, ports 5672/15672, volume, healthcheck và restart policy |
| `.env.example` | Sửa | Thêm RABBITMQ_USER, RABBITMQ_PASSWORD, RABBITMQ_VHOST, ghi rõ giá trị mẫu dành cho dev |
| `.env` | Tạo/sửa ở máy local | Điền cấu hình thực tế; không commit |
| `.dockerignore` | Tạo | Loại .git, .env, bin/obj, TestResults, node_modules, dist và playwright-output khỏi build context; giữ các file cần build |

- [ ] Mount named volume `rabbitmq-data:/var/lib/rabbitmq` và khai báo volume ở cuối Compose.
- [ ] Ghim phiên bản image/digest đã kiểm tra khi bàn giao.
- [ ] Dùng user riêng, vhost `swiftride`; bind cổng dev ra loopback nếu chỉ chạy trên máy.
- [ ] Healthcheck dùng `rabbitmq-diagnostics -q check_running` và `check_local_alarms`.
- [ ] Đăng nhập Management UI và xác nhận user/vhost.
- [ ] Ghi rõ đổi biến môi trường khởi tạo không tự cập nhật user/password đã tồn tại trong volume.

```powershell
Set-Location D:\SwiftRide
# Chỉ copy khi chưa có .env; giữ cấu hình local đã tồn tại.
if (-not (Test-Path -LiteralPath .env)) { Copy-Item .env.example .env }
docker compose config --quiet
docker compose up -d rabbitmq
docker compose ps rabbitmq
docker compose logs --tail 100 rabbitmq
```

**Đạt khi:** broker healthy và mở được `http://localhost:15672`; volume tồn tại.

## 5. Bước 3 — Projects và dependency

| File | Thao tác | Nội dung |
|---|---|---|
| `src/SwiftRide.Contracts/SwiftRide.Contracts.csproj` | Tạo | Class library .NET 10; không reference service, EF Core hoặc MongoDB |
| `src/SwiftRide.Messaging/SwiftRide.Messaging.csproj` | Tạo | Class library, reference Contracts nếu cấu hình topology theo contract; thêm MassTransit.RabbitMQ và các Microsoft.Extensions cần dùng |
| `tools/SwiftRide.Messaging.Smoke/SwiftRide.Messaging.Smoke.csproj` | Tạo | Console/Generic Host; reference Contracts, Messaging và hosting |
| `tests/SwiftRide.Messaging.Tests/SwiftRide.Messaging.Tests.csproj` | Tạo | Dùng cấu hình xUnit hiện có; reference Contracts/Messaging, dependency hosting/test cần thiết |
| `Directory.Packages.props` | Sửa | Ghim version dependency mới tập trung; đồng bộ họ package MassTransit nếu thêm nhiều package |
| `SwiftRide.sln` | Sửa | Đăng ký bốn project trên |

- [ ] Chọn phiên bản MassTransit xác định; mẫu trước dùng 8.5.5. Không tự nâng major mà chưa kiểm tra API và điều kiện sử dụng.
- [ ] PackageReference trong csproj không ghi Version vì repository dùng Central Package Management.
- [ ] Tận dụng Directory.Build.props; không tạo target framework khác cho project mới.
- [ ] Contracts và Messaging build được mà không cần project/service database.
- [ ] Restore/build solution thành công.

**Đạt khi:** bốn project có trong solution; dependency không bị khai báo version mâu thuẫn.

## 6. Bước 4 — Message contracts

| File đề xuất | Nội dung |
|---|---|
| `src/SwiftRide.Contracts/Diagnostics/MessagingProbeV1.cs` | Probe dùng kiểm tra, có ProbeId/CorrelationId, thời gian UTC và dữ liệu đơn giản |
| `src/SwiftRide.Contracts/Trips/TripRequestedV1.cs` | Các trường đã thống nhất để Matching nhận yêu cầu |
| `src/SwiftRide.Contracts/Matching/DriverMatchedV1.cs` | Kết quả tìm được tài xế |
| `src/SwiftRide.Contracts/Matching/DriverMatchFailedV1.cs` | Kết quả không tìm được tài xế và mã lý do |
| `src/SwiftRide.Contracts/Payments/ProcessPaymentV1.cs` | Command thanh toán với dữ liệu NV3 đã thống nhất |
| `src/SwiftRide.Contracts/Payments/PaymentSucceededV1.cs` | Event thanh toán thành công |
| `src/SwiftRide.Contracts/Payments/PaymentFailedV1.cs` | Event thanh toán thất bại |

- [ ] Chỉ tạo message nghiệp vụ nằm trong danh sách đã thống nhất; điều chỉnh tên/file theo quyết định nhóm.
- [ ] Dùng public record/class với dữ liệu serializable; không chứa entity/domain state hoặc hành vi nghiệp vụ.
- [ ] Thống nhất DateTimeOffset UTC, decimal cho tiền và kiểu ID.
- [ ] Không đổi namespace/type của contract đã bàn giao mà không đánh giá tương thích.

**Đạt khi:** các service có thể reference cùng assembly contracts; schema khớp tài liệu bước 1.

## 7. Bước 5 — Transport và conventions

| File tạo | Nội dung và tiêu chí |
|---|---|
| `src/SwiftRide.Messaging/RabbitMqOptions.cs` | Host, Port, VirtualHost, Username, Password; validate trường bắt buộc và port |
| `src/SwiftRide.Messaging/MessagingExtensions.cs` | AddSwiftRideMessaging; bind options, đăng ký MassTransit, host và callback đăng ký consumer do service cung cấp |
| `src/SwiftRide.Messaging/MessagingTopology.cs` | Quy tắc exchange theo contract đã chốt; có thể giữ tên exchange mặc định, phải ghi rõ |
| `src/SwiftRide.Messaging/MessagingEndpoints.cs` | Hằng tên endpoint và địa chỉ Send; command endpoint có tên cố định |
| `src/SwiftRide.Messaging/MessagingRetryPolicy.cs` | Policy retry chung và cách service tùy chỉnh loại lỗi cần/không cần retry |

- [ ] Naming đề xuất: `swiftride-{service}-{handler}` dạng kebab-case; mọi instance cùng handler dùng cùng queue.
- [ ] Publish event tới các queue subscriber riêng; Send command tới endpoint cụ thể.
- [ ] Không dùng một queue chung để hai service đều nhận đầy đủ cùng event.
- [ ] Ghi rõ formatter endpoint không tự đổi tên message exchange.
- [ ] Queue nghiệp vụ durable, không auto-delete, không purge khi khởi động.
- [ ] Cấu hình khởi đầu: prefetch 16, concurrent message limit 8; cho phép điều chỉnh.
- [ ] Retry đề xuất: 1s, 3s, 5s; tối đa 4 lần xử lý gồm lần đầu. Phân loại exception cần thống nhất với chủ service.
- [ ] Chỉ gắn retry một lần trên một endpoint; tránh cấu hình trùng giữa common extension và consumer definition.
- [ ] Giữ cơ chế `_error`, `_skipped` mặc định; không bỏ message lỗi.
- [ ] Cấu hình timeout start/stop; chọn rõ có chờ kết nối khi startup hay không và tài liệu hóa.
- [ ] ConfigureEndpoints gọi cuối; chỉ đăng ký AddMassTransit một lần trong mỗi service.
- [ ] Log đủ MessageId, CorrelationId, endpoint và exception để truy vết.

**Đạt khi:** smoke và tests dùng chính thư viện này, không có bản cấu hình RabbitMQ riêng chỉ dành cho test làm lệch behavior.

## 8. Bước 6 — Smoke test

| File tạo | Nội dung |
|---|---|
| `tools/SwiftRide.Messaging.Smoke/Program.cs` | Đọc config, start host, chờ endpoint sẵn sàng, publish/send probe, chờ kết quả có timeout, trả exit code rõ |
| `tools/SwiftRide.Messaging.Smoke/MessagingProbeConsumer.cs` | Consumer kỹ thuật ghi nhận đúng probe; không truy cập DB hoặc gọi API nghiệp vụ |
| `tools/SwiftRide.Messaging.Smoke/ProbeResult.cs` | Bộ ghi nhận thread-safe, so khớp probe riêng của lần chạy |

- [ ] Chạy qua RabbitMQ thật và AddSwiftRideMessaging.
- [ ] Nhận đúng probe và in PASS; trả nonzero nếu timeout/lỗi.
- [ ] Chạy lặp lại không nhận nhầm message cũ; dùng ID mới và lọc kết quả hoặc endpoint kỹ thuật riêng.
- [ ] Không commit password; truyền qua biến môi trường.

```powershell
$env:RabbitMq__Host = "localhost"
$env:RabbitMq__Port = "5672"
$env:RabbitMq__VirtualHost = "swiftride"
$env:RabbitMq__Username = "swiftride"
# Đặt RabbitMq__Password theo cấu hình local; không lưu giá trị vào kết quả bàn giao.
dotnet run --project tools/SwiftRide.Messaging.Smoke
```

**Đạt khi:** exit code 0, in PASS, probe đã được consume qua broker.

## 9. Bước 7 — Integration tests

Các file dưới đây đều tạo trong `tests/SwiftRide.Messaging.Tests/`.

| File | Ca kiểm tra / assertion |
|---|---|
| `Infrastructure/RabbitMqFixture.cs` | Cấu hình RabbitMQ thật, vhost test riêng, queue riêng mỗi test, readiness, timeout và cleanup |
| `Infrastructure/RabbitMqManagementClient.cs` | Kiểm tra queue/error/skipped bằng Management API; giới hạn vào tài nguyên test |
| `Consumers/ProbeConsumers.cs` | Consumer kỹ thuật: nhận thành công, lỗi tạm thời, lỗi vĩnh viễn, lỗi không retry; không có business consumer |
| `PublishConsumeTests.cs` | Publish probe; nhận đúng nội dung và ID |
| `RoutingTests.cs` | Publish tới A/B: cả hai nhận; Send tới A: A nhận và B không nhận trong khoảng quan sát xác định |
| `RetryTests.cs` | Lỗi hai lần rồi thành công: 3 lần xử lý; lỗi không retry: 1 lần xử lý |
| `ErrorQueueTests.cs` | Lỗi mãi: 4 lần xử lý và message gốc vào `_error`; sai loại contract vào `_skipped` |
| `OptionsValidationTests.cs` | Thiếu host/credentials hoặc port sai: lỗi cấu hình rõ ràng, không lộ password |

- [ ] Provision vhost test trước khi chạy; fixture fail với hướng dẫn rõ nếu broker/vhost chưa sẵn sàng.
- [ ] Dùng cùng retry/routing configuration với thư viện dùng chung.
- [ ] Chờ điều kiện có timeout, không phụ thuộc sleep cố định.
- [ ] Không chạy các test thao tác trên cùng queue song song.
- [ ] Chỉ cleanup queue/exchange do test tạo; không purge queue của service.
- [ ] Không dùng test in-memory để thay bằng chứng RabbitMQ routing.
- [ ] Thực hiện kiểm tra volume: tạo binding → dừng receiver → publish → xác nhận message chờ → restart broker → xác nhận message còn → consume.
- [ ] Thực hiện kiểm tra readiness: dừng broker khi service đang chạy → readiness unhealthy → bật lại broker → readiness healthy sau khi kết nối hồi phục.

**Đạt khi:** tests pass trên RabbitMQ thật; có kết quả retry, queue lỗi và kiểm tra lưu trữ.

## 10. Bước 8 — Tích hợp phần hạ tầng vào service

NV6 thực hiện references/config/startup; NV1–NV3 tự viết và đăng ký consumer nghiệp vụ. Tích hợp publisher-only vẫn kiểm tra được kết nối bus nhưng chưa chứng minh routing nghiệp vụ.

| File sửa | Công việc |
|---|---|
| `src/TripService/TripService.csproj` | Reference Contracts và Messaging |
| `src/MatchingService/MatchingService.csproj` | Reference Contracts và Messaging |
| `src/PaymentService/PaymentService.csproj` | Reference Contracts và Messaging |
| `src/TripService/Program.cs` | Gọi AddSwiftRideMessaging với serviceName trip; bổ sung readiness bus |
| `src/MatchingService/Program.cs` | Gọi extension với serviceName matching; bổ sung readiness bus |
| `src/PaymentService/Program.cs` | Gọi extension với serviceName payment; bổ sung readiness bus, giữ health database hiện có |
| `src/TripService/appsettings.json` | Host/Port/VirtualHost và cấu hình công khai; credentials qua env |
| `src/MatchingService/appsettings.json` | Cùng schema RabbitMq |
| `src/PaymentService/appsettings.json` | Cùng schema RabbitMq; giữ cấu hình tích hợp HTTP hiện có |
| `src/TripService/Dockerfile` | Copy csproj Contracts/Messaging trước dotnet restore |
| `src/MatchingService/Dockerfile` | Copy các csproj dùng chung trước restore |
| `src/PaymentService/Dockerfile` | Copy các csproj dùng chung trước restore |
| `docker-compose.yml` | Ba service có RabbitMq__Host=rabbitmq, port/vhost/credentials từ env và depends_on rabbitmq healthy |

- [ ] Local dotnet run dùng localhost; trong Compose dùng hostname rabbitmq.
- [ ] Bổ sung `/health/ready` bằng health checks của bus; giữ behavior `/health` đang có.
- [ ] Không thêm RabbitMQ vào ApiGateway nếu gateway không có nhu cầu messaging.
- [ ] Không thay callback HTTP, application service, repository hoặc migration bằng messaging trong phạm vi NV6.
- [ ] Docker build được sau khi thêm ProjectReference.
- [ ] Readiness phản ánh bus kết nối; lỗi DB/startup có sẵn phải phân biệt với lỗi messaging khi nghiệm thu.

**Đạt khi:** ba service dùng thư viện chung và kết nối broker; hướng dẫn để chủ service thêm consumers/publishers đã rõ.

## 11. Bước 9 — Tài liệu và bằng chứng

| File | Thao tác | Nội dung |
|---|---|---|
| `docs/messaging.md` | Tạo | Hướng dẫn chạy local/Compose, env, thêm consumer, Publish/Send, health, naming, retry, giới hạn trách nhiệm |
| `docs/messaging-contracts.md` | Hoàn thiện | Contracts/version và bằng chứng thống nhất với NV1–NV3 |
| `docs/messaging-routing.md` | Tạo | Message → publisher → exchange → queue → consumer; endpoint command cố định; sơ đồ nếu hữu ích |
| `docs/messaging-error-handling.md` | Tạo | Xem lỗi, phân biệt error/skipped/Fault, sửa nguyên nhân, gửi lại message gốc và kiểm tra |
| `docs/NV6-VERIFICATION.md` | Tạo | Ngày chạy, commit, version, lệnh, exit code, test totals, log/ảnh minh chứng đã thực sự thu được |
| `README.md` | Sửa | Cập nhật trạng thái RabbitMQ/MassTransit, ports và link hướng dẫn mới |
| `docs/NV6-CHECKLIST.md` | Cập nhật | Đánh dấu các mục đã hoàn thành bằng bằng chứng; ghi rõ phần còn chờ nhóm |

- [ ] Tài liệu mô tả bootstrap topology trước Publish lần đầu; không hứa message sẽ được lưu khi chưa có queue/binding nhận.
- [ ] Quy trình replay giữ message gốc/envelope và headers cần thiết; Fault<T> không thay thế message gốc.
- [ ] Không tạo vòng lặp tự replay vô hạn; người xử lý kiểm tra nguyên nhân trước khi gửi lại.
- [ ] Ghi rõ durable/retry không đồng nghĩa exactly-once hoặc đảm bảo đồng bộ DB + publish; Outbox/Inbox/idempotency thuộc chủ service.
- [ ] Bằng chứng không chứa password hoặc dữ liệu nhạy cảm.
- [ ] Người khác có thể làm theo tài liệu để chạy smoke/test từ checkout sạch, không dựa vào queue đã tạo thủ công từ trước.

## 12. Lệnh nghiệm thu cuối

Chỉ chạy sau khi đã tạo các project/file trong checklist và cấu hình broker/test credentials.

```powershell
Set-Location D:\SwiftRide
docker compose config --quiet
docker compose up -d rabbitmq
docker compose ps rabbitmq

dotnet restore SwiftRide.sln
dotnet build SwiftRide.sln --no-restore
dotnet run --project tools/SwiftRide.Messaging.Smoke
dotnet test tests/SwiftRide.Messaging.Tests/SwiftRide.Messaging.Tests.csproj --logger "trx;LogFileName=nv6-messaging.trx"
dotnet test SwiftRide.sln

docker compose build trip-service matching-service payment-service
docker compose up -d trip-service matching-service payment-service
docker compose logs --tail 100 trip-service matching-service payment-service

Invoke-WebRequest http://localhost:5101/health/ready
Invoke-WebRequest http://localhost:5102/health/ready
Invoke-WebRequest http://localhost:5103/health/ready
```

Không dùng `docker compose down -v` để kiểm tra khởi động lại vì lệnh đó xóa volume, gồm dữ liệu database của các service. Dùng restart broker cho kiểm tra lưu trữ.

## 13. Definition of Done — NV6 hoàn thành khi nào?

- [ ] RabbitMQ healthy, có volume và giao diện quản lý; kiểm tra restart giữ message thành công.
- [ ] Contracts nghiệp vụ được NV1–NV3 thống nhất; project contracts độc lập.
- [ ] Common transport/config/conventions được smoke/tests và ba service sử dụng.
- [ ] Routing Publish/Send, exchange/queue và endpoint đã được ghi rõ.
- [ ] Smoke publish/consume thành công trên RabbitMQ thật.
- [ ] Tests chứng minh fan-out, Send đúng đích, retry, hết retry, lỗi không retry và skipped.
- [ ] Có quy trình kiểm tra và gửi lại message lỗi.
- [ ] Restore/build/test và Docker build phần bị ảnh hưởng thành công; kết quả được lưu trong verification.
- [ ] NV1–NV3 có hướng dẫn tích hợp, không cần NV6 viết business consumers thay họ.
- [ ] Không có thay đổi ngoài phạm vi: business consumer, schema/migration, Outbox/Inbox hoặc tự chuyển luồng HTTP sang messaging.
- [ ] Checklist đã đánh dấu đúng trạng thái; không coi kiểm tra repository hoặc tạo tài liệu là đã triển khai hạ tầng.

## Nguồn tham khảo kỹ thuật

- Healthcheck dùng các diagnostics kiểm tra ứng dụng và cảnh báo tài nguyên: [RabbitMQ Monitoring](https://www.rabbitmq.com/docs/monitoring).
- Topology Publish/Send, endpoint và yêu cầu queue/binding trước khi phát event: [MassTransit RabbitMQ Configuration](https://masstransit.massient.com/configuration/transports/rabbitmq).
- Queue lỗi, skipped và Fault: [MassTransit Exceptions](https://masstransit.massient.com/concepts/exceptions).

Đối chiếu API với phiên bản package đã ghim; tài liệu trực tuyến có thể mô tả cả các tính năng của major mới hơn.
