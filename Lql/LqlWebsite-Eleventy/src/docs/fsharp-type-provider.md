---
layout: layouts/docs.njk
title: F# Type Provider
description: Compile-time validated LQL queries in F# with the LQL Type Provider.
---

The LQL Type Provider brings compile-time type checking to your LQL queries in F#. Write queries with IntelliSense support, catch errors before runtime, and enjoy seamless integration with your F# codebase.

## Installation

```xml
<PackageReference Include="Lql.TypeProvider.FSharp" Version="*" />
```

## Basic Usage

```fsharp
open Lql

// Define types with validated LQL - errors caught at COMPILE TIME
type GetUsers = LqlCommand<"Users |> select(Users.Id, Users.Name, Users.Email)">
type ActiveUsers = LqlCommand<"Users |> filter(fn(row) => row.Status = 'active') |> select(*)">

// Access generated SQL and original query
let sql = GetUsers.Sql      // Generated SQL string
let query = GetUsers.Query  // Original LQL string
```

## What Gets Validated

The type provider validates your LQL at compile time and generates two properties:
- `Query` - The original LQL query string
- `Sql` - The generated SQL (SQLite dialect)

## Query Examples

```fsharp
// Select with columns
type SelectColumns = LqlCommand<"Users |> select(Users.Id, Users.Name, Users.Email)">

// Filtering with AND/OR
type FilterComplex = LqlCommand<"Users |> filter(fn(row) => row.Users.Age > 18 and row.Users.Status = 'active') |> select(*)">

// Joins
type JoinQuery = LqlCommand<"Users |> join(Orders, on = Users.Id = Orders.UserId) |> select(Users.Name, Orders.Total)">
type LeftJoin = LqlCommand<"Users |> left_join(Orders, on = Users.Id = Orders.UserId) |> select(*)">

// Aggregations with GROUP BY and HAVING
type GroupBy = LqlCommand<"Orders |> group_by(Orders.UserId) |> select(Orders.UserId, count(*) as order_count)">
type Having = LqlCommand<"Orders |> group_by(Orders.UserId) |> having(fn(g) => count(*) > 5) |> select(Orders.UserId, count(*) as cnt)">

// Order, limit, offset
type Pagination = LqlCommand<"Users |> order_by(Users.Name asc) |> limit(10) |> offset(20) |> select(*)">

// Arithmetic expressions
type Calculated = LqlCommand<"Products |> select(Products.Price * Products.Quantity as total)">
```

## Compile-Time Error Example

Invalid LQL causes a build error with line/column position:

```fsharp
// This FAILS to compile with: "Invalid LQL syntax at line 1, column 15"
type BadQuery = LqlCommand<"Users |> selectt(*)">  // typo: 'selectt'
```

## Executing Queries

```fsharp
open Microsoft.Data.Sqlite

let executeQuery() =
    use conn = new SqliteConnection("Data Source=mydb.db")
    conn.Open()

    // SQL is validated at compile time, safe to execute
    use cmd = new SqliteCommand(GetUsers.Sql, conn)
    use reader = cmd.ExecuteReader()
    // ... process results
```
