---
layout: layouts/docs.njk
title: Introduction
description: DataProvider - A complete .NET data access toolkit built on functional programming principles.
---

DataProvider is a complete toolkit for .NET database access that prioritizes **type safety** in the same way that common ORMs do. It provides source-generated SQL extensions, a cross-database query language, offline-first synchronization, and schema migrations.

## Philosophy

DataProvider fixes the issues that have plagued .NET data access for decades:

**The simplicity and safety of an ORM** but without the issues that come along with them. DataProvider generates extension methods directly from your SQL. You write the queries. You see what executes. SQL errors result in compilation errors. No magic. Intellisense/Autocomplete in SQL coming soon.

**Sync data** across microservices or create ocassionally connected apps that only sync when there is an internet connection.

**No Exceptions** Database operations fail. Networks drop. Constraints get violated. These aren't exceptional. They're expected. Every DataProvider operation returns `Result<T, Error>` instead of throwing. Pattern match on the result. Handle both cases explicitly. Your code becomes honest about what can go wrong.

**SQL is the source of truth.** Your database schema and queries define your application's data model. DataProvider works with this reality instead of fighting it. Define schemas in YAML. Write queries in SQL or LQL. Generate strongly-typed code from both.

## The Stack

| Component | Purpose |
|-----------|---------|
| [DataProvider](/docs/dataprovider/) | Source generator: SQL files become type-safe extension methods |
| [LQL](/docs/lql/) | Lambda Query Language: Write once, transpile to any SQL dialect |
| [Migrations](/docs/migrations/) | YAML schemas: Database-agnostic, version-controlled schema definitions |
| [Sync](/docs/sync/) | Offline-first: Bidirectional synchronization with conflict resolution |
| [Gatekeeper](/docs/gatekeeper/) | Auth: WebAuthn authentication and role-based access control |

Each component works independently or together. Use what you need.

## Quick Example

```csharp
// DataProvider generates extension methods from your SQL files
var result = await connection.GetOrdersByStatusAsync(status: "active");

// All operations return Result<T, Error> - no exceptions
if (result is Result<ImmutableList<Order>, SqlError>.Ok ok)
{
    foreach (var order in ok.Value)
        Console.WriteLine($"{order.Id}: {order.Total}");
}
else if (result is Result<ImmutableList<Order>, SqlError>.Error err)
{
    logger.LogError("Query failed: {Message}", err.Value.Message);
}
```

## Next Steps

- [Installation](/docs/installation/) - Add DataProvider to your project
- [Quick Start](/docs/quick-start/) - Start coding in minutes
- [DataProvider](/docs/dataprovider/) - Deep dive into SQL source generation
- [LQL](/docs/lql/) - Learn the Lambda Query Language
