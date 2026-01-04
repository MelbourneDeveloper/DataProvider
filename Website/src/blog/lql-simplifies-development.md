---
layout: layouts/blog.njk
title: "LQL: How Lambda Query Language Simplifies .NET Development"
description: A deep dive into LQL and its benefits for .NET data access, including the new F# Type Provider.
date: 2024-04-14
author: DataProvider Team
tags:
  - .NET
  - LQL
  - F#
  - post
---

LQL (Lambda Query Language) is a functional pipeline-style DSL that transpiles to SQL. Write database logic once, run it anywhere.

## Why LQL?

Traditional SQL strings are error-prone and differ across database vendors. LQL provides:

- **Write once, deploy everywhere**: Same query works on PostgreSQL, SQLite, and SQL Server
- **Compile-time validation**: Catch errors before runtime (especially with F# Type Provider)
- **Functional pipeline syntax**: Readable, composable query building
- **IDE support**: VS Code extension with syntax highlighting and IntelliSense

## Basic Example

```lql
Users
|> filter(fn(row) => row.Age > 18 and row.Status = 'active')
|> join(Orders, on = Users.Id = Orders.UserId)
|> group_by(Users.Id, Users.Name)
|> select(Users.Name, sum(Orders.Total) as TotalSpent)
|> order_by(TotalSpent desc)
|> limit(10)
```

This transpiles to correct SQL for PostgreSQL, SQLite, or SQL Server.

## F# Type Provider: Compile-Time Validation

The new F# Type Provider takes LQL to the next level by validating queries at compile time. Invalid LQL causes a build error, not a runtime crash.

```fsharp
open Lql

// These are validated when you compile - errors caught immediately
type GetUsers = LqlCommand<"Users |> select(Users.Id, Users.Name, Users.Email)">
type ActiveUsers = LqlCommand<"Users |> filter(fn(row) => row.Status = 'active') |> select(*)">

// Access the generated SQL
let sql = GetUsers.Sql  // SQL string ready to execute
```

Invalid queries fail the build with descriptive error messages:

```fsharp
// Build error: "Invalid LQL syntax at line 1, column 15"
type BadQuery = LqlCommand<"Users |> selectt(*)">  // typo in 'select'
```

The type provider supports all LQL operations:
- Select, filter, join, left_join
- Group by, having, order by
- Limit, offset, distinct
- Arithmetic expressions and aggregations (sum, avg, count, min, max)

Install it with:

```xml
<PackageReference Include="Lql.TypeProvider.FSharp" Version="*" />
```

## Getting Started

Try LQL in our [interactive playground](/lql/) or check out the [documentation](/docs/lql/).
