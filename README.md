# Air Quality Monitoring

A full-stack air quality monitoring platform built with a .NET backend, PostgreSQL, Redis, Caddy, and workload/test automation using k6.

## Project overview

This repository includes:
- `backend/` — .NET 9 minimal API service for measurements, authentication, and Redis caching.
- `docker-compose.yaml` — core container orchestration for backend, Postgres, Redis, Caddy, and optional k6 workloads.
- `Caddyfile` — static map hosting and reverse proxy configuration for API routing.
- `db-init/` — PostgreSQL initialization script to create users and measurements tables.
- `map/` — static map site content served by Caddy.
- `k6-tester/` — load test script for the backend.
- `k6-demon/` — sensor simulator daemon script.

## Architecture

The main runtime services are:
- `backend` — ASP.NET Core service exposing `/api` endpoints and Swagger UI.
- `postgres` — PostgreSQL 16 database for users and measurement storage.
- `redis` — Redis cache and token/session store.
- `caddy` — web server/reverse proxy serving static content and forwarding API requests.
- `sensor-community-daemon` — optional sensor emulation via k6.
- `k6-tester` — optional backend load testing container.

## Prerequisites

- Docker
- Docker Compose
- Optional: local DNS or hosts entry for `aq.ural-net.ru` if you want to use the configured Caddy host.

## Environment

The repository includes a `.env` file with required environment values for Postgres, Redis, and the backend connection string.

Key variables:
- `REDIS_HOST`
- `REDIS_USER`
- `REDIS_USER_PASSWORD`
- `REDIS_PASSWORD`
- `POSTGRES_DB`
- `POSTGRES_USER`
- `POSTGRES_PASSWORD`
- `DB_CONNECTION_STRING`

## Run the application

Start the core services:

```powershell
docker compose --profile core up --build
```

This launches:
- `aq_backend` on `localhost:8080`
- `aq_postgres`
- `redis_container`
- `aq_caddy`

### Run tests

To also start the k6 tester container:

```powershell
docker compose --profile core --profile tests up --build
```

### Sensor daemon

To run the sensor simulation daemon:

```powershell
docker compose --profile core up --build sensor-community-daemon
```

## Accessing the app

- Backend API: `http://localhost:8080`
- Swagger UI: `http://localhost:8080/swagger`

Caddy is configured for host `aq.ural-net.ru` and serves the contents of `./map` as a static site.

## Database initialization

The PostgreSQL container loads SQL files from `db-init/` on first startup. The included script creates:
- `users`
- `measurements`

## Backend details

Project path: `backend/AirQualityMonitoring.Core/AirQualityMonitoring.Core.csproj`

The backend uses:
- ASP.NET Core minimal API
- Dapper for database access
- StackExchange.Redis
- PostgreSQL via `Npgsql`
- Swagger/OpenAPI with localization support
- Custom bearer authentication handler

## Useful directories

- `backend/` — backend source code and Dockerfile
- `db-init/` — database bootstrap SQL
- `map/` — static map site files served by Caddy
- `k6-tester/` — performance/load test scripts
- `k6-demon/` — sensor simulation scripts
- `devices-scripts/` — device firmware and sensor integration assets

## License

This repository is licensed under the terms of the included `LICENSE` file.
