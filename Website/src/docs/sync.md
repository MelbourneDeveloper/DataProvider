---
layout: layouts/docs.njk
title: Sync
description: Offline-first bidirectional synchronization.
---

## Overview

Sync provides offline-first bidirectional synchronization for mobile and web applications. Keep your local data in sync with the server even when offline.

## Key Features

- **Offline-First**: Work offline and sync when connected
- **Bidirectional**: Changes flow both ways
- **Conflict Resolution**: Built-in strategies for handling conflicts
- **Efficient**: Only syncs changed records

## Basic Usage

```csharp
var syncEngine = new SyncEngine(localDb, remoteApi);

// Sync all changes
await syncEngine.SyncAsync();

// Sync specific tables
await syncEngine.SyncAsync("Orders", "Customers");
```

## Conflict Resolution

Handle conflicts with built-in strategies:

```csharp
syncEngine.ConflictStrategy = ConflictStrategy.ServerWins;
// Or
syncEngine.ConflictStrategy = ConflictStrategy.ClientWins;
// Or custom
syncEngine.OnConflict = (local, remote) => /* your logic */;
```

## Next Steps

- [DataProvider Documentation](/docs/dataprovider/)
- [Gatekeeper Authentication](/docs/gatekeeper/)
