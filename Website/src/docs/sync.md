---
layout: layouts/docs.njk
title: "Sync Framework"
---

# Sync Framework

A database-agnostic, offline-first synchronization framework for .NET applications. Enables two-way data synchronization between distributed replicas with conflict resolution, tombstone management, and real-time subscriptions.

## Overview

The Sync framework provides:

- **Offline-first architecture** - Work locally, sync when connected
- **Two-way synchronization** - Pull changes from server, push local changes
- **Conflict resolution** - Last-write-wins, server-wins, client-wins, or custom strategies
- **Foreign key handling** - Automatic deferred retry for FK violations during sync
- **Tombstone management** - Safe deletion tracking for late-syncing clients
- **Real-time subscriptions** - Subscribe to changes on specific records or tables
- **Hash verification** - SHA-256 integrity checking for batches and databases
- **Database agnostic** - Currently supports SQLite and PostgreSQL

## Projects

| Project | Description |
|---------|-------------|
| `Sync` | Core synchronization engine (platform-agnostic) |
| `Sync.SQLite` | SQLite-specific implementation |
| `Sync.Postgres` | PostgreSQL-specific implementation |
| `Sync.Api` | REST API server with SSE real-time subscriptions |
| `Sync.Tests` | Core engine tests |
| `Sync.SQLite.Tests` | SQLite integration tests |
| `Sync.Postgres.Tests` | PostgreSQL integration tests |
| `Sync.Api.Tests` | API endpoint tests |
| `Sync.Integration.Tests` | Cross-database E2E tests |

## Getting Started

### Prerequisites

- .NET 9.0 SDK
- For PostgreSQL: Docker (or a local PostgreSQL instance)

### Installation

Add the appropriate NuGet packages to your project:

```xml
<!-- Core sync engine -->
<PackageReference Include="Sync" Version="1.0.0" />

<!-- Choose your database provider -->
<PackageReference Include="Sync.SQLite" Version="1.0.0" />
<!-- or -->
<PackageReference Include="Sync.Postgres" Version="1.0.0" />
```

### Basic Setup (SQLite)

#### 1. Initialize the Sync Schema

```csharp
using Microsoft.Data.Sqlite;
using Sync.SQLite;

// Create your database connection
using var connection = new SqliteConnection("Data Source=myapp.db");
connection.Open();

// Create sync tables (_sync_log, _sync_state, _sync_session, etc.)
SyncSchema.CreateSchema(connection);
SyncSchema.InitializeSyncState(connection, originId: Guid.NewGuid().ToString());
```

#### 2. Add Triggers to Your Tables

```csharp
// Generate and apply sync triggers for a table
var triggerResult = TriggerGenerator.GenerateTriggers(connection, "Person");
if (triggerResult is TriggerListOk ok)
{
    foreach (var trigger in ok.Value)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = trigger;
        cmd.ExecuteNonQuery();
    }
}
```

This creates INSERT, UPDATE, and DELETE triggers that automatically log changes to `_sync_log`.

#### 3. Perform Synchronization

```csharp
using Sync;

// Create delegate functions for database operations
Func<long, int, SyncBatchResult> fetchRemoteChanges = (fromVersion, batchSize) =>
    SyncLogRepository.FetchChanges(remoteConnection, fromVersion, batchSize);

Func<SyncLogEntry, BoolSyncResult> applyChange = (entry) =>
    ChangeApplierSQLite.ApplyChange(localConnection, entry);

Func<BoolSyncResult> enableSuppression = () =>
    SyncSessionManager.EnableSuppression(localConnection);

Func<BoolSyncResult> disableSuppression = () =>
    SyncSessionManager.DisableSuppression(localConnection);

// Pull changes from remote
var pullResult = SyncCoordinator.Pull(
    fetchRemoteChanges,
    applyChange,
    enableSuppression,
    disableSuppression,
    getLastServerVersion: () => SyncLogRepository.GetLastServerVersion(localConnection),
    updateLastServerVersion: (v) => SyncLogRepository.UpdateLastServerVersion(localConnection, v),
    localOriginId: myOriginId,
    config: new BatchConfig(BatchSize: 1000, MaxRetryPasses: 3),
    logger: NullLogger.Instance
);

// Push local changes to remote
var pushResult = SyncCoordinator.Push(
    fetchLocalChanges: (fromVersion, batchSize) =>
        SyncLogRepository.FetchChanges(localConnection, fromVersion, batchSize),
    sendToRemote: (batch) => ApplyBatchToRemote(remoteConnection, batch),
    getLastPushVersion: () => SyncLogRepository.GetLastPushVersion(localConnection),
    updateLastPushVersion: (v) => SyncLogRepository.UpdateLastPushVersion(localConnection, v),
    config: new BatchConfig(),
    logger: NullLogger.Instance
);
```

