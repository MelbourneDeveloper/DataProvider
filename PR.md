# Major Release: Sync Framework, Gatekeeper Auth, and Healthcare Samples

## Summary

This release transforms DataProvider from a SQL code generator into a comprehensive data layer suite. It adds three major new components:

1. **Sync Framework** - Offline-first bidirectional synchronization engine
2. **Gatekeeper** - Passwordless authentication and fine-grained authorization microservice
3. **Healthcare Samples** - FHIR-compliant clinical and scheduling APIs with React dashboard

## Statistics

- **373 files changed**
- **~130,000 lines added**
- **~1,600 lines removed** (cleanup of obsolete specs)

## New Components

### Sync Framework (`Sync/`)
A database-agnostic, offline-first synchronization framework for .NET applications.

**Features:**
- Two-way synchronization with version-based change tracking
- Conflict resolution strategies (last-write-wins, server-wins, custom)
- Foreign key handling with automatic deferred retry
- Tombstone management for safe deletion tracking
- Real-time subscriptions via Server-Sent Events (SSE)
- SHA-256 hash verification for data integrity
- Mapping engine for heterogeneous schema sync between microservices
- Database support: SQLite and PostgreSQL

**Projects:**
| Project | Lines | Description |
|---------|-------|-------------|
| `Sync.Core` | ~3,500 | Core sync engine, coordinator, conflict resolver |
| `Sync.SQLite` | ~2,500 | SQLite triggers, schema, repositories |
| `Sync.Postgres` | ~1,200 | PostgreSQL implementation |
| `Sync.Http` | ~800 | REST endpoints with SSE |
| `Sync.Tests` | ~5,000 | Core unit tests |
| `Sync.SQLite.Tests` | ~4,500 | SQLite integration tests |
| `Sync.Postgres.Tests` | ~1,500 | PostgreSQL integration tests |
| `Sync.Http.Tests` | ~3,500 | API endpoint tests |
| `Sync.Integration.Tests` | ~1,200 | Cross-database E2E tests |

### Gatekeeper (`Gatekeeper/`)
An independent authentication and authorization microservice.

**Features:**
- Passwordless authentication with WebAuthn/FIDO2 passkeys
- Role-based access control (RBAC) with hierarchical roles
- Record-level permissions for fine-grained access
- JWT session management with revocation
- Framework-agnostic REST API

**Projects:**
| Project | Lines | Description |
|---------|-------|-------------|
| `Gatekeeper.Api` | ~1,200 | REST API with auth endpoints |
| `Gatekeeper.Migration` | ~250 | Database schema |
| `Gatekeeper.Api.Tests` | ~1,400 | Integration tests |

### Healthcare Samples (`Samples/`)
A complete demonstration of the DataProvider suite with FHIR-compliant healthcare APIs.

**Architecture:**
```
Dashboard.Web (React/H5)
       │
       ├──► Clinical.Api ◄──── Clinical.Sync ◄─┐
       │    (SQLite)                           │
       │    Patient, Encounter, Condition      │ Practitioner→Provider
       │                                       │
       └──► Scheduling.Api ◄── Scheduling.Sync ◄┘
            (SQLite)           Patient→ScheduledPatient
            Practitioner, Appointment
```

**Projects:**
| Project | Description |
|---------|-------------|
| `Clinical.Api` | FHIR Patient, Encounter, Condition, MedicationRequest |
| `Clinical.Sync` | Pulls Practitioner data from Scheduling |
| `Scheduling.Api` | FHIR Practitioner, Appointment, Schedule, Slot |
| `Scheduling.Sync` | Pulls Patient data from Clinical |
| `Dashboard.Web` | React 18 UI transpiled from C# via H5 |
| `Dashboard.Integration.Tests` | Playwright E2E tests |

## Changes to Existing Components

### DataProvider Core
- Enhanced `DbConnectionExtensions` and `DbTransactionExtensions`
- Improved code generation for table operations
- Better nullable handling in generated code
- Additional tests for edge cases

### LQL
- Minor fixes to browser playground
- Improved file operation handling

### Documentation
- Updated root README with complete suite overview
- New architecture diagrams showing component integration
- Updated CLAUDE.md with all components and coding rules
- Individual README files for Sync, Gatekeeper, and Samples

## Breaking Changes

None. This release adds new components without modifying existing APIs.

## Testing

All components include comprehensive test suites following the project's testing philosophy:

- **E2E integration tests** with real databases (no mocks)
- **Cross-database tests** for Sync (SQLite ↔ PostgreSQL)
- **Playwright tests** for Dashboard UI

Run all tests:
```bash
dotnet test
```

Run specific component:
```bash
dotnet test --filter "FullyQualifiedName~Sync"
dotnet test --filter "FullyQualifiedName~Gatekeeper"
dotnet test --filter "FullyQualifiedName~Samples"
```

## Dependencies

New package dependencies:
- `Fido2.AspNet` - WebAuthn/FIDO2 implementation for Gatekeeper
- `Npgsql` - PostgreSQL driver for Sync.Postgres
- `H5` - C# to JavaScript transpiler for Dashboard

## Documentation

- [Sync Framework README](./Sync/README.md)
- [Sync Specification](./Sync/spec.md)
- [Gatekeeper README](./Gatekeeper/README.md)
- [Gatekeeper Specification](./Gatekeeper/spec.md)
- [Samples README](./Samples/readme.md)

## Checklist

- [x] All tests pass
- [x] Code formatted with `dotnet csharpier .`
- [x] Documentation updated
- [x] No breaking changes to existing APIs
- [x] Follows coding rules (no exceptions, no classes, Result types)
