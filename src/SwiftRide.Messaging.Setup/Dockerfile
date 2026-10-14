FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["Directory.Build.props", "."]
COPY ["Directory.Packages.props", "."]
COPY ["src/SwiftRide.Contracts/SwiftRide.Contracts.csproj", "src/SwiftRide.Contracts/"]
COPY ["src/SwiftRide.Messaging/SwiftRide.Messaging.csproj", "src/SwiftRide.Messaging/"]
COPY ["src/SwiftRide.Messaging.Setup/SwiftRide.Messaging.Setup.csproj", "src/SwiftRide.Messaging.Setup/"]
RUN dotnet restore "src/SwiftRide.Messaging.Setup/SwiftRide.Messaging.Setup.csproj"
COPY . .
RUN dotnet publish "src/SwiftRide.Messaging.Setup/SwiftRide.Messaging.Setup.csproj" -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "SwiftRide.Messaging.Setup.dll"]
