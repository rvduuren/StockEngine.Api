# StockEngine.Api

.NET 10 minimal API with JWT sign-in, rotating refresh tokens, role-based authorization, and Swagger in Development.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Docker (for local MariaDB)

## Run locally

```bash
docker compose up -d
dotnet tool restore
dotnet ef database update --project src/StockEngine.Api
dotnet run --project src/StockEngine.Api
```

Swagger UI: [http://localhost:5088/swagger](http://localhost:5088/swagger) (Development only).

## Development users

| Email             | Password   | Role  |
| ----------------- | ---------- | ----- |
| `admin@localhost` | `Admin123!` | Admin |
| `user@localhost`  | `User123!`  | User  |

## Auth flow

1. `POST /auth/sign-in` with `{ "email", "password" }` → access + refresh tokens.
2. Call protected routes with `Authorization: Bearer {accessToken}`.
3. `POST /auth/refresh` with `{ "refreshToken" }` when the access token expires (returns a **new** refresh token).
4. `POST /auth/sign-out` with `{ "refreshToken" }` to revoke the refresh token.

## Sample endpoints

| Route                    | Auth        |
| ------------------------ | ----------- |
| `GET /api/health`        | Anonymous   |
| `GET /api/me`            | Authenticated |
| `GET /api/admin/dashboard` | Admin role |

## Configuration

`src/StockEngine.Api/appsettings.json` holds the MariaDB connection string and JWT settings. Override `Jwt:SigningKey` via user secrets or environment variables before production.

### Database migrations

In **Development**, the API applies EF Core migrations and seeds dev users on startup (or use `dotnet ef database update` before `dotnet run` as above).

Outside Development, migrations do **not** run on startup unless `Database:ApplyMigrationsOnStartup` is `true` (environment variable `Database__ApplyMigrationsOnStartup=true`). Otherwise apply schema at deploy time with `dotnet ef database update`. Dev seeding never runs outside Development.

## Tests

```bash
dotnet test StockEngine.slnx
```

The suite includes integration tests that start **MariaDB 11.4 via Testcontainers**. **Docker** must be available locally and in CI (GitHub Actions on `ubuntu-latest` runs the full suite). Unit tests (`TokenHasher`, `RefreshToken`) do not require Docker.

Domain vocabulary: [`GLOSSARY.md`](./GLOSSARY.md). Auth stack decision: [`docs/adr/0001-identity-mariadb-rotating-refresh.md`](./docs/adr/0001-identity-mariadb-rotating-refresh.md).