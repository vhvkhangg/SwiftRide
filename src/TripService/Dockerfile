FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["Directory.Build.props", "."]
COPY ["Directory.Packages.props", "."]
COPY ["src/TripService/TripService.csproj", "src/TripService/"]
RUN dotnet restore "src/TripService/TripService.csproj"
COPY . .
WORKDIR "/src/src/TripService"
RUN dotnet publish "TripService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "TripService.dll"]
