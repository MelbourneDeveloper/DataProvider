# .NET Sync Framework Specification

**Version:** 0.1.0-draft
**Status:** Draft
**Last Updated:** 2025-12-18

---

## Table of Contents

1. [Introduction](#1-introduction)
2. [Goals & Non-Goals](#2-goals--non-goals)
3. [Architecture Overview](#3-architecture-overview)
4. [Change Tracking](#4-change-tracking)
5. [Versioning Model](#5-versioning-model)
6. [Triggers](#6-triggers)
7. [Tombstones & Deletions](#7-tombstones--deletions)
8. [Change Enumeration](#8-change-enumeration)
9. [Conflict Resolution](#9-conflict-resolution)
10. [Schema Evolution](#10-schema-evolution)
11. [Security Considerations](#11-security-considerations)
12. [Conformance Requirements](#12-conformance-requirements)
13. [Appendices](#13-appendices)
14. [References](#14-references)

---

## 1. Introduction

This specification defines a two-way synchronization framework for .NET applications targeting SQLite databases. The framework enables offline-first workloads, multi-device synchronization, and eventual consistency across distributed replicas.

### 1.1 Scope

This specification covers:

- Row-level change capture via triggers and tracking tables
- Version-based change enumeration
- Tombstone management for deletions
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
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────────┐  │
│  │   Change    │  │   Version   │  │    Conflict     │  │
│  │  Enumerator │  │   Manager   │  │    Resolver     │  │
│  └─────────────┘  └─────────────┘  └─────────────────┘  │
├─────────────────────────────────────────────────────────┤
│                  Tracking Layer                          │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────────┐  │
│  │  Triggers   │  │  Tracking   │  │   Tombstone     │  │
│  │  (per table)│  │   Tables    │  │    Manager      │  │
│  └─────────────┘  └─────────────┘  └─────────────────┘  │
├─────────────────────────────────────────────────────────┤
│                    Database Layer                        │
│              (SQLite / SQL Server / PostgreSQL)          │
└─────────────────────────────────────────────────────────┘
```

---

## 4. Change Tracking

### 4.1 Definitions

| Term | Definition |
|------|------------|
| **Base Table** | Application-owned table containing business data |
| **Tracking Table** | System-owned table storing change metadata |
| **Version** | Monotonically increasing change identifier |
| **Origin** | Identifier of the logical writer (device, node, replica) |
| **Tombstone** | Tracking record indicating a deleted row |

### 4.2 Tracking Table Schema

For each base table `{T}`, a corresponding tracking table `{T}_tracking` MUST exist.

**Required columns:**

| Column | Type | Description |
|--------|------|-------------|
| `[pk columns]` | Same as `{T}` | MUST match primary key of base table |
| `version` | INTEGER | Monotonically increasing change version |
| `is_deleted` | INTEGER (0/1) | Deletion state flag |
| `origin` | TEXT | Writer identifier (SHOULD be included) |
| `timestamp` | TEXT or INTEGER | UTC timestamp of change (SHOULD be included) |

**Example:**

```sql
CREATE TABLE Person_tracking (
    PersonId INTEGER PRIMARY KEY,
    version INTEGER NOT NULL,
    is_deleted INTEGER NOT NULL DEFAULT 0,
    origin TEXT,
    timestamp TEXT NOT NULL
);
```

---

## 5. Versioning Model

### 5.1 Requirements

- Versions MUST be strictly monotonic within a single node
- Versions MUST be 64-bit integers minimum
- Version generation MUST be atomic with the tracked operation

### 5.2 Acceptable Strategies

| Strategy | Pros | Cons |
|----------|------|------|
| Global counter table | Simple, deterministic | Single point of contention |
| Unix timestamp (`strftime('%s','now')`) | No extra tables | Second-level granularity only |
| Hybrid Logical Clock (HLC) | Distributed-friendly | More complex implementation |
| Lamport Clock | Causal ordering | Requires careful propagation |

### 5.3 Reference Implementation

```sql
CREATE TABLE _sync_version (v INTEGER NOT NULL);
INSERT INTO _sync_version VALUES (0);
```

Version increment (called within triggers):

```sql
UPDATE _sync_version SET v = v + 1;
SELECT v FROM _sync_version;
```

---

## 6. Triggers

### 6.1 General Requirements

- Each tracked base table MUST define `AFTER INSERT`, `AFTER UPDATE`, and `AFTER DELETE` triggers
- Triggers MUST be minimal to reduce write amplification
- Triggers MUST NOT contain business logic beyond tracking

### 6.2 Insert Trigger

```sql
CREATE TRIGGER {T}_insert_tracking
AFTER INSERT ON {T}
BEGIN
    INSERT OR REPLACE INTO {T}_tracking
        (pk_columns, version, is_deleted, timestamp)
    VALUES
        (NEW.pk_columns, next_version(), 0, datetime('now'));
END;
```

### 6.3 Update Trigger

```sql
CREATE TRIGGER {T}_update_tracking
AFTER UPDATE ON {T}
BEGIN
    UPDATE {T}_tracking
    SET version = next_version(),
        is_deleted = 0,
        timestamp = datetime('now')
    WHERE pk_column = NEW.pk_column;
END;
```

### 6.4 Delete Trigger

```sql
CREATE TRIGGER {T}_delete_tracking
AFTER DELETE ON {T}
BEGIN
    INSERT OR REPLACE INTO {T}_tracking
        (pk_columns, version, is_deleted, timestamp)
    VALUES
        (OLD.pk_columns, next_version(), 1, datetime('now'));
END;
```

---

## 7. Tombstones & Deletions

### 7.1 Tombstone Semantics

- Deletions MUST NOT remove tracking metadata
- Deletions MUST create a tombstone record (`is_deleted = 1`)
- Replicas receiving tombstones MUST delete or hide the corresponding row

### 7.2 Retention Policy

- Tombstones SHOULD be retained for a configurable retention window
- After confirmed propagation to all replicas, tombstones MAY be purged
- Purging before full propagation will cause "resurrection" on stale replicas

### 7.3 Recommended Retention

| Sync Frequency | Minimum Retention |
|----------------|-------------------|
| Real-time | 24 hours |
| Daily | 7 days |
| Weekly | 30 days |

---

## 8. Change Enumeration

### 8.1 Query Pattern

Consumers enumerate changes by comparing their last-seen version:

```sql
SELECT
    t.*,
    tr.is_deleted,
    tr.version,
    tr.timestamp,
    tr.origin
FROM {T}_tracking tr
LEFT JOIN {T} t ON t.pk_column = tr.pk_column
WHERE tr.version > @last_sync_version
ORDER BY tr.version ASC;
```

### 8.2 Requirements

- `ORDER BY version ASC` is REQUIRED to preserve causal ordering
- `LEFT JOIN` is REQUIRED to include tombstones (deleted rows)
- Consumers MUST persist their last processed version

---

## 9. Conflict Resolution

### 9.1 Conflict Detection

Two changes conflict when:

- Same primary key
- Different versions from different origins
- Neither has been applied to the other's replica

### 9.2 Resolution Strategies (Pluggable)

| Strategy | Description |
|----------|-------------|
| **Last-Write-Wins (LWW)** | Highest timestamp wins |
| **Origin Priority** | Predefined origin hierarchy |
| **Application Logic** | Custom merge function |
| **User Resolution** | Surface to UI for manual merge |

### 9.3 Echo Prevention

- Origin field SHOULD be used to prevent re-applying own changes
- Sync consumers MUST skip changes where `origin = self`

---

## 10. Schema Evolution

### 10.1 Requirements

- Triggers MUST be dropped and recreated on schema changes
- Tracking tables SHOULD add new metadata columns as needed
- Version history MUST NOT reset on schema changes

### 10.2 Migration Process

1. Drop existing triggers
2. Alter base table schema
3. Alter tracking table schema (if needed)
4. Recreate triggers with updated column references

---

## 11. Security Considerations

- Tracking tables MUST NOT store secrets or PII beyond what base tables contain
- Origin fields MUST NOT be trusted for authorization decisions
- Sync endpoints MUST authenticate before accepting changes
- Version values SHOULD NOT be predictable (avoid information leakage)

---

## 12. Conformance Requirements

An implementation is **conformant** if:

1. All tracked tables define INSERT, UPDATE, and DELETE triggers
2. All changes result in tracking table version increments
3. Deletes generate tombstones (not physical tracking row deletions)
4. Consumers can enumerate changes by ascending version
5. Version values are strictly monotonic within a node

---

## 13. Appendices

### Appendix A: Suggested Indexes

```sql
CREATE INDEX idx_{T}_tracking_version ON {T}_tracking(version);
CREATE INDEX idx_{T}_tracking_deleted ON {T}_tracking(is_deleted)
    WHERE is_deleted = 1;
```

### Appendix B: Multi-Table Registry

```sql
CREATE TABLE _sync_registry (
    table_name TEXT PRIMARY KEY,
    last_version INTEGER NOT NULL,
    enabled INTEGER NOT NULL DEFAULT 1
);
```

### Appendix C: Platform-Specific Notes

**SQLite:**
- No transaction log decoding available
- All tracking MUST use triggers
- `FOR EACH ROW` is the only trigger mode

**SQL Server:**
- Can use native Change Tracking (`CHANGETABLE`)
- Eliminates need for triggers and tracking tables
- Better performance at scale

**PostgreSQL:**
- Can use logical replication / `pg_logical`
- `LISTEN/NOTIFY` for real-time change streams

---

## 14. References

### 14.1 SQLite Documentation

- [CREATE TRIGGER](https://sqlite.org/lang_createtrigger.html) — Official SQLite trigger documentation. Explains that triggers fire `FOR EACH ROW` and can reference `NEW.*`/`OLD.*` column values.

### 14.2 Sync Frameworks

- [Dotmim.Sync Documentation](https://dotmimsync.readthedocs.io/) — Modern .NET sync framework. The SQLite provider uses triggers and tracking tables. Server-side can leverage SQL Server Change Tracking to eliminate triggers.

- [Dotmim.Sync Change Tracking](https://dotmimsync.readthedocs.io/en/latest/ChangeTracking.html) — Explains SQL Server's built-in change tracking: "no more tracking tables... no more triggers on your tables."

### 14.3 Enterprise Sync Solutions

- [SymmetricDS FAQ](https://www.symmetricds.org/docs/faq) — Enterprise replication engine. States that "triggers are installed in the database to guarantee that data changes are captured." Pro version supports log mining and logical streaming.

---

## Document History

| Version | Date | Changes |
|---------|------|---------|
| 0.1.0-draft | 2025-12-18 | Initial specification |