### Using the REST API

#### Start the API Server

```bash
cd Sync/Sync.Api
dotnet run
```

#### API Endpoints

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/health` | GET | Health check |
| `/sync/changes` | GET | Pull changes from server |
| `/sync/changes` | POST | Push changes to server |
| `/sync/clients` | POST | Register a sync client |
| `/sync/state` | GET | Get server sync state |
| `/sync/subscribe` | GET | Subscribe to real-time changes (SSE) |
| `/sync/subscribe/{id}` | DELETE | Unsubscribe |

#### Pull Changes

```bash
curl "http://localhost:5000/sync/changes?fromVersion=0&batchSize=100&connectionString=Data%20Source=server.db&dbType=sqlite"
```

Response:
```json
{
  "changes": [
    {
      "version": 1,
      "tableName": "Person",
      "pkValue": "{\"Id\":1}",
      "operation": "Insert",
      "payload": "{\"Id\":1,\"Name\":\"Alice\",\"Email\":\"alice@example.com\"}",
      "origin": "client-abc",
      "timestamp": "2025-01-15T10:30:00.000Z"
    }
  ],
  "fromVersion": 0,
  "toVersion": 1,
  "hasMore": false
}
```

#### Push Changes

```bash
curl -X POST "http://localhost:5000/sync/changes?connectionString=Data%20Source=server.db&dbType=sqlite" \
  -H "Content-Type: application/json" \
  -d '{
    "OriginId": "client-xyz",
    "Changes": [
      {
        "version": 0,
        "tableName": "Person",
        "pkValue": "{\"Id\":2}",
        "operation": "Insert",
        "payload": "{\"Id\":2,\"Name\":\"Bob\"}",
        "origin": "client-xyz",
        "timestamp": "2025-01-15T11:00:00.000Z"
      }
    ]
  }'
```

#### Real-Time Subscriptions (SSE)

```bash
# Subscribe to all changes on the Person table
curl "http://localhost:5000/sync/subscribe?tableName=Person"

# Subscribe to a specific record
curl "http://localhost:5000/sync/subscribe?tableName=Person&pkValue=1"
```

### PostgreSQL Setup

#### 1. Start PostgreSQL with Docker

From the repository root:

```bash
docker-compose up -d
```

This starts a single PostgreSQL container on `localhost:5432` (user: postgres, password: postgres, database: gigs). The C# migrations handle schema creation.

#### 2. Initialize Schema

```csharp
using Npgsql;
using Sync.Postgres;

using var connection = new NpgsqlConnection(
    "Host=localhost;Port=5432;Database=gigs;Username=postgres;Password=postgres");
connection.Open();

