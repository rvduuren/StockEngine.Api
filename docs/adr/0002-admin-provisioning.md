# ADR-0002: Admin-only user provisioning

## Status

Accepted

## Context

Warehouse staff need accounts without a public registration endpoint. Admins must provision users, assign warehouse job roles, and have role changes take effect under the JWT and rotating refresh token model from ADR-0001.

## Decision

- **No self-service registration** for v1; only Admins call `POST /api/admin/users` and `PUT /api/admin/users/{id}/roles`.
- **Role catalog (v1)**: `Admin`, `WarehouseManager`, `Picker`, `Packer`. `Admin` must be assigned alone; other accounts require at least one warehouse job role.
- **Provisioned users** get `EmailConfirmed = true` and sign in through the existing `/auth/sign-in` flow with the password set by the Admin.
- **Role replacement** is a full replace (not a patch). Identical role sets are a no-op and do **not** revoke refresh tokens.
- When roles **actually change**, all active refresh tokens for that user are revoked in the same database transaction as the role update. Users must refresh or sign in again to obtain tokens that embed the new roles.
- **Access tokens** still reflect roles at issuance time until they expire (short lifetime). Operators should expect a brief window of stale access permissions after a role change.
- **Guards**: an Admin cannot remove their own `Admin` role; the system rejects updates that would leave zero users with `Admin`.
- **Concurrency**: role updates are last-write-wins (no ETags in v1).

## Consequences

- Admins handle initial passwords operationally (no invite or reset flow in this milestone).
- Revoking refresh tokens on role change aligns long-lived sessions with authorization without adding token-version claims on access JWTs.
- Warehouse-specific authorization policies can build on role claims when inventory endpoints arrive.

## Alternatives considered

- **Public `/auth/register`**: rejected; provisioning must stay Admin-only for warehouse staff.
- **Email invite / magic-link onboarding**: rejected for v1 scope.
- **Security-stamp / token version on access JWTs**: rejected; short access lifetime plus refresh revocation is sufficient for v1.
- **Incremental role PATCH**: rejected; full replace is easier to reason about and idempotent in intent.