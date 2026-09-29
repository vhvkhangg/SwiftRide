FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["Directory.Build.props", "."]
COPY ["Directory.Packages.props", "."]
COPY ["src/MatchingService/MatchingService.csproj", "src/MatchingService/"]
RUN dotnet restore "src/MatchingService/MatchingService.csproj"
COPY . .
WORKDIR "/src/src/MatchingService"
RUN dotnet publish "MatchingService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "MatchingService.dll"]
