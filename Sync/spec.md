# .NET Sync Framework Specification

**Version:** 0.1.0-draft
**Status:** Draft
**Last Updated:** 2025-12-18

---

## Table of Contents

1. [Introduction](#1-introduction)
2. [Goals & Non-Goals](#2-goals--non-goals)
3. [Architecture Overview](#3-architecture-overview)
4. [Unified Change Log](#4-unified-change-log)
5. [Versioning Model](#5-versioning-model)
6. [Triggers](#6-triggers)
7. [Batching](#7-batching)
8. [Change Enumeration](#8-change-enumeration)
9. [Conflict Resolution](#9-conflict-resolution)
10. [Hash Verification](#10-hash-verification)
11. [Security Considerations](#11-security-considerations)
12. [Conformance Requirements](#12-conformance-requirements)
13. [Appendices](#13-appendices)
14. [References](#14-references)

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
- Conflict detection semantics

This specification does **not** cover:

- Network transport protocols
- Authentication/authorization
- Specific conflict resolution algorithms (pluggable)
- Multi-master topology coordination

### 1.2 Target Platforms

- SQLite (primary - requires trigger-based tracking)
- SQL Server (can leverage native Change Tracking)
- PostgreSQL (can leverage logical replication)
- Any other platform we want to implement

---

## 2. Goals & Non-Goals

### 2.1 Goals

- **Minimal footprint**: Lightweight tracking with low storage overhead
- **Database-agnostic design**: Core concepts portable across engines
- **Offline-first**: Full functionality without network connectivity
- **Deterministic sync**: Reproducible change ordering via monotonic versions
- **Extensible conflict handling**: Pluggable resolution strategies
- **Hash based verification**: Every sync produces a new hash that can be used to verify the contents of the database
- **Efficient batching**: Handle millions of records for long-offline scenarios

### 2.2 Non-Goals

- Real-time streaming (batch-oriented by design)
- CRDT-based automatic merge (out of scope, but extensible)
- Binary blob synchronization
- File system sync

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
│         │   - All tables in one place     │              │
│         │   - JSON payloads               │              │
│         │   - Inserts, updates, deletes   │              │
│         └─────────────────────────────────┘              │
├─────────────────────────────────────────────────────────┤
│                    Database Layer                        │
│              (SQLite / SQL Server / PostgreSQL)          │
└─────────────────────────────────────────────────────────┘
```

---

## 4. Unified Change Log

### 4.1 Design Rationale

Instead of per-table tracking tables, all changes are stored in a **single unified table** with JSON payloads. This approach:

- Simplifies schema management (one table for everything)
- Makes schema evolution trivial (JSON is schema-agnostic)
- Enables efficient batched enumeration across all tables
- Stores tombstones naturally alongside other changes
- Produces wire-ready format (already JSON)

### 4.2 Definitions

| Term | Definition |
|------|------------|
| **Change Log** | Single table storing all tracked changes as JSON |
| **Version** | Monotonically increasing change identifier (global) |
| **Origin** | Identifier of the logical writer (device, node, replica) |
| **Operation** | Type of change: `insert`, `update`, `delete` |
| **Payload** | JSON representation of the row data |

### 4.3 Change Log Schema

A single table `_sync_log` stores ALL changes from ALL tracked tables.

```sql
CREATE TABLE _sync_log (
    version     INTEGER PRIMARY KEY,
    table_name  TEXT NOT NULL,
    pk_value    TEXT NOT NULL,           -- JSON: {"Id": 123} or {"A": 1, "B": 2}
    operation   TEXT NOT NULL,           -- 'insert' | 'update' | 'delete'
    payload     TEXT,                    -- JSON: full row data (NULL for deletes)
    origin      TEXT,                    -- writer identifier
    timestamp   TEXT NOT NULL            -- ISO 8601 UTC
);

CREATE INDEX idx_sync_log_table ON _sync_log(table_name, version);
```

### 4.4 Example Records

```json
{"version": 1, "table_name": "Person", "pk_value": "{\"Id\":42}", "operation": "insert",
 "payload": "{\"Id\":42,\"Name\":\"Alice\",\"Email\":\"alice@example.com\"}", "timestamp": "2025-12-18T10:30:00Z"}

{"version": 2, "table_name": "Person", "pk_value": "{\"Id\":42}", "operation": "update",
 "payload": "{\"Id\":42,\"Name\":\"Alice Smith\",\"Email\":\"alice@example.com\"}", "timestamp": "2025-12-18T11:00:00Z"}

{"version": 3, "table_name": "Order", "pk_value": "{\"OrderId\":100}", "operation": "insert",
 "payload": "{\"OrderId\":100,\"PersonId\":42,\"Total\":99.99}", "timestamp": "2025-12-18T11:05:00Z"}

{"version": 4, "table_name": "Person", "pk_value": "{\"Id\":42}", "operation": "delete",
 "payload": null, "timestamp": "2025-12-18T12:00:00Z"}
```

### 4.5 Tombstones

Deletions are stored as records with `operation = 'delete'` and `payload = NULL`. The `pk_value` is retained to identify which row was deleted.

- Tombstones MUST be retained for a configurable retention window
- After confirmed propagation to all replicas, tombstones MAY be purged
- Purging before full propagation causes "resurrection" on stale replicas

---

## 5. Versioning Model

### 5.1 Requirements

- Versions MUST be strictly monotonic within a single node
- Versions MUST be 64-bit integers minimum
- Version is the PRIMARY KEY of `_sync_log` - auto-incrementing
- Version generation is atomic with the INSERT into `_sync_log`

### 5.2 Implementation

SQLite's `INTEGER PRIMARY KEY` with `AUTOINCREMENT` provides:
- Guaranteed monotonic values
- Atomic assignment
- No contention issues

```sql
CREATE TABLE _sync_log (
    version INTEGER PRIMARY KEY AUTOINCREMENT,
    ...
);
```

---

## 6. Triggers

### 6.1 General Requirements

- Each tracked base table MUST define `AFTER INSERT`, `AFTER UPDATE`, and `AFTER DELETE` triggers
- Triggers serialize the row to JSON and INSERT into `_sync_log`
- Triggers MUST be minimal - just capture and log

### 6.2 Insert Trigger

```sql
CREATE TRIGGER Person_sync_insert
AFTER INSERT ON Person
BEGIN
    INSERT INTO _sync_log (table_name, pk_value, operation, payload, timestamp)
    VALUES (
        'Person',
        json_object('Id', NEW.Id),
        'insert',
        json_object('Id', NEW.Id, 'Name', NEW.Name, 'Email', NEW.Email),
        datetime('now')
    );
END;
```

### 6.3 Update Trigger

```sql
CREATE TRIGGER Person_sync_update
AFTER UPDATE ON Person
BEGIN
    INSERT INTO _sync_log (table_name, pk_value, operation, payload, timestamp)
    VALUES (
        'Person',
        json_object('Id', NEW.Id),
        'update',
        json_object('Id', NEW.Id, 'Name', NEW.Name, 'Email', NEW.Email),
        datetime('now')
    );
END;
```

### 6.4 Delete Trigger

```sql
CREATE TRIGGER Person_sync_delete
AFTER DELETE ON Person
BEGIN
    INSERT INTO _sync_log (table_name, pk_value, operation, payload, timestamp)
    VALUES (
        'Person',
        json_object('Id', OLD.Id),
        'delete',
        NULL,
        datetime('now')
    );
END;
```

---

## 7. Batching

### 7.1 The Problem

A device offline for days/weeks may need to sync millions of records. Loading all changes into memory is not feasible. Batching is **critical**.

### 7.2 Batch Request Model

Clients request changes in batches:

```
GET /sync?from_version=1000&batch_size=5000
```

Server responds with:

```json
{
    "changes": [...],           // Array of change records
    "from_version": 1000,       // Requested start
    "to_version": 5999,         // Last version in this batch
    "has_more": true,           // More batches available
    "batch_hash": "abc123..."   // Hash of this batch for verification
}
```

### 7.3 Batch Query

```sql
SELECT version, table_name, pk_value, operation, payload, origin, timestamp
FROM _sync_log
WHERE version > @from_version
ORDER BY version ASC
LIMIT @batch_size;
```

### 7.4 Batch Processing Requirements

- Batches MUST be processed in order (ascending version)
- Client MUST persist `to_version` after successfully applying each batch
- Client MUST NOT advance version until batch is fully applied
- On failure, client retries from last successful `to_version`
- Batch size SHOULD be configurable (default: 1000-5000 records)

### 7.5 Batch Application

For each batch:

1. Begin transaction
2. For each change in order:
   - `insert`: INSERT row (or upsert if exists)
   - `update`: UPDATE row
   - `delete`: DELETE row
3. Update local sync state with `to_version`
4. Commit transaction

If any step fails, rollback and retry the entire batch.

### 7.6 Memory Considerations

- Stream changes rather than loading entire batch into memory
- Process and apply changes row-by-row within the batch transaction
- For very large syncs, consider chunked batch transactions (e.g., commit every 1000 rows within a batch)

### 7.7 Progress Reporting

For long-running syncs, report progress:

```
Syncing: 45,000 / 2,300,000 records (2%)
Batch 9 of ~460 | ETA: 12 minutes
```

---

## 8. Change Enumeration

### 8.1 Query Pattern

Enumerate all changes since a version:

```sql
SELECT version, table_name, pk_value, operation, payload, origin, timestamp
FROM _sync_log
WHERE version > @last_sync_version
ORDER BY version ASC
LIMIT @batch_size;
```

### 8.2 Table-Specific Enumeration

To sync a specific table:

```sql
SELECT version, table_name, pk_value, operation, payload, origin, timestamp
FROM _sync_log
WHERE version > @last_sync_version
  AND table_name = @table_name
ORDER BY version ASC
LIMIT @batch_size;
```

### 8.3 Requirements

- `ORDER BY version ASC` is REQUIRED to preserve causal ordering
- Consumers MUST persist their last processed version
- Batching is REQUIRED for large change sets

---

## 9. Conflict Resolution

### 9.1 Conflict Detection

Two changes conflict when:

- Same `table_name` and `pk_value`
- Different versions from different origins
- Neither has been applied to the other's replica

### 9.2 Resolution Strategies (Pluggable)

| Strategy | Description |
|----------|-------------|
| **Last-Write-Wins (LWW)** | Highest timestamp wins |
| **Origin Priority** | Predefined origin hierarchy |
| **Application Logic** | Custom merge function per table |
| **User Resolution** | Surface to UI for manual merge |

### 9.3 Echo Prevention

- Origin field SHOULD be used to prevent re-applying own changes
- Sync consumers MUST skip changes where `origin = self`

---

## 10. Hash Verification

### 10.1 Purpose

Hash verification ensures database consistency after sync. If two replicas have the same data, they produce the same hash.

### 10.2 Hash Computation

After applying a batch (or full sync), compute a hash of the current database state:

1. For each tracked table (in alphabetical order):
2. Select all rows ordered by primary key
3. Serialize each row to canonical JSON
4. Append to hash input
5. Compute SHA-256 of concatenated data

### 10.3 Batch Hash

Each batch response includes a `batch_hash` computed from the changes in that batch. This allows:

- Verification that batch was transmitted correctly
- Detection of tampering or corruption

### 10.4 Full Database Hash

After sync completion, both client and server can compute and compare full database hashes to verify consistency.

---

## 11. Security Considerations

- Change log MUST NOT store secrets beyond what base tables contain
- Origin fields MUST NOT be trusted for authorization
- Sync endpoints MUST authenticate before accepting/sending changes
- Batch hashes provide integrity verification, not authentication

---

## 12. Conformance Requirements

An implementation is **conformant** if:

1. All tracked tables define INSERT, UPDATE, and DELETE triggers
2. All changes are logged to the unified `_sync_log` table
3. Deletes are stored as tombstones (not physical deletions from log)
4. Changes can be enumerated by ascending version with batching
5. Version values are strictly monotonic within a node
6. Batch processing is transactional and resumable

---

## 13. Appendices

### Appendix A: Suggested Indexes

```sql
-- Primary index is on version (PK)
-- Secondary index for table-specific queries
CREATE INDEX idx_sync_log_table ON _sync_log(table_name, version);

-- Optional: for tombstone cleanup
CREATE INDEX idx_sync_log_deletes ON _sync_log(operation, timestamp)
    WHERE operation = 'delete';
```

### Appendix B: Sync State Table

```sql
CREATE TABLE _sync_state (
    key TEXT PRIMARY KEY,
    value TEXT NOT NULL
);

-- Track sync progress
INSERT INTO _sync_state VALUES ('last_server_version', '0');
INSERT INTO _sync_state VALUES ('last_sync_timestamp', '');
INSERT INTO _sync_state VALUES ('origin_id', 'device-abc-123');
```

### Appendix C: Platform-Specific Notes

**SQLite:**
- `json_object()` available in SQLite 3.38+ (2022)
- No transaction log decoding - triggers are required
- `INTEGER PRIMARY KEY AUTOINCREMENT` for versions

**SQL Server:**
- Can use native Change Tracking instead of triggers
- `FOR JSON` for payload serialization
- Consider `SEQUENCE` for version generation

**PostgreSQL:**
- Can use logical replication / `pg_logical`
- `jsonb` type for payloads
- `LISTEN/NOTIFY` for real-time notifications

### Appendix D: Retention & Cleanup

```sql
-- Purge tombstones older than 30 days
DELETE FROM _sync_log
WHERE operation = 'delete'
  AND timestamp < datetime('now', '-30 days');

-- Compact: remove superseded changes for same row
-- (Keep only latest change per table+pk)
-- WARNING: Only safe after all replicas have synced past these versions
```

---

## 14. References

### 14.1 SQLite Documentation

- [CREATE TRIGGER](https://sqlite.org/lang_createtrigger.html) — Official SQLite trigger documentation. Explains that triggers fire `FOR EACH ROW` and can reference `NEW.*`/`OLD.*` column values.

- [JSON Functions](https://sqlite.org/json1.html) — SQLite JSON1 extension documentation. Covers `json_object()`, `json_extract()`, and related functions.

### 14.2 Sync Frameworks

- [Dotmim.Sync Documentation](https://dotmimsync.readthedocs.io/) — Modern .NET sync framework. The SQLite provider uses triggers and tracking tables.

- [Dotmim.Sync Change Tracking](https://dotmimsync.readthedocs.io/en/latest/ChangeTracking.html) — Explains SQL Server's built-in change tracking.

### 14.3 Enterprise Sync Solutions

- [SymmetricDS FAQ](https://www.symmetricds.org/docs/faq) — Enterprise replication engine. States that "triggers are installed in the database to guarantee that data changes are captured."

---

## Document History

| Version | Date | Changes |
|---------|------|---------|
| 0.1.0-draft | 2025-12-18 | Initial specification |
| 0.2.0-draft | 2025-12-18 | Unified change log model, batching, hash verification |
