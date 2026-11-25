# ADR-0001: MariaDB, Identity, and rotating refresh tokens

## Status

Accepted

## Context

StockEngine.Api needs sign-in, JWT bearer access, role-based authorization, refresh, and sign-out. The warehouse stack already standardizes on MariaDB. Refresh handling is security-sensitive and costly to change once clients ship.

## Decision

- Use **ASP.NET Core Identity** with **EF Core** on **MariaDB** via **Oracle `MySql.EntityFrameworkCore`** (the official EF provider aligned with **.NET 10** / ASP.NET Core 10) for users, password hashing, and roles.
- Register **`MariaDbMySqlHistoryRepository`** as the EF migration history repository so `dotnet ef database update` and startup migrations acquire locks using MariaDB-compatible `GET_LOCK` semantics (the stock Oracle `MySQLHistoryRepository` uses `GET_LOCK(..., -1)`, which MariaDB rejects).
- Issue **JWT access tokens** (short lifetime) and **opaque refresh tokens** stored **hashed** in the database.
- On each refresh, **rotate** the refresh token (invalidate the presented token, issue a new one).

## Consequences

- Requires a running MariaDB for non-test environments and migrations on deploy.
- Identity + EF adds dependencies but avoids custom password and user schema work.
- Clients must persist the latest refresh token after every refresh call.
- Rotating refresh enables reuse detection later without changing the public contract.

## Alternatives considered

- **Pomelo.EntityFrameworkCore.MySql**: rejected for this host on .NET 10; we standardize on the Oracle provider version-matched to ASP.NET Core 10 and patch MariaDB migration locking locally.
- **In-memory users**: rejected for v1; not a solid baseline.
- **Dapper-only user store**: rejected for v1; duplicates Identity concerns.
- **Long-lived JWT as refresh token**: rejected; harder to revoke and rotate safely.