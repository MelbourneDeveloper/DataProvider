# Sync Framework

A database-agnostic, **offline-first** synchronisation framework for .NET. Two-way data sync between distributed replicas with conflict resolution, tombstone management, and real-time subscriptions via Server-Sent Events.

Use cases:

- Mobile and web apps that must keep working without a network
- Microservices that need to share domain entities across databases
- Multi-region deployments with eventual consistency

## Install

```bash
dotnet add package Nimblesite.Sync.Core     --version ${NIMBLESITE_VERSION}
dotnet add package Nimblesite.Sync.Http     --version ${NIMBLESITE_VERSION}
dotnet add package Nimblesite.Sync.Postgres --version ${NIMBLESITE_VERSION}
dotnet add package Nimblesite.Sync.SQLite   --version ${NIMBLESITE_VERSION}
```

## Projects

| Project | Description |
|---------|-------------|
| `Nimblesite.Sync.Core` | Core synchronisation engine |
| `Nimblesite.Sync.Http` | HTTP transport client/server |
| `Nimblesite.Sync.SQLite` | SQLite replica provider |
| `Nimblesite.Sync.Postgres` | PostgreSQL replica provider |

## How it works

Each replica tracks per-row version vectors and tombstones. When a replica reconnects, the sync engine:

1. Exchanges watermarks with its peer
2. Streams only the rows that changed on either side
3. Resolves conflicts using a configurable merge strategy (last-writer-wins by default)
4. Applies deletes via tombstones so "last seen" replicas don't resurrect old rows
5. Emits SSE notifications to connected subscribers

## Subscriptions

`Nimblesite.Sync.Http` exposes a Server-Sent Events endpoint that pushes change notifications to clients in real time. Combine with `Nimblesite.Sync.Postgres` or `Nimblesite.Sync.SQLite` to drive offline-first UIs that update as data arrives.

## Related documentation

- Full specification: [docs/specs/sync-spec.md](../docs/specs/sync-spec.md)
- Reference implementation: the [Clinical Coding Platform](https://github.com/MelbourneDeveloper/ClinicalCoding) uses the sync framework to flow Patient/Practitioner records between the Clinical and Scheduling domains.
