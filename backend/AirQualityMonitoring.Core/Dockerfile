FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base

RUN echo "deb http://mirror.yandex.ru/debian bookworm main contrib non-free non-free-firmware" > /etc/apt/sources.list && \
    echo "deb http://mirror.yandex.ru/debian bookworm-updates main contrib non-free non-free-firmware" >> /etc/apt/sources.list && \
    echo "deb http://mirror.yandex.ru/debian-security bookworm-security main contrib non-free non-free-firmware" >> /etc/apt/sources.list && \
    apt-get update && apt-get install -y \
    libkrb5-3 \
    libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

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

USER $APP_UID

ENTRYPOINT ["dotnet", "AirQualityMonitoring.Core.dll"]