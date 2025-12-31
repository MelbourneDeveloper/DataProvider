---
layout: layouts/docs.njk
title: Getting Started
description: Get started with DataProvider for .NET data access.
---

## Installation

Add DataProvider to your .NET project:

```bash
dotnet add package DataProvider
```

## Quick Start

DataProvider generates type-safe extension methods on `IDbConnection` from your SQL queries.

```csharp
using DataProvider;

// Execute a query
var orders = connection.Query<Order>("SELECT * FROM Orders WHERE Status = @status", 
    new { status = "Active" });

// Insert a record
connection.Execute("INSERT INTO Orders (Name, Status) VALUES (@name, @status)",
    new { name = "New Order", status = "Pending" });
```

## Core Concepts

DataProvider is built around these key principles:

- **Source Generation**: SQL queries generate type-safe extension methods at compile time
- **No ORM Overhead**: Direct SQL execution without mapping layers
- **Result Types**: All operations return `Result<T,E>` for error handling without exceptions

## Next Steps

- [DataProvider Documentation](/docs/dataprovider/)
- [LQL Query Language](/docs/lql/)
- [API Reference](/api/)
