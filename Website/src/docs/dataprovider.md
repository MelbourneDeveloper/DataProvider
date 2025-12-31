---
layout: layouts/docs.njk
title: DataProvider
description: Source generator for SQL that creates type-safe extension methods.
---

## Overview

DataProvider is the core component that generates type-safe extension methods for database operations. It analyzes your SQL queries at compile time and generates strongly-typed methods on `IDbConnection` and `IDbTransaction`.

## How It Works

1. Write your SQL queries
2. DataProvider analyzes them at compile time
3. Type-safe extension methods are generated
4. Use the generated methods with full IntelliSense support

## Key Features

### Source-Generated Extensions

All database operations are generated as extension methods:

```csharp
// Generated method signature
public static IEnumerable<Order> GetActiveOrders(this IDbConnection connection)
```

### Result Types

Operations return `Result<T,E>` for safe error handling:

```csharp
var result = connection.GetActiveOrders();
return result.Match(
    success: orders => Ok(orders),
    failure: error => BadRequest(error.Message)
);
```

### Transaction Support

Full transaction support with the same generated methods:

```csharp
using var transaction = connection.BeginTransaction();
transaction.InsertOrder(newOrder);
transaction.Commit();
```

## Configuration

Configure DataProvider in your project file:

```xml
<ItemGroup>
  <PackageReference Include="DataProvider" Version="*" />
</ItemGroup>
```

## Next Steps

- [LQL Query Language](/docs/lql/)
- [API Reference](/api/)