PostgresSyncSchema.CreateSchema(connection);
PostgresSyncSchema.InitializeSyncState(connection, originId: Guid.NewGuid().ToString());
```

## Architecture

### Sync Tables

The framework creates these tables in your database:

| Table | Purpose |
|-------|---------|
| `_sync_log` | Change log with version, table, PK, operation, payload, origin, timestamp |
| `_sync_state` | Local replica state (origin_id, last_server_version, last_push_version) |
| `_sync_session` | Trigger suppression flag (sync_active) |
| `_sync_clients` | Server-side client tracking for tombstone management |
| `_sync_subscriptions` | Real-time subscription registrations |

### Change Capture

When you modify a tracked table:
1. AFTER trigger fires (if `sync_active = 0`)
2. Trigger inserts row into `_sync_log` with:
   - Auto-incrementing version
   - Table name and primary key (JSON)
   - Operation (Insert/Update/Delete)
   - Full row payload (JSON) for Insert/Update, NULL for Delete
   - Origin ID (prevents echo during sync)
   - UTC timestamp

### Sync Flow

**Pull (receive changes):**
1. Enable trigger suppression (`sync_active = 1`)
2. Fetch batch from remote (version > lastServerVersion)
3. Apply changes with FK violation defer/retry
4. Skip changes from own origin (echo prevention)
5. Update lastServerVersion
6. Repeat until no more changes
7. Disable trigger suppression

**Push (send changes):**
1. Fetch local changes (version > lastPushVersion)
2. Send batch to remote
3. Update lastPushVersion
4. Repeat until no more changes

### Conflict Resolution

When the same row is modified by different origins:

```csharp
// Default: Last-write-wins (by timestamp, version as tiebreaker)
var resolved = ConflictResolver.Resolve(
    localEntry,
    remoteEntry,
    ConflictStrategy.LastWriteWins
);

// Or use custom resolution
var resolved = ConflictResolver.ResolveCustom(
    localEntry,
    remoteEntry,
    (local, remote) => /* your merge logic */
);
```

### Hash Verification

Verify data integrity with SHA-256 hashes:

```csharp
// Hash a batch of changes
var batchHash = HashVerifier.ComputeBatchHash(changes);

// Hash entire database state
var dbHash = HashVerifier.ComputeDatabaseHash(
    fetchAllChanges: () => SyncLogRepository.FetchAll(connection)
);

// Verify batch integrity
var isValid = HashVerifier.VerifyHash(expectedHash, actualHash);
```

## Running Tests

```bash
# All tests
dotnet test

# Specific test projects
dotnet test --filter "FullyQualifiedName~Sync.Tests"
dotnet test --filter "FullyQualifiedName~Sync.SQLite.Tests"
dotnet test --filter "FullyQualifiedName~Sync.Postgres.Tests"
dotnet test --filter "FullyQualifiedName~Sync.Api.Tests"

# Cross-database integration tests (requires Docker)
dotnet test --filter "FullyQualifiedName~Sync.Integration.Tests"
```

## Configuration

### BatchConfig

```csharp
var config = new BatchConfig(
    BatchSize: 1000,      // Changes per batch (default: 1000)
    MaxRetryPasses: 3     // FK violation retry attempts (default: 3)
);
```

### Tombstone Management

```csharp
// Calculate safe version to purge (all clients have synced past this)
var safeVersion = TombstoneManager.CalculateSafePurgeVersion(
    getAllClients: () => SyncClientRepository.GetAll(connection)
);

// Purge old tombstones
TombstoneManager.PurgeTombstones(
    purge: (version) => SyncLogRepository.PurgeBefore(connection, version),
    safeVersion
);

// Detect stale clients (90 days inactive by default)
var staleClients = TombstoneManager.FindStaleClients(
    getAllClients: () => SyncClientRepository.GetAll(connection),
    inactivityThreshold: TimeSpan.FromDays(90)
);
```

## Error Handling

All operations return `Result<TValue, SyncError>`:

```csharp
var result = SyncCoordinator.Pull(...);

if (result is PullResultOk ok)
{
    Console.WriteLine($"Pulled {ok.Value.ChangesApplied} changes");
}
else if (result is PullResultError error)
{
    switch (error.Value)
    {
        case SyncErrorForeignKeyViolation fk:
            Console.WriteLine($"FK violation: {fk.Message}");
            break;
        case SyncErrorFullResyncRequired:
            Console.WriteLine("Client fell too far behind, full resync needed");
            break;
        case SyncErrorHashMismatch hash:
            Console.WriteLine($"Data integrity error: {hash.Expected} != {hash.Actual}");
            break;
        // ... handle other error types
    }
}
```

## Design Principles

This framework follows the coding rules from `CLAUDE.md`:

- **No exceptions** - All fallible operations return `Result<T, SyncError>`
- **No classes** - Uses records and static methods (FP style)
- **No interfaces** - Uses `Func<T>` and `Action<T>` for abstractions
- **Integration testing** - No mocks, tests use real databases
- **Copious logging** - All operations log via `ILogger`

## License

See repository root for license information.
