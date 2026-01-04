---
layout: layouts/docs.njk
title: "Lambda Query Language (LQL)"
---

# Lambda Query Language (LQL)

A functional pipeline-style DSL that transpiles to SQL. LQL provides an intuitive, composable way to write database queries using lambda expressions and pipeline operators, making complex queries more readable and maintainable.

## Website

Visit [lql.dev](https://lql.dev) for interactive playground and documentation.

## Features

- **Pipeline Syntax** - Chain operations using `|>` operator
- **Lambda Expressions** - Use familiar lambda syntax for filtering
- **Cross-Database Support** - Transpiles to PostgreSQL, SQLite, and SQL Server
- **Type Safety** - Integrates with DataProvider for compile-time validation
- **VS Code Extension** - Syntax highlighting and IntelliSense support
- **CLI Tools** - Command-line transpilation and validation

## Syntax Overview

### Basic Pipeline
```lql
users |> select(id, name, email)
```

### With Filtering
```lql
employees
|> filter(fn(row) => row.salary > 50000)
|> select(id, name, salary)
```

### Joins
```lql
Customer
|> join(Order, on = Customer.Id = Order.CustomerId)
|> select(Customer.Name, Order.Total)
```

### Complex Queries
```lql
let high_value_customers = Customer
|> join(Order, on = Customer.Id = Order.CustomerId)
|> filter(fn(row) => row.Order.Total > 1000)
|> group_by(Customer.Id, Customer.Name)
|> having(fn(row) => SUM(row.Order.Total) > 5000)
|> select(Customer.Name, SUM(Order.Total) AS TotalSpent)
|> order_by(TotalSpent DESC)
|> limit(10)
```

## Pipeline Operations

| Operation | Description | SQL Equivalent |
|-----------|-------------|----------------|
| `select(cols...)` | Choose columns | `SELECT` |
| `filter(fn(row) => ...)` | Filter rows | `WHERE` |
| `join(table, on = ...)` | Join tables | `JOIN` |
| `left_join(table, on = ...)` | Left join | `LEFT JOIN` |
| `group_by(cols...)` | Group rows | `GROUP BY` |
| `having(fn(row) => ...)` | Filter groups | `HAVING` |
| `order_by(col [ASC/DESC])` | Sort results | `ORDER BY` |
| `limit(n)` | Limit rows | `LIMIT` |
| `offset(n)` | Skip rows | `OFFSET` |
| `distinct()` | Unique rows | `DISTINCT` |
| `union(query)` | Combine queries | `UNION` |
| `union_all(query)` | Combine with duplicates | `UNION ALL` |

## Installation

### CLI Tool (SQLite)
```bash
dotnet tool install -g LqlCli.SQLite
```

### VS Code Extension
Search for "LQL" in VS Code Extensions or:
```bash
code --install-extension lql-lang
```

### NuGet Packages
```xml
<!-- For SQLite -->
<PackageReference Include="Lql.SQLite" Version="*" />

<!-- For SQL Server -->
<PackageReference Include="Lql.SqlServer" Version="*" />

<!-- For PostgreSQL -->
<PackageReference Include="Lql.Postgres" Version="*" />
```

## CLI Usage

### Transpile to SQL
```bash
lql --input query.lql --output query.sql
```

### Validate Syntax
```bash
lql --input query.lql --validate
```

### Print to Console
```bash
lql --input query.lql
```

## Programmatic Usage

```csharp
using Lql;
using Lql.SQLite;

// Parse LQL
var lqlCode = "users |> filter(fn(row) => row.age > 21) |> select(name, email)";
var statement = LqlCodeParser.Parse(lqlCode);

// Convert to SQL
var context = new SQLiteContext();
var sql = statement.ToSql(context);

Console.WriteLine(sql);
// Output: SELECT name, email FROM users WHERE age > 21
```

## Function Support

### Aggregate Functions
- `COUNT()`, `SUM()`, `AVG()`, `MIN()`, `MAX()`

### String Functions
- `UPPER()`, `LOWER()`, `LENGTH()`, `CONCAT()`

### Date Functions
- `NOW()`, `DATE()`, `YEAR()`, `MONTH()`

### Conditional
- `CASE WHEN ... THEN ... ELSE ... END`
- `COALESCE()`, `NULLIF()`

## Expression Support

### Arithmetic
```lql
products |> select(price * quantity AS total)
```

### Comparisons
```lql
orders |> filter(fn(row) => row.date >= '2024-01-01' AND row.status != 'cancelled')
```

### Pattern Matching
```lql
customers |> filter(fn(row) => row.name LIKE 'John%')
```

### Subqueries
```lql
orders |> filter(fn(row) => row.customer_id IN (
    customers |> filter(fn(c) => c.country = 'USA') |> select(id)
))
```

## VS Code Extension Features

- Syntax highlighting
- Auto-completion
- Error diagnostics
- Format on save
- Snippets for common patterns

## F# Type Provider

LQL includes an F# Type Provider that validates LQL queries at **compile time**. Invalid queries cause compilation errors, not runtime errors.

### Installation

```xml
<PackageReference Include="Lql.TypeProvider.FSharp" Version="*" />
```

### Usage

```fsharp
open Lql.TypeProvider

// These queries are validated at COMPILE TIME
type GetUsers = LqlCommand<"Users |> select(Id, Name, Email)">
type FilterActive = LqlCommand<"Users |> filter(fn(row) => row.Status = 'active') |> select(*)">
type JoinOrders = LqlCommand<"Users |> join(Orders, on = Users.Id = Orders.UserId) |> select(Users.Name, Orders.Total)">

// Access the generated SQL
let sql = GetUsers.Sql
let originalQuery = GetUsers.Query

// Execute against a database
use conn = new SqliteConnection("Data Source=mydb.db")
conn.Open()
use cmd = new SqliteCommand(GetUsers.Sql, conn)
use reader = cmd.ExecuteReader()
// ... process results
```

### Benefits

- **Compile-time validation** - Syntax errors caught during build
- **Type safety** - Generated types ensure correct usage
- **IntelliSense** - Full IDE support in F# editors
- **Zero runtime overhead** - SQL is generated at compile time

## Architecture

```
Lql/
├── Lql/                       # Core transpiler
│   ├── Parsing/               # ANTLR grammar and parser
│   ├── FunctionMapping/       # Database-specific functions
│   └── Pipeline steps         # AST transformation
├── Lql.SQLite/                # SQLite dialect
├── Lql.SqlServer/             # SQL Server dialect
├── Lql.Postgres/              # PostgreSQL dialect
├── Lql.TypeProvider.FSharp/   # F# Type Provider
├── LqlCli.SQLite/             # CLI tool
├── LqlExtension/              # VS Code extension
└── Website/                   # lql.dev website
```

## Testing

```bash
# C# tests
dotnet test Lql.Tests/Lql.Tests.csproj

# F# Type Provider tests
dotnet test Lql.TypeProvider.FSharp.Tests/Lql.TypeProvider.FSharp.Tests.fsproj
```

## Examples

See the `Lql.Tests/TestData/Lql/` directory for comprehensive examples of LQL queries and their SQL equivalents.

## Error Handling

LQL provides detailed error messages:

```lql
// Invalid: Identifier cannot start with number
123table |> select(id)
// Error: Syntax error at line 1:0 - Identifier cannot start with a number

// Invalid: Undefined variable
undefined_var |> select(name)
// Error: Syntax error at line 1:0 - Undefined variable
```

## Integration with DataProvider

LQL files are automatically processed by DataProvider source generators:

1. Write `.lql` files in your project
2. DataProvider transpiles to SQL during build
3. Generates type-safe C# extension methods
4. Use with full IntelliSense support

## Contributing

1. Follow functional programming principles
2. Add tests for new features
3. Update grammar file for syntax changes
4. Ensure all dialects are supported
5. Run tests before submitting PRs

## License

MIT License

## Author

MelbourneDeveloper - [ChristianFindlay.com](https://christianfindlay.com)
