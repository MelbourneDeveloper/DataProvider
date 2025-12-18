# .NET Sync Framework Specification

## Table of Contents

1. [Introduction](#1-introduction)
2. [Goals & Non-Goals](#2-goals--non-goals)
3. [Architecture Overview](#3-architecture-overview)
4. [Primary Key Requirements](#4-primary-key-requirements)
5. [Origin Identification](#5-origin-identification)
6. [Timestamps](#6-timestamps)
7. [Unified Change Log](#7-unified-change-log)
8. [Trigger Suppression](#8-trigger-suppression)
9. [LQL Trigger Generation](#9-lql-trigger-generation)
10. [Real-Time Subscriptions](#10-real-time-subscriptions)
11. [Bi-Directional Sync Protocol](#11-bi-directional-sync-protocol)
12. [Batching](#12-batching)
13. [Tombstone Retention](#13-tombstone-retention)
14. [Conflict Resolution](#14-conflict-resolution)
15. [Hash Verification](#15-hash-verification)
16. [Security Considerations](#16-security-considerations)
17. [Conformance Requirements](#17-conformance-requirements)
18. [Appendices](#18-appendices)
19. [References](#19-references)

---

## 1. Introduction

This specification defines a two-way synchronization framework for .NET applications. The framework enables offline-first workloads, multi-device synchronization, and eventual consistency across distributed replicas.

### 1.1 Scope

This specification covers:

- Row-level change capture via triggers
- Unified change log with JSON payloads
- Version-based change enumeration with batching
- Tombstone management for deletions
- Hash-based database verification
- Bi-directional sync ordering
- Trigger suppression during sync

This specification does **not** cover:

- Network transport protocols
- Authentication/authorization
- Specific conflict resolution algorithms (pluggable)

### 1.2 Target Platforms

- SQLite (primary - requires trigger-based tracking)
- SQL Server (can leverage native Change Tracking)
- PostgreSQL (can leverage logical replication)

---

## 2. Goals & Non-Goals

### 2.1 Goals

- **Minimal footprint**: Lightweight tracking with low storage overhead
- **Database-agnostic design**: Core concepts portable across engines
- **Offline-first**: Full functionality without network connectivity
- **Deterministic sync**: Reproducible change ordering via monotonic versions
- **Extensible conflict handling**: Pluggable resolution strategies
- **Hash-based verification**: Every sync produces a verifiable hash
- **Efficient batching**: Handle millions of records for long-offline scenarios

### 2.2 Non-Goals

- CRDT-based automatic merge (out of scope, but extensible)
- Binary blob or file system synchronization

---

## 3. Architecture Overview

```
┌─────────────────────────────────────────────────────────┐
│                    Application Layer                     │
├─────────────────────────────────────────────────────────┤
│                    Sync Engine                           │
│  ┌───────────┐ ┌───────────┐ ┌───────────┐ ┌─────────┐  │
│  │  Batch    │ │  Change   │ │  Conflict │ │  Hash   │  │
│  │  Manager  │ │Enumerator │ │  Resolver │ │ Verifier│  │
│  └───────────┘ └───────────┘ └───────────┘ └─────────┘  │
├─────────────────────────────────────────────────────────┤
│                  Tracking Layer                          │
│         ┌─────────────────────────────────┐              │
│         │   Unified Change Log (_sync)    │              │
│         │   - Trigger suppression flag    │              │
│         │   - JSON payloads               │              │
│         │   - Inserts, updates, deletes   │              │
│         └─────────────────────────────────┘              │
├─────────────────────────────────────────────────────────┤
│                    Database Layer                        │
│              (SQLite / SQL Server / PostgreSQL)          │
└─────────────────────────────────────────────────────────┘
```

---

## 4. Primary Key Requirements

### 4.1 Single-Column UUID Primary Key (REQUIRED)

Every tracked table MUST have a single-column primary key. **UUID/GUID is strongly recommended**.

**Rationale:**
- Composite keys complicate JSON serialization, conflict detection, and FK references
- Auto-increment integers cause collision issues in distributed systems
- UUIDs can be generated offline without coordination

### 4.2 Supported PK Types

| Type | Suitability | Notes |
|------|-------------|-------|
| UUID/GUID | **Recommended** | No collision risk, offline-safe |
| ULID | Excellent | Sortable, offline-safe |
| Integer (auto-inc) | Poor | Collisions across replicas |
| Composite | **Not supported** | Simplicity over flexibility |

### 4.3 Implementation

```sql
-- Recommended: UUID primary key
CREATE TABLE Person (
    Id TEXT PRIMARY KEY,  -- UUID as text
    Name TEXT NOT NULL,
    Email TEXT
);

-- The pk_value in _sync_log is always a simple JSON string
-- {"Id": "550e8400-e29b-41d4-a716-446655440000"}
```

Implementations MAY support integer PKs for simple cases but MUST document the collision risks.

---

## 5. Origin Identification

### 5.1 Purpose

Every replica needs a unique identifier to:
- Track which changes came from where
- Prevent echo (re-applying own changes)
- Detect conflicts between replicas

### 5.2 Origin ID Generation

Origin IDs MUST be generated using UUID v4 (random).

```csharp
// Generated ONCE on first sync initialization
var originId = Guid.NewGuid().ToString();
// Store in _sync_state, never regenerate
```

### 5.3 Origin ID Lifecycle

| Event | Action |
|-------|--------|
| First app install | Generate new UUID v4, store permanently |
| App reinstall | Generate new UUID v4 (treated as new device) |
| Database restore | Keep existing origin ID from backup |
| Device clone | MUST regenerate origin ID on clone |

### 5.4 Storage

```sql
CREATE TABLE _sync_state (
    key TEXT PRIMARY KEY,
    value TEXT NOT NULL
);

-- Set once, never change
INSERT INTO _sync_state VALUES ('origin_id', '550e8400-e29b-41d4-a716-446655440000');
```

### 5.5 Requirements

- Origin ID MUST be 36 characters (standard UUID format with hyphens)
- Origin ID MUST be stored before any sync operations
- Origin ID MUST NOT change after initial generation (except clone scenario)
- Origin ID MUST be included in every change log entry

---

## 6. Timestamps

### 6.1 Format

All timestamps MUST be stored as **ISO 8601 UTC strings** with millisecond precision:

```
2025-12-18T10:30:00.000Z
```

### 6.2 Requirements

- Timestamps MUST be UTC (indicated by `Z` suffix)
- Timestamps MUST include milliseconds (3 decimal places)
- Timestamps MUST NOT use local time zones
- Timestamps are for conflict resolution hints, NOT causal ordering (use version for ordering)

### 6.3 Implementation

**SQLite:**
```sql
-- strftime for consistent formatting
strftime('%Y-%m-%dT%H:%M:%fZ', 'now')
-- Result: 2025-12-18T10:30:00.123Z
```

**SQL Server:**
```sql
FORMAT(SYSUTCDATETIME(), 'yyyy-MM-ddTHH:mm:ss.fffZ')
```

**PostgreSQL:**
```sql
to_char(now() AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"')
```

---

## 7. Unified Change Log

### 7.1 Design Rationale

All changes are stored in a **single unified table** with JSON payloads:

- Simplifies schema management (one table for everything)
- Makes schema evolution trivial (JSON is schema-agnostic)
- Enables efficient batched enumeration across all tables
- Stores tombstones naturally alongside other changes

### 7.2 Schema

```sql
CREATE TABLE _sync_log (
    version     INTEGER PRIMARY KEY AUTOINCREMENT,
    table_name  TEXT NOT NULL,
    pk_value    TEXT NOT NULL,           -- JSON: {"Id": "uuid-here"}
    operation   TEXT NOT NULL CHECK (operation IN ('insert', 'update', 'delete')),
    payload     TEXT,                    -- JSON: full row data (NULL for deletes)
    origin      TEXT NOT NULL,           -- UUID of writer
    timestamp   TEXT NOT NULL            -- ISO 8601 UTC with milliseconds
);

-- Primary access pattern: get changes since version X
CREATE INDEX idx_sync_log_version ON _sync_log(version);

-- Secondary: table-specific queries
CREATE INDEX idx_sync_log_table ON _sync_log(table_name, version);
```

### 7.3 JSON Payload Serialization

Payloads are generated by triggers using the database's native JSON functions. The trigger generator MUST enumerate all columns at generation time.

**Column Enumeration:** The code generator reads table metadata (column names, types) and generates triggers with explicit column lists. When schema changes, triggers MUST be regenerated.

```sql
-- Generated trigger knows all columns at generation time
json_object('Id', NEW.Id, 'Name', NEW.Name, 'Email', NEW.Email, 'CreatedAt', NEW.CreatedAt)
```

This is acceptable because:
- Triggers are generated code, regenerated on schema change
- Explicit columns prevent accidentally syncing sensitive columns
- Failed triggers on missing columns surface schema drift immediately

---

## 8. Trigger Suppression

### 8.1 The Problem

When applying incoming changes, local triggers fire and log those changes again. This creates:
- Duplicate entries in the log
- Infinite sync loops
- Wasted storage and bandwidth

### 8.2 Solution: Sync Session Flag

A session-scoped flag disables trigger logging during sync application.

```sql
-- Add to _sync_state or use a temp table
CREATE TABLE _sync_session (
    sync_active INTEGER DEFAULT 0
);
INSERT INTO _sync_session VALUES (0);
```

### 8.3 Modified Triggers

All triggers MUST check the suppression flag:

```sql
CREATE TRIGGER Person_sync_insert
AFTER INSERT ON Person
WHEN (SELECT sync_active FROM _sync_session) = 0
BEGIN
    INSERT INTO _sync_log (table_name, pk_value, operation, payload, origin, timestamp)
    VALUES (
        'Person',
        json_object('Id', NEW.Id),
        'insert',
        json_object('Id', NEW.Id, 'Name', NEW.Name, 'Email', NEW.Email),
        (SELECT value FROM _sync_state WHERE key = 'origin_id'),
        strftime('%Y-%m-%dT%H:%M:%fZ', 'now')
    );
END;
```

### 8.4 Sync Application Pattern

```sql
-- 1. Enable suppression
UPDATE _sync_session SET sync_active = 1;

-- 2. Apply all incoming changes in transaction
BEGIN TRANSACTION;
-- ... apply changes ...
COMMIT;

-- 3. Disable suppression
UPDATE _sync_session SET sync_active = 0;
```

### 8.5 Requirements

- Trigger suppression MUST be enabled before applying ANY incoming changes
- Trigger suppression MUST be disabled after sync completes (success or failure)
- Local changes made while sync_active = 1 will NOT be tracked (this is intentional)

---

## 9. LQL Trigger Generation

### 9.1 Overview

Triggers are defined in LQL (Lambda Query Language) and transpiled to platform-specific SQL. This enables a single trigger definition to work across SQLite, SQL Server, and PostgreSQL.

### 9.2 LQL Trigger Syntax

```lql
-- Define sync triggers for a table
Person
|> syncTriggers({
    columns: ["Id", "Name", "Email"],
    pkColumn: "Id"
   })
```

The `syncTriggers` operator generates INSERT, UPDATE, and DELETE triggers that:
- Check the suppression flag before logging
- Serialize specified columns to JSON payload
- Include origin ID from `_sync_state`
- Format timestamps per platform

### 9.3 Column Enumeration

The LQL transpiler queries schema metadata to enumerate columns:

| Platform | Metadata Source |
|----------|-----------------|
| SQLite | `PRAGMA table_info(TableName)` |
| SQL Server | `INFORMATION_SCHEMA.COLUMNS` |
| PostgreSQL | `information_schema.columns` |

Columns can be explicitly listed (recommended for excluding sensitive data) or auto-discovered from schema.

### 9.4 Transpilation Output

**SQLite:**
```sql
CREATE TRIGGER Person_sync_insert
AFTER INSERT ON Person
WHEN (SELECT sync_active FROM _sync_session) = 0
BEGIN
    INSERT INTO _sync_log (table_name, pk_value, operation, payload, origin, timestamp)
    VALUES ('Person', json_object('Id', NEW.Id), 'insert',
            json_object('Id', NEW.Id, 'Name', NEW.Name, 'Email', NEW.Email),
            (SELECT value FROM _sync_state WHERE key = 'origin_id'),
            strftime('%Y-%m-%dT%H:%M:%fZ', 'now'));
END;
```

**SQL Server:**
```sql
CREATE TRIGGER Person_sync_insert ON Person AFTER INSERT AS
BEGIN
    IF (SELECT sync_active FROM _sync_session) = 0
    INSERT INTO _sync_log (table_name, pk_value, operation, payload, origin, timestamp)
    SELECT 'Person', (SELECT i.Id FOR JSON PATH, WITHOUT_ARRAY_WRAPPER), 'insert',
           (SELECT i.Id, i.Name, i.Email FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
           (SELECT value FROM _sync_state WHERE [key] = 'origin_id'),
           FORMAT(SYSUTCDATETIME(), 'yyyy-MM-ddTHH:mm:ss.fffZ')
    FROM inserted i;
END;
```

**PostgreSQL:**
```sql
CREATE OR REPLACE FUNCTION person_sync_insert() RETURNS TRIGGER AS $$
BEGIN
    IF (SELECT sync_active FROM _sync_session) = 0 THEN
        INSERT INTO _sync_log (table_name, pk_value, operation, payload, origin, timestamp)
        VALUES ('Person', jsonb_build_object('Id', NEW.Id), 'insert',
                jsonb_build_object('Id', NEW.Id, 'Name', NEW.Name, 'Email', NEW.Email),
                (SELECT value FROM _sync_state WHERE key = 'origin_id'),
                to_char(now() AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"'));
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER Person_sync_insert AFTER INSERT ON Person
FOR EACH ROW EXECUTE FUNCTION person_sync_insert();
```

### 9.5 Requirements

- Triggers MUST be generated via LQL transpilation, not hand-written SQL
- Each tracked table MUST have INSERT, UPDATE, and DELETE triggers
- Triggers MUST be regenerated when table schema changes
- LQL trigger definitions SHOULD be stored alongside table definitions

---

## 10. Real-Time Subscriptions

### 10.1 Overview

Clients can subscribe to real-time change notifications from the server. This complements batch sync with immediate push notifications for active clients.

### 10.2 Subscription Types

| Type | Description | Use Case |
|------|-------------|----------|
| **Record** | Subscribe to specific PK(s) | Editing a document, watching an order |
| **Table** | Subscribe to all changes in a table | Dashboard, admin panel |
| **Query** | Subscribe to records matching criteria | "My orders", "Assigned tasks" |

### 10.3 Subscription Protocol

**Subscribe:**
```json
{
    "action": "subscribe",
    "subscription_id": "sub-uuid-123",
    "type": "record",
    "table": "Orders",
    "pk_values": ["order-uuid-456", "order-uuid-789"]
}
```

**Table subscription:**
```json
{
    "action": "subscribe",
    "subscription_id": "sub-uuid-124",
    "type": "table",
    "table": "Products"
}
```

**Unsubscribe:**
```json
{
    "action": "unsubscribe",
    "subscription_id": "sub-uuid-123"
}
```

### 10.4 Server Notifications

When a subscribed record changes, the server pushes:

```json
{
    "type": "change",
    "subscription_id": "sub-uuid-123",
    "change": {
        "version": 12345,
        "table_name": "Orders",
        "pk_value": {"Id": "order-uuid-456"},
        "operation": "update",
        "payload": {"Id": "order-uuid-456", "Status": "shipped"},
        "origin": "origin-uuid-999",
        "timestamp": "2025-12-18T10:30:00.123Z"
    }
}
```

### 10.5 Delivery Guarantees

- **At-least-once**: Notifications may be delivered multiple times; clients must handle idempotently
- **Client acknowledgment**: Clients SHOULD acknowledge received notifications
- **Missed notifications**: If connection drops, client resumes via batch sync from last known version

### 10.6 Server-Side Tracking

```sql
CREATE TABLE _sync_subscriptions (
    subscription_id TEXT PRIMARY KEY,
    origin_id TEXT NOT NULL,
    subscription_type TEXT NOT NULL CHECK (subscription_type IN ('record', 'table', 'query')),
    table_name TEXT NOT NULL,
    filter TEXT,                    -- JSON: pk_values for record, query criteria for query
    created_at TEXT NOT NULL,
    expires_at TEXT                 -- Optional TTL
);

CREATE INDEX idx_subscriptions_table ON _sync_subscriptions(table_name);
CREATE INDEX idx_subscriptions_origin ON _sync_subscriptions(origin_id);
```

### 10.7 Requirements

- Server MUST support record-level and table-level subscriptions
- Subscriptions MUST be scoped to authenticated origin
- Server MUST clean up subscriptions when client disconnects or subscription expires
- Notifications MUST include full change payload (same format as `_sync_log` entries)
- Clients MUST NOT rely solely on subscriptions; batch sync remains the source of truth

---

## 11. Bi-Directional Sync Protocol

### 11.1 The Problem

Tables have foreign key relationships. Syncing in wrong order causes:
- FK constraint violations
- Failed inserts
- Orphaned records

### 11.2 Sync Direction

Bi-directional sync has two phases:

1. **Pull**: Get changes from server, apply locally
2. **Push**: Send local changes to server

### 11.3 Table Ordering (CRITICAL)

Tables MUST be synced in **dependency order** based on foreign key relationships.

**Pull Order (Parent → Child):**
```
1. Users        (no FK dependencies)
2. Categories   (no FK dependencies)
3. Products     (FK → Categories)
4. Orders       (FK → Users)
5. OrderItems   (FK → Orders, Products)
```

**Push Order (Child → Parent for deletes, Parent → Child for inserts/updates):**
- Inserts/Updates: Same as pull (parent first)
- Deletes: Reverse order (child first)

### 11.4 Dependency Resolution Algorithm

```
1. Build directed graph: table → tables it references
2. Topological sort to get order
3. For circular dependencies:
   a. Disable FK constraints
   b. Apply all changes
   c. Re-enable FK constraints
   d. Validate (fail sync if violations)
```

### 11.5 Handling FK Violations

When a change references a row that doesn't exist yet:

1. **Defer**: Queue the change, continue with others, retry deferred changes
2. **Retry Loop**: After each batch, retry deferred changes up to N times
3. **Fail**: If deferred changes cannot be applied after all batches, fail the sync

```
Batch 1: Applied 950/1000, deferred 50 (missing FK targets)
Batch 2: Applied 1000/1000
Retry deferred: Applied 48/50
Retry deferred: Applied 2/2
Sync complete.
```

### 11.6 Sync Session Protocol

```
PULL PHASE:
1. Enable trigger suppression
2. Begin transaction
3. For each table in dependency order:
   a. Fetch changes from server (batched)
   b. Apply changes, deferring FK violations
4. Retry deferred changes (max 3 passes)
5. Update last_server_version
6. Commit transaction
7. Disable trigger suppression

PUSH PHASE:
1. Begin transaction
2. Collect local changes since last push
3. Group by table in dependency order
4. Send to server (server applies with same ordering logic)
5. Update last_push_version on success
6. Commit transaction
```

### 11.7 Requirements

- Sync engine MUST calculate table dependency order before sync
- FK violations during apply MUST be deferred, not failed immediately
- Deferred changes MUST be retried after each batch
- Circular FK dependencies MUST be handled by disabling constraints temporarily
- Sync MUST fail if deferred changes cannot be resolved after all retries

---

## 12. Batching

### 12.1 The Problem

A device offline for weeks may need to sync millions of records. Loading all changes into memory is not feasible.

### 12.2 Batch Query

```sql
SELECT version, table_name, pk_value, operation, payload, origin, timestamp
FROM _sync_log
WHERE version > @from_version
ORDER BY version ASC
LIMIT @batch_size;
```

### 12.3 Batch Processing Requirements

- Batches MUST be processed in order (ascending version)
- Client MUST persist `to_version` after successfully applying each batch
- On failure, client retries from last successful `to_version`
- Batch size SHOULD be configurable (default: 1000-5000 records)
- Trigger suppression MUST be active for entire pull phase, not per-batch

### 12.4 Batch Application

For each batch:

1. Trigger suppression already active (set at start of pull phase)
2. For each change in order:
   - `insert`: INSERT or UPSERT
   - `update`: UPDATE (or UPSERT)
   - `delete`: DELETE
   - On FK violation: defer to retry queue
3. After batch: retry deferred changes
4. Update local sync state with `to_version`

---

## 13. Tombstone Retention

### 13.1 The Problem

Deleted records must be tracked so replicas know to delete them too. But keeping tombstones forever wastes storage.

### 13.2 Retention Strategy

Tombstones are retained based on **time since last successful sync from any replica**, not just calendar time.

### 13.3 Server Tracking

The server tracks the last sync version for each known origin:

```sql
CREATE TABLE _sync_clients (
    origin_id TEXT PRIMARY KEY,
    last_sync_version INTEGER NOT NULL,
    last_sync_timestamp TEXT NOT NULL
);
```

### 13.4 Safe Purge Calculation

Tombstones can be purged when:

```sql
-- Find the minimum version that ALL known clients have synced past
SELECT MIN(last_sync_version) AS safe_purge_version FROM _sync_clients;

-- Purge tombstones older than that version
DELETE FROM _sync_log
WHERE operation = 'delete'
  AND version < @safe_purge_version;
```

### 13.5 Stale Client Handling

Clients that haven't synced for extended periods:

| Scenario | Action |
|----------|--------|
| Client syncs within retention window | Normal sync |
| Client missed purged tombstones | Full resync required |
| Client inactive > 90 days | Remove from `_sync_clients`, require re-registration |

### 13.6 Full Resync Protocol

When a client has fallen too far behind:

1. Server detects: client's `last_sync_version` < oldest available version
2. Server responds with `requires_full_resync: true`
3. Client wipes local data (or specific tables)
4. Client downloads full current state
5. Client resumes incremental sync

### 13.7 Requirements

- Server MUST track last sync version per origin
- Tombstones MUST NOT be purged until all active clients have synced past them
- Clients that miss tombstones MUST perform full resync
- Retention policy SHOULD be configurable (default: purge when all clients synced, max 90 days)

---

## 14. Conflict Resolution

### 14.1 Conflict Detection

Two changes conflict when:

- Same `table_name` and `pk_value`
- Different `origin` values
- Both changes occurred since last sync

### 14.2 Resolution Strategies (Pluggable)

| Strategy | Description |
|----------|-------------|
| **Last-Write-Wins (LWW)** | Highest timestamp wins (default) |
| **Server Wins** | Server version always wins |
| **Client Wins** | Client version always wins |
| **Application Logic** | Custom merge function per table |

### 14.3 Echo Prevention

Changes MUST NOT be re-applied to their origin:

```sql
-- Skip changes from self
WHERE origin != @my_origin_id
```

---

## 15. Hash Verification

### 15.1 Purpose

Hash verification ensures database consistency. If two replicas have the same data, they produce the same hash.

### 15.2 Canonical JSON Specification

To ensure identical hashes across implementations, JSON MUST be serialized canonically:

1. **Keys**: Sorted alphabetically (Unicode code point order)
2. **Whitespace**: None (no spaces, no newlines)
3. **Numbers**: No leading zeros, no trailing zeros after decimal, no positive sign
4. **Strings**: Minimal escaping (only `"`, `\`, and control characters)
5. **Unicode**: UTF-8 encoded, no escaping of non-ASCII characters
6. **Null**: Literal `null`, not omitted

**Example:**
```json
{"Email":"alice@example.com","Id":"550e8400-e29b-41d4-a716-446655440000","Name":"Alice"}
```

### 15.3 Full Database Hash Algorithm

```
1. Initialize SHA-256 hasher
2. For each tracked table (sorted alphabetically by name):
   a. Append table name + newline to hasher
   b. Select all rows ordered by primary key ASC
   c. For each row:
      - Serialize to canonical JSON
      - Append JSON + newline to hasher
3. Return hex-encoded SHA-256 digest (lowercase)
```

### 15.4 Batch Hash

Each batch includes a hash of its contents:

```
1. Initialize SHA-256 hasher
2. For each change in batch (in version order):
   a. Append: "{version}:{table_name}:{pk_value}:{operation}:{payload}\n"
3. Return hex-encoded SHA-256 digest
```

### 15.5 Verification

After sync, client and server can compare full database hashes. Mismatch indicates:
- Sync bug
- Data corruption
- Unauthorized modification

---

## 16. Security Considerations

- Change log MUST NOT store secrets beyond what base tables contain
- Origin IDs are identifiers, NOT authentication tokens
- Sync endpoints MUST authenticate before accepting/sending changes
- Hash verification provides integrity checking, not authentication
- Consider encrypting payloads for sensitive data

---

## 17. Conformance Requirements

An implementation is **conformant** if:

1. All tracked tables have single-column primary keys
2. Origin ID is generated as UUID v4 and stored permanently
3. All timestamps are UTC ISO 8601 with milliseconds
4. Triggers check suppression flag before logging
5. Triggers include origin ID in every entry
6. Bi-directional sync respects FK dependency ordering
7. Tombstones are retained until all clients have synced past them
8. Hash computation follows canonical JSON specification
9. Batching supports resumable sync from last successful version
10. Triggers are generated via LQL transpilation
11. Server supports real-time subscriptions (record and table level)

---

## 18. Appendices

### Appendix A: Complete Schema

```sql
-- Sync state (persistent)
CREATE TABLE _sync_state (
    key TEXT PRIMARY KEY,
    value TEXT NOT NULL
);

-- Sync session (ephemeral flag)
CREATE TABLE _sync_session (
    sync_active INTEGER DEFAULT 0
);
INSERT INTO _sync_session VALUES (0);

-- Change log
CREATE TABLE _sync_log (
    version     INTEGER PRIMARY KEY AUTOINCREMENT,
    table_name  TEXT NOT NULL,
    pk_value    TEXT NOT NULL,
    operation   TEXT NOT NULL CHECK (operation IN ('insert', 'update', 'delete')),
    payload     TEXT,
    origin      TEXT NOT NULL,
    timestamp   TEXT NOT NULL
);

CREATE INDEX idx_sync_log_version ON _sync_log(version);
CREATE INDEX idx_sync_log_table ON _sync_log(table_name, version);

-- Initialize
INSERT INTO _sync_state VALUES ('origin_id', '');  -- Set on first sync init
INSERT INTO _sync_state VALUES ('last_server_version', '0');
INSERT INTO _sync_state VALUES ('last_push_version', '0');
```

### Appendix B: Server Client Tracking (for tombstone retention)

```sql
CREATE TABLE _sync_clients (
    origin_id TEXT PRIMARY KEY,
    last_sync_version INTEGER NOT NULL DEFAULT 0,
    last_sync_timestamp TEXT NOT NULL,
    created_at TEXT NOT NULL
);
```

### Appendix C: Platform-Specific Notes

**SQLite:**
- `json_object()` available in SQLite 3.38+ (2022)
- `strftime('%Y-%m-%dT%H:%M:%fZ', 'now')` for timestamps
- `WHEN` clause on triggers for suppression

**SQL Server:**
- Can use native Change Tracking instead of triggers
- `FOR JSON` for payload serialization
- Session context or temp table for suppression flag

**PostgreSQL:**
- `jsonb_build_object()` for payloads
- `current_setting()` for session variables (suppression)
- Consider `pg_logical` for change capture

---

## 19. References

- [SQLite CREATE TRIGGER](https://sqlite.org/lang_createtrigger.html)
- [SQLite JSON Functions](https://sqlite.org/json1.html)
- [RFC 4122 - UUID](https://tools.ietf.org/html/rfc4122)
- [RFC 8785 - JSON Canonicalization](https://tools.ietf.org/html/rfc8785)
- [Dotmim.Sync Documentation](https://dotmimsync.readthedocs.io/)