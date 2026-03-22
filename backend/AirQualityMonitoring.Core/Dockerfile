FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base

RUN apt-get update \
    && apt-get install -y libkrb5-3 \
    && rm -rf /var/lib/apt/lists/*

USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["AirQualityMonitoring.Core/AirQualityMonitoring.Core.csproj", "AirQualityMonitoring.Core/"]
RUN dotnet restore "AirQualityMonitoring.Core/AirQualityMonitoring.Core.csproj"
COPY . .
WORKDIR "/src/AirQualityMonitoring.Core"
RUN dotnet build "AirQualityMonitoring.Core.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "AirQualityMonitoring.Core.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AirQualityMonitoring.Core.dll"]
