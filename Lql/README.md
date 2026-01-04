# Lambda Query Language (LQL)

A functional pipeline-style DSL that transpiles to SQL. Write database logic once, run it anywhere.

## The Problem

SQL dialects differ. PostgreSQL, SQLite, and SQL Server each have their own quirks. This creates problems:

- **Migrations** - Schema changes need different SQL for each database
- **Business Logic** - Triggers, stored procedures, and constraints vary by vendor
- **Sync Logic** - Offline-first apps need identical logic on client (SQLite) and server (Postgres)
- **Testing** - Running tests against SQLite while production uses Postgres

## The Solution

LQL is a single query language that transpiles to any SQL dialect. Write once, deploy everywhere.

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

## Use Cases

### Cross-Database Migrations
Define schema changes in LQL. Migration.CLI generates the right SQL for your target database.

### Business Logic
Write triggers and constraints in LQL. Deploy the same logic to any database.

### Offline-First Sync
Sync framework uses LQL for conflict resolution. Same logic runs on mobile (SQLite) and server (Postgres).

### Integration Testing
Test against SQLite locally, deploy to Postgres in production. Same queries, same results.

## Quick Start

### CLI Tool
```bash
dotnet tool install -g LqlCli.SQLite
lql --input query.lql --output query.sql
```

### NuGet Packages
```xml
<PackageReference Include="Lql.SQLite" Version="*" />
<PackageReference Include="Lql.Postgres" Version="*" />
<PackageReference Include="Lql.SqlServer" Version="*" />
```

### Programmatic Usage
```csharp
using Lql;
using Lql.SQLite;

var lql = "Users |> filter(fn(row) => row.Age > 21) |> select(Name, Email)";
var sql = LqlCodeParser.Parse(lql).ToSql(new SQLiteContext());
```

## F# Type Provider

Validate LQL queries at compile time. Invalid queries cause compilation errors, not runtime errors.

```fsharp
open Lql.TypeProvider

type GetUsers = LqlCommand<"Users |> select(Id, Name, Email)">
type ActiveUsers = LqlCommand<"Users |> filter(fn(row) => row.Status = 'active') |> select(*)">

let sql = GetUsers.Sql  // SQL generated at compile time
```

## Pipeline Operations

| Operation | Description |
|-----------|-------------|
| `select(cols...)` | Choose columns |
| `filter(fn(row) => ...)` | Filter rows |
| `join(table, on = ...)` | Join tables |
| `left_join(table, on = ...)` | Left join |
| `group_by(cols...)` | Group rows |
| `having(fn(row) => ...)` | Filter groups |
| `order_by(col [asc/desc])` | Sort results |
| `limit(n)` / `offset(n)` | Pagination |
| `distinct()` | Unique rows |
| `union(query)` | Combine queries |

## VS Code Extension

Search for "LQL" in VS Code Extensions for syntax highlighting and IntelliSense.

## Website

Visit [lql.dev](https://lql.dev) for interactive playground.

## License

MIT License
