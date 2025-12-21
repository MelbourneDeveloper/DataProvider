# Gatekeeper: Authentication & Authorization Microservice

## Overview

Gatekeeper is an independent, deployable authentication and authorization microservice implementing:
- **Passkey-only authentication** (WebAuthn/FIDO2) - no passwords
- **Fine-grained RBAC** with record-level permissions
- **Allows C# attributes to specify permissions or roles at code level** - distinc from .NET ABAC

This service is framework-agnostic and can be integrated with any system via REST API.

---

## Authoritative References

### WebAuthn/FIDO2 Standards
- [W3C WebAuthn Specification](https://www.w3.org/TR/webauthn-3/)
- [FIDO Alliance Technical Specifications](https://fidoalliance.org/specs/fido-v2.0-id-20180227/fido-client-to-authenticator-protocol-v2.0-id-20180227.html)
- [WebAuthn Guide (webauthn.guide)](https://webauthn.guide/)

### ASP.NET Core Implementation
- [ASP.NET Core Passkeys Documentation](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/passkeys/?view=aspnetcore-10.0)
- [fido2-net-lib GitHub](https://github.com/passwordless-lib/fido2-net-lib) - Recommended library for .NET 9
- [Syncfusion FIDO2 Tutorial](https://www.syncfusion.com/blogs/post/passkey-in-asp-dotnet-core-with-fido2)
- [damienbod/AspNetCoreIdentityFido2Mfa](https://github.com/damienbod/AspNetCoreIdentityFido2Mfa) - .NET 9 reference implementation

### React Implementation
- [SimpleWebAuthn Documentation](https://simplewebauthn.dev/docs/packages/browser/)
- [SimpleWebAuthn Server Package](https://simplewebauthn.dev/docs/packages/server)
- [Complete React + WebAuthn Guide](https://medium.com/@siddhantahire98/building-a-modern-authentication-system-with-webauthn-passkeys-a-complete-guide-65cac3511049)

### Access Control Design
- [NocoBase RBAC Design Guide](https://www.nocobase.com/en/blog/how-to-design-rbac-role-based-access-control-system)
- [Oso RBAC Layer Guide](https://www.osohq.com/learn/rbac-role-based-access-control)
- [SQLFlash Fine-Grained RBAC](https://sqlflash.ai/article/20250617-2/)
- [Hoop.dev Fine-Grained Access Control](https://hoop.dev/blog/fine-grained-access-control-and-rbac-building-secure-and-scalable-permission-systems/)
- [Permify Fine-Grained Access](https://permify.co/post/fine-grained-access-control-where-rbac-falls-short/)

---

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                     Client Application                          │
│                   (React Dashboard, etc.)                       │
│                                                                 │
│  ┌─────────────────┐    ┌─────────────────┐                    │
│  │ @simplewebauthn │    │ API Client      │                    │
│  │ /browser        │    │ (fetch/axios)   │                    │
│  └────────┬────────┘    └────────┬────────┘                    │
└───────────┼──────────────────────┼──────────────────────────────┘
            │                      │
            ▼                      ▼
┌─────────────────────────────────────────────────────────────────┐
│                    Gatekeeper API (:5002)                       │
│                                                                 │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │                  Authentication Layer                    │   │
│  │  POST /auth/register/begin     - Start passkey creation │   │
│  │  POST /auth/register/complete  - Finish registration    │   │
│  │  POST /auth/login/begin        - Start authentication   │   │
│  │  POST /auth/login/complete     - Finish authentication  │   │
│  │  POST /auth/logout             - Invalidate session     │   │
│  │  GET  /auth/session            - Get current session    │   │
│  └─────────────────────────────────────────────────────────┘   │
│                                                                 │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │                  Authorization Layer                     │   │
│  │  GET  /authz/check             - Check permission       │   │
│  │  GET  /authz/permissions       - List user permissions  │   │
│  │  POST /authz/evaluate          - Bulk permission check  │   │
│  └─────────────────────────────────────────────────────────┘   │
│                                                                 │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │                  Admin API (protected)                   │   │
│  │  /admin/users      - User management                    │   │
│  │  /admin/roles      - Role management                    │   │
│  │  /admin/permissions - Permission management             │   │
│  │  /admin/policies   - ABAC policy management             │   │
│  └─────────────────────────────────────────────────────────┘   │
│                                                                 │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │              fido2-net-lib (Fido2.AspNet)               │   │
│  │         Attestation & Assertion verification            │   │
│  └─────────────────────────────────────────────────────────┘   │
└────────────────────────────┬────────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────────┐
│                       Database                           │
│                                                                 │
│  Users ──┬── Credentials (passkeys)                            │
│          ├── UserRoles ── Roles ── RolePermissions             │
│          └── UserPermissions (direct grants)                   │
│                              │                                  │
│                              ▼                                  │
│  Permissions ── ResourceType + Action + Scope                  │
│                                                                 │
│  Policies (ABAC) ── Conditions + Attributes                    │
└─────────────────────────────────────────────────────────────────┘
```

---

## Database Schema

Defined using the Migration SchemaBuilder (database-independent metadata):

```csharp
using static Migration.PortableTypes;

public static class GatekeeperSchema
{
    public static SchemaDefinition Build() => Schema.Define("gatekeeper")

        // ═══════════════════════════════════════════════════════════════
        // CORE AUTHENTICATION TABLES
        // ═══════════════════════════════════════════════════════════════

        // Users (minimal - no password!)
        .Table("gk_user", t => t
            .Column("id", Text, c => c.PrimaryKey())
            .Column("display_name", Text, c => c.NotNull())
            .Column("email", Text)
            .Column("created_at", Text, c => c.NotNull())
            .Column("last_login_at", Text)
            .Column("is_active", Boolean, c => c.NotNull().Default("1"))
            .Column("metadata", Json)  // Extensibility
            .Index("idx_user_email", "email", unique: true))

        // WebAuthn Credentials (passkeys)
        .Table("gk_credential", t => t
            .Column("id", Text, c => c.PrimaryKey())  // Base64URL credential ID
            .Column("user_id", Text, c => c.NotNull())
            .Column("public_key", Blob, c => c.NotNull())  // COSE public key
            .Column("sign_count", Int, c => c.NotNull().Default("0"))
            .Column("aaguid", Text)  // Authenticator AAGUID
            .Column("credential_type", Text, c => c.NotNull())  // 'public-key'
            .Column("transports", Json)  // ['internal', 'usb', 'ble', 'nfc']
            .Column("attestation_format", Text)
            .Column("created_at", Text, c => c.NotNull())
            .Column("last_used_at", Text)
            .Column("device_name", Text)  // User-friendly name
            .Column("is_backup_eligible", Boolean)  // BE flag
            .Column("is_backed_up", Boolean)  // BS flag
            .ForeignKey("user_id", "gk_user", "id", onDelete: ForeignKeyAction.Cascade)
            .Index("idx_credential_user", "user_id"))

        // Sessions (stateful for revocation support)
        .Table("gk_session", t => t
            .Column("id", Text, c => c.PrimaryKey())
            .Column("user_id", Text, c => c.NotNull())
            .Column("credential_id", Text)
            .Column("created_at", Text, c => c.NotNull())
            .Column("expires_at", Text, c => c.NotNull())
            .Column("last_activity_at", Text, c => c.NotNull())
            .Column("ip_address", Text)
            .Column("user_agent", Text)
            .Column("is_revoked", Boolean, c => c.NotNull().Default("0"))
            .ForeignKey("user_id", "gk_user", "id", onDelete: ForeignKeyAction.Cascade)
            .ForeignKey("credential_id", "gk_credential", "id")
            .Index("idx_session_user", "user_id")
            .Index("idx_session_expires", "expires_at"))

        // WebAuthn Challenge Store (temporary, for registration/login)
        .Table("gk_challenge", t => t
            .Column("id", Text, c => c.PrimaryKey())
            .Column("user_id", Text)  // NULL for login challenges
            .Column("challenge", Blob, c => c.NotNull())
            .Column("type", Text, c => c.NotNull())  // 'registration' or 'authentication'
            .Column("created_at", Text, c => c.NotNull())
            .Column("expires_at", Text, c => c.NotNull()))

        // ═══════════════════════════════════════════════════════════════
        // RBAC TABLES
        // ═══════════════════════════════════════════════════════════════

        // Roles
        .Table("gk_role", t => t
            .Column("id", Text, c => c.PrimaryKey())
            .Column("name", Text, c => c.NotNull())
            .Column("description", Text)
            .Column("is_system", Boolean, c => c.NotNull().Default("0"))  // Cannot delete system roles
            .Column("created_at", Text, c => c.NotNull())
            .Column("parent_role_id", Text)  // Role hierarchy
            .ForeignKey("parent_role_id", "gk_role", "id")
            .Index("idx_role_name", "name", unique: true))

        // User-Role assignments
        .Table("gk_user_role", t => t
            .Column("user_id", Text, c => c.NotNull())
            .Column("role_id", Text, c => c.NotNull())
            .Column("granted_at", Text, c => c.NotNull())
            .Column("granted_by", Text)
            .Column("expires_at", Text)  // Optional temporal grants
            .CompositePrimaryKey("user_id", "role_id")
            .ForeignKey("user_id", "gk_user", "id", onDelete: ForeignKeyAction.Cascade)
            .ForeignKey("role_id", "gk_role", "id", onDelete: ForeignKeyAction.Cascade)
            .ForeignKey("granted_by", "gk_user", "id"))

        // Permissions (the actual capabilities)
        .Table("gk_permission", t => t
            .Column("id", Text, c => c.PrimaryKey())
            .Column("code", Text, c => c.NotNull())  // e.g., 'patient:read', 'order:write'
            .Column("resource_type", Text, c => c.NotNull())  // e.g., 'patient', 'order', 'menu'
            .Column("action", Text, c => c.NotNull())  // e.g., 'read', 'write', 'delete', 'access'
            .Column("description", Text)
            .Column("created_at", Text, c => c.NotNull())
            .Index("idx_permission_code", "code", unique: true)
            .Index("idx_permission_resource", "resource_type"))

        // Role-Permission assignments
        .Table("gk_role_permission", t => t
            .Column("role_id", Text, c => c.NotNull())
            .Column("permission_id", Text, c => c.NotNull())
            .Column("granted_at", Text, c => c.NotNull())
            .CompositePrimaryKey("role_id", "permission_id")
            .ForeignKey("role_id", "gk_role", "id", onDelete: ForeignKeyAction.Cascade)
            .ForeignKey("permission_id", "gk_permission", "id", onDelete: ForeignKeyAction.Cascade))

        // Direct user-permission grants (bypass roles for exceptions)
        .Table("gk_user_permission", t => t
            .Column("user_id", Text, c => c.NotNull())
            .Column("permission_id", Text, c => c.NotNull())
            .Column("scope_type", Text)  // 'all', 'record', 'query'
            .Column("scope_value", Text)  // Record ID, LQL query, or NULL for all
            .Column("granted_at", Text, c => c.NotNull())
            .Column("granted_by", Text)
            .Column("expires_at", Text)
            .Column("reason", Text)  // Why this exception was granted
            .ForeignKey("user_id", "gk_user", "id", onDelete: ForeignKeyAction.Cascade)
            .ForeignKey("permission_id", "gk_permission", "id", onDelete: ForeignKeyAction.Cascade)
            .ForeignKey("granted_by", "gk_user", "id")
            .Index("idx_user_permission", ["user_id", "permission_id", "scope_value"], unique: true))

        // ═══════════════════════════════════════════════════════════════
        // FINE-GRAINED ACCESS CONTROL
        // ═══════════════════════════════════════════════════════════════

        // Resource-level permissions (record-level access)
        .Table("gk_resource_grant", t => t
            .Column("id", Text, c => c.PrimaryKey())
            .Column("user_id", Text, c => c.NotNull())
            .Column("resource_type", Text, c => c.NotNull())  // e.g., 'order', 'patient'
            .Column("resource_id", Text, c => c.NotNull())  // e.g., '123456', 'patient-uuid'
            .Column("permission_id", Text, c => c.NotNull())
            .Column("granted_at", Text, c => c.NotNull())
            .Column("granted_by", Text)
            .Column("expires_at", Text)
            .ForeignKey("user_id", "gk_user", "id", onDelete: ForeignKeyAction.Cascade)
            .ForeignKey("permission_id", "gk_permission", "id")
            .ForeignKey("granted_by", "gk_user", "id")
            .Index("idx_resource_grant_user", "user_id")
            .Index("idx_resource_grant_resource", ["resource_type", "resource_id"])
            .Unique("uq_resource_grant", "user_id", "resource_type", "resource_id", "permission_id"))

        // Policies (conditional access rules)
        .Table("gk_policy", t => t
            .Column("id", Text, c => c.PrimaryKey())
            .Column("name", Text, c => c.NotNull())
            .Column("description", Text)
            .Column("resource_type", Text, c => c.NotNull())
            .Column("action", Text, c => c.NotNull())
            .Column("condition", Json, c => c.NotNull())  // JSON condition expression
            .Column("effect", Text, c => c.NotNull().Default("'allow'"))  // 'allow' or 'deny'
            .Column("priority", Int, c => c.NotNull().Default("0"))  // Higher = evaluated first
            .Column("is_active", Boolean, c => c.NotNull().Default("1"))
            .Column("created_at", Text, c => c.NotNull())
            .Index("idx_policy_name", "name", unique: true))

        .Build();
}

// Example policy conditions (stored as JSON in 'condition' field):
// { "user.department": "finance", "resource.status": "draft" }
// { "time.hour": { "$gte": 9, "$lte": 17 } }
// { "user.id": { "$eq": "resource.owner_id" } }
```

---

## API Specification

### Authentication Endpoints

#### POST /auth/register/begin
Start passkey registration for a new or existing user.

**Request:**
```json
{
  "email": "user@example.com",
  "displayName": "John Doe"
}
```

**Response:**
```json
{
  "challengeId": "uuid",
  "options": {
    "challenge": "base64url-encoded-challenge",
    "rp": {
      "name": "Gatekeeper",
      "id": "localhost"
    },
    "user": {
      "id": "base64url-user-id",
      "name": "user@example.com",
      "displayName": "John Doe"
    },
    "pubKeyCredParams": [
      { "type": "public-key", "alg": -7 },
      { "type": "public-key", "alg": -257 }
    ],
    "timeout": 60000,
    "attestation": "none",
    "authenticatorSelection": {
      "authenticatorAttachment": "platform",
      "residentKey": "required",
      "userVerification": "required"
    }
  }
}
```

#### POST /auth/register/complete
Complete passkey registration with authenticator response.

**Request:**
```json
{
  "challengeId": "uuid",
  "response": {
    "id": "credential-id",
    "rawId": "base64url",
    "type": "public-key",
    "response": {
      "clientDataJSON": "base64url",
      "attestationObject": "base64url"
    }
  },
  "deviceName": "MacBook Pro Touch ID"
}
```

**Response:**
```json
{
  "userId": "user-uuid",
  "credentialId": "credential-id",
  "session": {
    "token": "session-token",
    "expiresAt": "2025-12-22T00:00:00Z"
  }
}
```

#### POST /auth/login/begin
Start passkey authentication.

**Request:**
```json
{
  "email": "user@example.com"  // Optional - for discoverable credentials
}
```

**Response:**
```json
{
  "challengeId": "uuid",
  "options": {
    "challenge": "base64url-encoded-challenge",
    "timeout": 60000,
    "rpId": "localhost",
    "allowCredentials": [],  // Empty for discoverable credentials
    "userVerification": "required"
  }
}
```

#### POST /auth/login/complete
Complete passkey authentication.

**Request:**
```json
{
  "challengeId": "uuid",
  "response": {
    "id": "credential-id",
    "rawId": "base64url",
    "type": "public-key",
    "response": {
      "clientDataJSON": "base64url",
      "authenticatorData": "base64url",
      "signature": "base64url",
      "userHandle": "base64url"
    }
  }
}
```

**Response:**
```json
{
  "userId": "user-uuid",
  "displayName": "John Doe",
  "session": {
    "token": "session-token",
    "expiresAt": "2025-12-22T00:00:00Z"
  }
}
```

#### GET /auth/session
Get current session info.

**Headers:** `Authorization: Bearer <session-token>`

**Response:**
```json
{
  "userId": "user-uuid",
  "displayName": "John Doe",
  "email": "user@example.com",
  "roles": ["admin", "clinician"],
  "expiresAt": "2025-12-22T00:00:00Z"
}
```

#### POST /auth/logout
Invalidate current session.

**Headers:** `Authorization: Bearer <session-token>`

**Response:** `204 No Content`

---

### Authorization Endpoints

#### GET /authz/check
Check if current user has a specific permission.

**Headers:** `Authorization: Bearer <session-token>`

**Query Parameters:**
- `permission` - Permission code (e.g., `patient:read`)
- `resourceType` - Optional resource type
- `resourceId` - Optional specific resource ID

**Response:**
```json
{
  "allowed": true,
  "reason": "role:admin grants patient:read",
  "evaluatedPolicies": ["default-admin-policy"]
}
```

#### POST /authz/evaluate
Bulk permission evaluation.

**Request:**
```json
{
  "checks": [
    { "permission": "patient:read", "resourceId": "patient-123" },
    { "permission": "patient:write", "resourceId": "patient-123" },
    { "permission": "order:delete", "resourceId": "order-456" }
  ]
}
```

**Response:**
```json
{
  "results": [
    { "permission": "patient:read", "resourceId": "patient-123", "allowed": true },
    { "permission": "patient:write", "resourceId": "patient-123", "allowed": true },
    { "permission": "order:delete", "resourceId": "order-456", "allowed": false }
  ]
}
```

#### GET /authz/permissions
List all effective permissions for current user.

**Response:**
```json
{
  "permissions": [
    {
      "code": "patient:read",
      "source": "role:clinician",
      "scope": "all"
    },
    {
      "code": "patient:write",
      "source": "direct-grant",
      "scope": "record",
      "scopeValue": "patient-123"
    }
  ]
}
```

---

### Admin Endpoints

All admin endpoints require the `admin:*` permission.

#### Users
- `GET /admin/users` - List users
- `GET /admin/users/{id}` - Get user details
- `POST /admin/users` - Create user (generates registration link)
- `PUT /admin/users/{id}` - Update user
- `DELETE /admin/users/{id}` - Deactivate user
- `GET /admin/users/{id}/credentials` - List user's passkeys
- `DELETE /admin/users/{id}/credentials/{credentialId}` - Revoke passkey

#### Roles
- `GET /admin/roles` - List roles
- `GET /admin/roles/{id}` - Get role with permissions
- `POST /admin/roles` - Create role
- `PUT /admin/roles/{id}` - Update role
- `DELETE /admin/roles/{id}` - Delete role (if not system role)
- `POST /admin/roles/{id}/permissions` - Add permission to role
- `DELETE /admin/roles/{id}/permissions/{permissionId}` - Remove permission

#### Permissions
- `GET /admin/permissions` - List permissions
- `POST /admin/permissions` - Create permission
- `DELETE /admin/permissions/{id}` - Delete permission

#### User Grants
- `POST /admin/users/{id}/roles` - Assign role to user
- `DELETE /admin/users/{id}/roles/{roleId}` - Remove role
- `POST /admin/users/{id}/permissions` - Direct permission grant
- `DELETE /admin/users/{id}/permissions/{permissionId}` - Revoke grant
- `POST /admin/users/{id}/resources` - Grant resource-level access
- `DELETE /admin/users/{id}/resources/{grantId}` - Revoke resource access

---

## Project Structure

```
Samples/
└── Gatekeeper/
    ├── spec.md                          # This file
    ├── Gatekeeper.Api/
    │   ├── Gatekeeper.Api.csproj
    │   ├── Program.cs                   # Minimal API setup
    │   ├── GlobalUsings.cs
    │   ├── Endpoints/
    │   │   ├── AuthEndpoints.cs         # /auth/* routes
    │   │   ├── AuthzEndpoints.cs        # /authz/* routes
    │   │   └── AdminEndpoints.cs        # /admin/* routes
    │   ├── Services/
    │   │   ├── PasskeyService.cs        # fido2-net-lib wrapper
    │   │   ├── SessionService.cs        # Session management
    │   │   ├── AuthorizationService.cs  # Permission evaluation
    │   │   └── PolicyEvaluator.cs       # ABAC policy engine
    │   ├── Middleware/
    │   │   └── AuthMiddleware.cs        # Session validation
    │   ├── Sql/
    │   │   ├── GetUserByEmail.sql
    │   │   ├── GetUserCredentials.sql
    │   │   ├── InsertCredential.sql
    │   │   ├── GetUserPermissions.sql
    │   │   ├── CheckResourceGrant.sql
    │   │   └── ... (DataProvider SQL files)
    │   └── gatekeeper.db
    │
    ├── Gatekeeper.Api.Tests/
    │   ├── Gatekeeper.Api.Tests.csproj
    │   ├── AuthenticationTests.cs
    │   ├── AuthorizationTests.cs
    │   └── PermissionTests.cs
    │
    └── Gatekeeper.Migration/
        ├── Gatekeeper.Migration.csproj
        └── Schema.cs                     # Migration SchemaBuilder
```

---

## Implementation Guide

### Dependencies (NuGet)

```xml
<!-- Gatekeeper.Api.csproj -->
<PackageReference Include="Fido2" Version="4.*" />
<PackageReference Include="Fido2.AspNet" Version="4.*" />
<PackageReference Include="Microsoft.Data.Sqlite" Version="9.*" />
```

### FIDO2 Configuration

```csharp
// Program.cs
builder.Services.AddFido2(options =>
{
    options.ServerDomain = builder.Configuration["Fido2:ServerDomain"] ?? "localhost";
    options.ServerName = "Gatekeeper";
    options.Origins = new HashSet<string>
    {
        builder.Configuration["Fido2:Origin"] ?? "http://localhost:5173"
    };
    options.TimestampDriftTolerance = 300000; // 5 minutes
});
```

### React Integration

```typescript
// Using @simplewebauthn/browser
import {
  startRegistration,
  startAuthentication
} from '@simplewebauthn/browser';

// Registration
async function registerPasskey() {
  const beginResp = await fetch('/auth/register/begin', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, displayName })
  });
  const { challengeId, options } = await beginResp.json();

  // Trigger browser passkey creation
  const credential = await startRegistration(options);

  const completeResp = await fetch('/auth/register/complete', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ challengeId, response: credential })
  });

  return completeResp.json();
}

// Authentication
async function loginWithPasskey() {
  const beginResp = await fetch('/auth/login/begin', { method: 'POST' });
  const { challengeId, options } = await beginResp.json();

  const assertion = await startAuthentication(options);

  const completeResp = await fetch('/auth/login/complete', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ challengeId, response: assertion })
  });

  return completeResp.json();
}
```

---

## Permission Model Examples

### High-Level Permissions (Menu Access)
```
menu:dashboard:access     - Can see dashboard
menu:patients:access      - Can see patients menu
menu:admin:access         - Can see admin menu
```

### Resource Permissions (CRUD)
```
patient:create            - Can create patients
patient:read              - Can read any patient
patient:write             - Can update any patient
patient:delete            - Can delete any patient

order:create
order:read
order:write
order:delete
```

### Record-Level Permissions
Granted via `POST /admin/users/{id}/resources`:
```json
// User can only read order 123456
{
  "resourceType": "order",
  "resourceId": "123456",
  "permissionCode": "order:read"
}

// User can write to order 54345
{
  "resourceType": "order",
  "resourceId": "54345",
  "permissionCode": "order:write"
}
```

### ABAC Policy Examples
```json
// Policy: Users can only edit resources they own
{
  "name": "owner-edit-policy",
  "resource_type": "*",
  "action": "write",
  "condition": {
    "user.id": { "$eq": "resource.owner_id" }
  },
  "effect": "allow"
}

// Policy: Finance users can only access finance resources during business hours
{
  "name": "finance-time-restriction",
  "resource_type": "finance_report",
  "action": "*",
  "condition": {
    "$and": [
      { "user.department": "finance" },
      { "context.hour": { "$gte": 9, "$lte": 17 } },
      { "context.day_of_week": { "$in": [1, 2, 3, 4, 5] } }
    ]
  },
  "effect": "allow"
}

// Policy: Deny access to archived records except for admins
{
  "name": "archived-restriction",
  "resource_type": "*",
  "action": "*",
  "condition": {
    "$and": [
      { "resource.status": "archived" },
      { "user.roles": { "$nin": ["admin"] } }
    ]
  },
  "effect": "deny",
  "priority": 100
}
```

---

## Authorization Decision Flow

```
┌─────────────────────────────────────────────────────────────────┐
│                    Authorization Request                        │
│   User: user-123, Permission: order:write, Resource: order-456 │
└────────────────────────────┬────────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────────┐
│ Step 1: Check explicit DENY policies (highest priority first)  │
│   → If any DENY matches → DENY                                 │
└────────────────────────────┬────────────────────────────────────┘
                             │ No DENY
                             ▼
┌─────────────────────────────────────────────────────────────────┐
│ Step 2: Check resource-level grants (gk_resource_grant)        │
│   → SELECT * FROM gk_resource_grant                            │
│     WHERE user_id = ? AND resource_type = ?                    │
│       AND resource_id = ? AND permission_id = ?                │
│   → If found and not expired → ALLOW                           │
└────────────────────────────┬────────────────────────────────────┘
                             │ Not found
                             ▼
┌─────────────────────────────────────────────────────────────────┐
│ Step 3: Check direct user permissions (gk_user_permission)     │
│   → If scope='all' → ALLOW                                     │
│   → If scope='record' and scope_value matches → ALLOW          │
└────────────────────────────┬────────────────────────────────────┘
                             │ Not found
                             ▼
┌─────────────────────────────────────────────────────────────────┐
│ Step 4: Check role permissions (gk_role_permission via roles)  │
│   → Traverse role hierarchy                                    │
│   → If permission found in any role → ALLOW                    │
└────────────────────────────┬────────────────────────────────────┘
                             │ Not found
                             ▼
┌─────────────────────────────────────────────────────────────────┐
│ Step 5: Evaluate ABAC ALLOW policies                           │
│   → If any ALLOW policy matches → ALLOW                        │
└────────────────────────────┬────────────────────────────────────┘
                             │ No match
                             ▼
┌─────────────────────────────────────────────────────────────────┐
│                      Default: DENY                              │
└─────────────────────────────────────────────────────────────────┘
```

---

## Security Considerations

1. **No Password Storage** - Only public keys stored; private keys never leave user devices
2. **Challenge Expiry** - All challenges expire after 5 minutes
3. **Session Rotation** - Sessions can be invalidated server-side
4. **Sign Count Verification** - Detect cloned authenticators
5. **User Verification Required** - Biometric/PIN required for all operations
6. **Audit Logging** - All authentication and authorization events logged

---

## Integration with Other Services

Other microservices integrate via middleware:

```csharp
// In Clinical.Api or Scheduling.Api
app.Use(async (context, next) =>
{
    var token = context.Request.Headers["Authorization"]
        .ToString()
        .Replace("Bearer ", "");

    if (string.IsNullOrEmpty(token))
    {
        context.Response.StatusCode = 401;
        return;
    }

    // Validate with Gatekeeper
    using var client = new HttpClient();
    var response = await client.GetAsync(
        $"http://localhost:5002/auth/session",
        new HttpRequestMessage { Headers = { Authorization = new("Bearer", token) } }
    );

    if (!response.IsSuccessStatusCode)
    {
        context.Response.StatusCode = 401;
        return;
    }

    var session = await response.Content.ReadFromJsonAsync<SessionInfo>();
    context.Items["User"] = session;

    await next();
});
```

Or via shared library:
```csharp
// Gatekeeper.Client library
services.AddGatekeeperAuth(options =>
{
    options.GatekeeperUrl = "http://localhost:5002";
});

// Then in endpoints:
app.MapGet("/fhir/Patient", async (HttpContext ctx) =>
{
    var authz = ctx.RequestServices.GetRequiredService<IGatekeeperClient>();
    if (!await authz.CheckAsync("patient:read"))
        return Results.Forbid();

    // ... handle request
});
```

---

## Default Roles & Permissions

Seeded via `Gatekeeper.Migration` on first run:

| Role | Description | System? |
|------|-------------|---------|
| `admin` | Full system access | Yes |
| `user` | Basic authenticated user | Yes |

| Permission Code | Resource | Action | Description |
|-----------------|----------|--------|-------------|
| `admin:*` | admin | * | Full admin access |
| `user:profile` | user | read | View own profile |
| `user:credentials` | user | manage | Manage own passkeys |

| Role | Permissions |
|------|-------------|
| `admin` | `admin:*` |
| `user` | `user:profile`, `user:credentials` |

---

## Sync Support

Gatekeeper integrates with the existing Sync infrastructure for multi-node deployments. Sync triggers are enabled on permission tables via the `Sync.SQLite` schema extensions (uses existing `Sync.Http` infrastructure).

---

## Open Questions

1. **Token Format**: JWT vs opaque session tokens?
   - Current spec uses opaque tokens for server-side revocation
   - JWT could be added for stateless verification in edge cases

2. **Cross-Origin Passkeys**: Support for passkeys across subdomains?
   - Requires careful RP ID configuration

3. **Recovery Flow**: What happens if user loses all devices?
   - Admin-initiated account recovery?
   - Backup codes? (against passkey-only philosophy)

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0.0 | 2025-12-21 | Initial specification |
