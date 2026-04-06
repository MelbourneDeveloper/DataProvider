# Sync Framework

A database-agnostic, offline-first synchronization framework for .NET. Enables two-way data sync between distributed replicas with conflict resolution, tombstone management, and real-time subscriptions.

## Projects

| Project | Description |
|---------|-------------|
| `Sync` | Core synchronization engine |
| `Sync.SQLite` | SQLite implementation |
| `Sync.Postgres` | PostgreSQL implementation |
| `Sync.Api` | REST API server with SSE subscriptions |
| `Sync.Tests` | Core engine tests |
| `Sync.SQLite.Tests` | SQLite integration tests |
| `Sync.Postgres.Tests` | PostgreSQL integration tests |
| `Sync.Api.Tests` | API endpoint tests |
| `Sync.Integration.Tests` | Cross-database E2E tests |

## Documentation

- Full specification: [docs/specs/sync-spec.md](../docs/specs/sync-spec.md)
