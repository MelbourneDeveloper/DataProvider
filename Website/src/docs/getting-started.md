---
layout: layouts/docs.njk
title: Getting Started
description: Get started with DataProvider for .NET data access.
---

This guide walks you through setting up DataProvider and running your first query.

## Prerequisites

- .NET 9.0 SDK or later
- A database (SQLite, SQL Server, or PostgreSQL)

## Installation

Add DataProvider to your .NET project:

```bash
dotnet add package DataProvider
```

For database-specific providers:

```bash
# SQLite
dotnet add package DataProvider.SQLite

# SQL Server
dotnet add package DataProvider.SqlServer

# PostgreSQL
dotnet add package DataProvider.Postgres
```

## Project Setup

Configure your project for DataProvider:

```xml
<PropertyGroup>
  <TargetFramework>net9.0</TargetFramework>
  <Nullable>enable</Nullable>
  <LangVersion>latest</LangVersion>
</PropertyGroup>
```

## Your First Query

### 1. Create a SQL File

Add a `GetOrders.sql` file to your project:

```sql
SELECT Id, CustomerName, Total, Status
FROM Orders
WHERE Status = @status
```

### 2. Mark it as AdditionalFiles

```xml
<ItemGroup>
  <AdditionalFiles Include="Queries/*.sql" />
</ItemGroup>
```

### 3. Use the Generated Extension

```csharp
using Microsoft.Data.Sqlite;
using Generated;

using var connection = new SqliteConnection("Data Source=app.db");
connection.Open();

var result = await connection.GetOrdersAsync(status: "active");

if (result is GetOrdersResult.Ok ok)
{
    foreach (var order in ok.Value)
    {
        Console.WriteLine($"{order.CustomerName}: ${order.Total}");
    }
}
```

## Understanding Result Types

DataProvider never throws exceptions for expected failures. Every operation returns a `Result<T, Error>`:

```csharp
var result = await connection.GetOrdersAsync(status: "active");

// Pattern match on the result
var message = result switch
{
    GetOrdersResult.Ok ok => $"Found {ok.Value.Count} orders",
    GetOrdersResult.Error err => $"Query failed: {err.Value.Message}"
};
```

## Next Steps

- [Installation](/docs/installation/) - Detailed package installation guide
- [Quick Start](/docs/quick-start/) - More query examples
- [DataProvider](/docs/dataprovider/) - Full documentation
- [LQL](/docs/lql/) - Cross-database query language
