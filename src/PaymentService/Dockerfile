FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0.401 AS build
WORKDIR /src
COPY ["Directory.Build.props", "."]
COPY ["Directory.Packages.props", "."]
COPY ["src/PaymentService/PaymentService.csproj", "src/PaymentService/"]
COPY ["src/SwiftRide.Contracts/SwiftRide.Contracts.csproj", "src/SwiftRide.Contracts/"]
COPY ["src/SwiftRide.Messaging/SwiftRide.Messaging.csproj", "src/SwiftRide.Messaging/"]
RUN dotnet restore "src/PaymentService/PaymentService.csproj"
COPY . .
WORKDIR "/src/src/PaymentService"
RUN dotnet publish "PaymentService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "PaymentService.dll"]
