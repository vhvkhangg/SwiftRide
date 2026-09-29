# SwiftRide

SwiftRide là bài tập lớn môn **Kiến trúc Phần mềm**. Hệ thống mô phỏng một nền tảng đặt xe hai phía, kết nối **rider** (người đi) và **driver** (tài xế), với trọng tâm là thiết kế kiến trúc microservices, SOLID và các design pattern phù hợp.

## 1. Phạm vi core

Repository scaffold này ưu tiên **luồng cơ bản chạy được trước**. Core gồm:

- **API Gateway** dùng YARP, là điểm vào chung cho client.
- **Trip Service** quản lý vòng đời chuyến đi / state machine.
- **Matching & Pricing Service** tìm tài xế và tính giá.
- **Payment Service** xử lý thanh toán, ledger/refund và idempotency ở giai đoạn triển khai nghiệp vụ.
- JWT Bearer dependency được khai báo để triển khai authentication/authorization trong core.
- Docker Compose cho toàn bộ backend và 3 database logic riêng.
- xUnit + Moq cho kiểm thử.

Các phần **RabbitMQ/MassTransit, Transactional Outbox, OpenTelemetry và Saga** chưa được đưa vào dependency/core scaffold này. Chúng nên được bổ sung sau khi end-to-end basic flow ổn định.

Frontend chưa được chọn công nghệ nên repository hiện **không chứa frontend-specific files hoặc dependencies**.

## 2. Kiến trúc

```text
Client (frontend TBD)
        |
        v
API Gateway (YARP + JWT)
        |
        +--------------------+--------------------+
        |                    |                    |
        v                    v                    v
 Trip Service       Matching/Pricing       Payment Service
        |                    |                    |
        v                    v                    v
 PostgreSQL              MongoDB               PostgreSQL
  TripDB                 MatchDB                PayDB
```

Mỗi service sở hữu dữ liệu của mình; Trip và Payment dùng cùng **DBMS PostgreSQL** nhưng vẫn tách thành **database/instance riêng** trong môi trường Docker để giữ nguyên nguyên tắc database-per-service.

## 3. Luồng nghiệp vụ chính

Luồng mục tiêu của bài:

1. Rider gửi yêu cầu đặt xe qua API Gateway.
2. Trip Service tạo chuyến ở trạng thái `Requested`.
3. Matching & Pricing Service tìm tài xế phù hợp và tính giá.
4. Rider xác nhận hoặc từ chối giá.
5. Driver nhận/từ chối chuyến; hệ thống có thể thử tài xế khác theo giới hạn `k`.
6. Khi driver nhận chuyến, Trip Service quản lý các trạng thái đang đến, đón khách và trả khách.
7. Sau khi trả khách, Payment Service xử lý thanh toán.
8. Chuyến kết thúc ở trạng thái thành công hoặc một trạng thái kết thúc/lỗi phù hợp.

## 4. Tech stack core

| Hạng mục | Công nghệ |
|---|---|
| Language | C# |
| Runtime / Framework | .NET 10 LTS / ASP.NET Core |
| API style | Minimal API hoặc Controllers |
| API Gateway | YARP |
| Authentication | JWT Bearer; OAuth2/OIDC có thể tích hợp sau |
| Trip data | PostgreSQL + EF Core 10 + Npgsql |
| Matching/Pricing data | MongoDB + MongoDB.Driver |
| Payment data | PostgreSQL + EF Core 10 + Npgsql |
| Container | Docker + Docker Compose |
| Testing | xUnit v3 + Moq + Microsoft.NET.Test.Sdk |

## 5. Quản lý dependency trong .NET

Nếu quen Maven/Spring, các file tương ứng trong solution này là:

- `*.csproj`: gần nhất với `pom.xml` ở cấp từng project; khai báo SDK và `PackageReference`.
- `Directory.Packages.props`: quản lý version NuGet tập trung cho toàn solution.
- `Directory.Build.props`: cấu hình build chung, gồm `net10.0`, nullable và implicit usings.
- `global.json`: chọn .NET SDK 10 và cho phép roll-forward tới feature band mới hơn của .NET 10.

## 6. Cấu trúc thư mục

Cấu trúc bám theo mục 8.1 của đề, có điều chỉnh database theo quyết định của nhóm:

```text
SwiftRide/
├── src/
│   ├── ApiGateway/
│   │   ├── ApiGateway.csproj
│   │   ├── Program.cs
│   │   └── Dockerfile
│   ├── TripService/
│   │   ├── Domain/
│   │   ├── Application/
│   │   ├── Infrastructure/
│   │   ├── Api/
│   │   ├── TripService.csproj
│   │   ├── Program.cs
│   │   └── Dockerfile
│   ├── MatchingService/
│   │   ├── Domain/
│   │   ├── Application/
│   │   ├── Infrastructure/
│   │   ├── Api/
│   │   ├── MatchingService.csproj
│   │   ├── Program.cs
│   │   └── Dockerfile
│   └── PaymentService/
│       ├── Domain/
│       ├── Application/
│       ├── Infrastructure/
│       ├── Api/
│       ├── PaymentService.csproj
│       ├── Program.cs
│       └── Dockerfile
├── tests/
│   └── TripService.Tests/
├── Directory.Build.props
├── Directory.Packages.props
├── global.json
├── docker-compose.yml
└── SwiftRide.sln
```

## 7. SOLID và design pattern định hướng

Core nên triển khai pattern vì có nhu cầu nghiệp vụ, không phải để đủ số lượng:

- **State**: vòng đời Trip và các transition hợp lệ.
- **Strategy**: Standard / Surge / Promotion pricing.
- **Repository**: trừu tượng hóa persistence cho Trip và Payment khi triển khai domain/application layer.
- **Dependency Injection / DIP**: application/domain phụ thuộc abstraction thay vì hạ tầng cụ thể.
- **Factory**: chỉ cần nếu có nhiều kiểu payment method cần khởi tạo khác nhau.

Pub/Sub, Outbox và Saga thuộc phần nâng cao sau core.

## 8. Chạy project

### Yêu cầu

- .NET 10 SDK
- Docker Desktop / Docker Engine có Docker Compose

### Restore

```bash
dotnet restore SwiftRide.sln
```

### Chạy toàn backend bằng Docker

```bash
cp .env.example .env
docker compose up --build
```

Các endpoint health mặc định:

- API Gateway: `http://localhost:5100/health`
- Trip Service: `http://localhost:5101/health`
- Matching Service: `http://localhost:5102/health`
- Payment Service: `http://localhost:5103/health`

Database local:

- Trip PostgreSQL: `localhost:5433`
- Matching MongoDB: `localhost:27017`
- Payment PostgreSQL: `localhost:5434`

### Chạy test

```bash
dotnet test SwiftRide.sln
```

## 9. Ghi chú scaffold

Package này là **khung khởi tạo**, chưa chứa implementation nghiệp vụ của Trip/Matching/Pricing/Payment. Các `Program.cs`, DbContext và health endpoint chỉ tạo baseline để nhóm bắt đầu code trên cùng một cấu trúc thống nhất.
