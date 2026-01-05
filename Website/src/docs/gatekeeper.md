---
layout: "layouts/docs.njk"
title: "Gatekeeper"
description: "WebAuthn authentication and role-based access control."
---

An independent authentication and authorization microservice implementing passkey-only authentication (WebAuthn/FIDO2) and fine-grained role-based access control with record-level permissions.

NOTE: There seems to be a mistake here. ABAC was not supposed to be part of this technology but was implemented anyway. We will need to remove this.

## Overview

Gatekeeper provides:

- **Passwordless authentication** - WebAuthn/FIDO2 passkeys only, no passwords
- **Role-based access control (RBAC)** - Hierarchical roles with permission inheritance
- **Record-level permissions** - Fine-grained access to specific resources
- **JWT sessions** - Stateless session management with refresh tokens
- **Framework-agnostic** - REST API for integration with any system

## Projects

| Project | Description |
|---------|-------------|
| `Gatekeeper.Api` | REST API with WebAuthn and authorization endpoints |
| `Gatekeeper.Migration` | Database schema using DataProvider migrations |
| `Gatekeeper.Api.Tests` | Integration tests |

## Getting Started

### Prerequisites

- .NET 9.0 SDK
- SQLite (default) or PostgreSQL

### Run the API

```bash
cd Gatekeeper/Gatekeeper.Api
dotnet run
```

The API starts on `http://localhost:5002`.

### Database Setup

The database is automatically created on first run. To reset:

```bash
rm gatekeeper.db
dotnet run
```

## API Endpoints

### Authentication

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/auth/register/begin` | POST | Start passkey registration |
| `/auth/register/complete` | POST | Complete passkey registration |
| `/auth/login/begin` | POST | Start passkey authentication |
| `/auth/login/complete` | POST | Complete authentication, returns JWT |
| `/auth/logout` | POST | Revoke current session |
| `/auth/session` | GET | Get current session info |

### Authorization

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/authz/check` | GET | Check if user has permission |
| `/authz/permissions` | GET | List user's effective permissions |
| `/authz/evaluate` | POST | Bulk permission check |

### Admin (requires admin role)

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/admin/users` | GET/POST | User management |
| `/admin/roles` | GET/POST | Role management |
| `/admin/permissions` | GET/POST | Permission management |

## Usage Examples

### Register a Passkey

```bash
# 1. Begin registration
curl -X POST http://localhost:5002/auth/register/begin \
  -H "Content-Type: application/json" \
  -d '{"email": "user@example.com", "displayName": "John Doe"}'

# Response contains WebAuthn options for the browser
# 2. Browser calls navigator.credentials.create() with options
# 3. Complete registration with authenticator response
curl -X POST http://localhost:5002/auth/register/complete \
  -H "Content-Type: application/json" \
  -d '{"challengeId": "...", "response": {...}}'
```

### Authenticate

```bash
# 1. Begin login
curl -X POST http://localhost:5002/auth/login/begin \
  -H "Content-Type: application/json" \
  -d '{"email": "user@example.com"}'

# 2. Browser calls navigator.credentials.get() with options
# 3. Complete login
curl -X POST http://localhost:5002/auth/login/complete \
  -H "Content-Type: application/json" \
  -d '{"challengeId": "...", "response": {...}}'

# Response: {"token": "eyJ...", "expiresAt": "..."}
```

### Check Permission

```bash
curl "http://localhost:5002/authz/check?resource=patient&action=read&resourceId=123" \
  -H "Authorization: Bearer eyJ..."

# Response: {"allowed": true, "reason": "Role: physician"}
```

## Database Schema

```
gk_user ──┬── gk_credential (passkeys)
          ├── gk_session (active sessions)
          ├── gk_user_role ── gk_role ── gk_role_permission
          ├── gk_user_permission (direct grants)
          └── gk_resource_grant (record-level access)
                                    │
                                    ▼
                              gk_permission
```

### Key Tables

| Table | Purpose |
|-------|---------|
| `gk_user` | User accounts (id, email, display_name) |
| `gk_credential` | WebAuthn credentials (public_key, sign_count) |
| `gk_session` | Active JWT sessions with revocation |
| `gk_role` | Roles with optional parent hierarchy |
| `gk_permission` | Permissions (resource_type + action) |
| `gk_resource_grant` | Record-level permission grants |

## Permission Model

### RBAC

```
admin (role)
  └── user:manage (permission)
  └── role:manage (permission)

physician (role)
  └── patient:read (permission)
  └── patient:write (permission)
```

### Record-Level

```
User "dr.smith" has "patient:read" on Patient "patient-123"
```

## Configuration

Environment variables:

| Variable | Default | Description |
|----------|---------|-------------|
| `JWT_SECRET` | (generated) | Secret for JWT signing |
| `JWT_ISSUER` | `Gatekeeper` | JWT issuer claim |
| `JWT_AUDIENCE` | `GatekeeperClients` | JWT audience claim |
| `JWT_EXPIRY_MINUTES` | `60` | Token expiration |
| `DATABASE_PATH` | `gatekeeper.db` | SQLite database path |
| `WEBAUTHN_RP_ID` | `localhost` | WebAuthn Relying Party ID |
| `WEBAUTHN_RP_NAME` | `Gatekeeper` | WebAuthn Relying Party name |
| `WEBAUTHN_ORIGIN` | `http://localhost:5002` | Expected origin |

## Testing

```bash
# Run all Gatekeeper tests
dotnet test --filter "FullyQualifiedName~Gatekeeper"

# Specific test class
dotnet test --filter "FullyQualifiedName~AuthorizationTests"
```

## Design Principles

Following the repository's coding rules:

- **No exceptions** - Returns `Result<T, GatekeeperError>` types
- **No classes** - Uses records and static methods
- **No interfaces** - Uses `Func<T>` for abstractions
- **Integration tests** - Real database, no mocks
- **DataProvider** - All SQL via generated extension methods

## References

### WebAuthn/FIDO2
- [W3C WebAuthn Specification](https://www.w3.org/TR/webauthn-3/)
- [fido2-net-lib](https://github.com/passwordless-lib/fido2-net-lib)
- [SimpleWebAuthn](https://simplewebauthn.dev/docs/)

### Access Control
- [NocoBase RBAC Guide](https://www.nocobase.com/en/blog/how-to-design-rbac-role-based-access-control-system)
- [Permify Fine-Grained Access](https://permify.co/post/fine-grained-access-control-where-rbac-falls-short/)

## License

See repository root for license information.
