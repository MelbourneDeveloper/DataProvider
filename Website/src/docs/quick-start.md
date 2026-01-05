---
layout: layouts/docs.njk
title: Quick Start
description: Get up and running with DataProvider in minutes.
---

## Create a Connection

```csharp
using System.Data.SqlClient;
using DataProvider;

var connectionString = "Server=localhost;Database=MyDb;...";
using var connection = new SqlConnection(connectionString);
connection.Open();
```

## Execute Queries

### Select

```csharp
var orders = connection.Query<Order>(
    "SELECT * FROM Orders WHERE Status = @status",
    new { status = "Active" }
);

foreach (var order in orders)
{
    Console.WriteLine($"{order.Id}: {order.Name}");
}
```

### Insert

```csharp
var affectedRows = connection.Execute(
    "INSERT INTO Orders (Name, Status) VALUES (@name, @status)",
    new { name = "New Order", status = "Pending" }
);
```

### Update

```csharp
connection.Execute(
    "UPDATE Orders SET Status = @status WHERE Id = @id",
    new { status = "Completed", id = 123 }
);
```

### Delete

```csharp
connection.Execute(
    "DELETE FROM Orders WHERE Id = @id",
    new { id = 123 }
);
```

## Using Transactions

```csharp
using var transaction = connection.BeginTransaction();
try
{
    transaction.Execute("INSERT INTO ...");
    transaction.Execute("UPDATE ...");
    transaction.Commit();
}
catch
{
    transaction.Rollback();
    throw;
}
```

## Using LQL for Cross-Database Queries

Instead of writing raw SQL, use LQL to write queries that work across all databases:

```csharp
using Lql;
using Lql.SQLite;

var lql = "Orders |> filter(fn(row) => row.Status = 'Active') |> select(Id, Name)";
var sql = LqlCodeParser.Parse(lql).ToSql(new SQLiteContext());
```

For F# projects, use the Type Provider for compile-time validation:

```fsharp
open Lql

type ActiveOrders = LqlCommand<"Orders |> filter(fn(row) => row.Status = 'Active') |> select(*)">
let sql = ActiveOrders.Sql  // SQL validated at compile time
```

## Next Steps

- [DataProvider Documentation](/docs/dataprovider/)
- [LQL Query Language](/docs/lql/) - Cross-database query language
- [F# Type Provider](/docs/lql/#f-type-provider) - Compile-time LQL validation
- [API Reference](/api/)
