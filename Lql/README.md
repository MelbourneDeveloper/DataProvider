# Lambda Query Language (LQL)

A functional, pipeline-style DSL that transpiles to SQL. Write database logic once and run it against **PostgreSQL**, **SQLite**, or **SQL Server**.

```
Users
|> filter(fn(row) => row.Age > 18 and row.Status = 'active')
|> join(Orders, on = Users.Id = Orders.UserId)
|> select(Users.Name, sum(Orders.Total) as TotalSpent)
|> group_by(Users.Id)
|> order_by(TotalSpent desc)
```

LQL is **database platform independent**. The same `.lql` source must produce semantically identical results on every target dialect.

## Install

### Build-time transpilation (recommended)

```bash
dotnet new tool-manifest
dotnet tool install Lql --version 0.9.6-beta
```

Then transpile during the build:

```bash
dotnet Lql sqlite   --input GetCustomers.lql --output GetCustomers.generated.sql
dotnet Lql postgres --input GetCustomers.lql --output GetCustomers.generated.sql
```

### Runtime transpilation

Reference one of the library packages to transpile LQL in your application code:

```bash
dotnet add package Nimblesite.Lql.SQLite   --version 0.9.6-beta
dotnet add package Nimblesite.Lql.Postgres --version 0.9.6-beta
dotnet add package Nimblesite.Lql.SqlServer --version 0.9.6-beta
```

## Runtime API

```csharp
using Nimblesite.Lql.Core;
using Nimblesite.Lql.Postgres;
using Nimblesite.Sql.Model;

var lql = """
Customer
|> filter(fn(row) => Customer.Active = true)
|> select(Customer.Id, Customer.Name)
""";

var statement = new LqlStatement(lql);
Result<string, SqlError> result = statement.ToPostgreSql();

var sql = result switch
{
    Result<string, SqlError>.Ok ok => ok.Value,
    Result<string, SqlError>.Error err =>
        throw new InvalidOperationException(err.Value.Message)
};
```

`statement.ToSqlite()` and `statement.ToSqlServer()` are also available from their respective packages.

## Projects

| Project | Description |
|---------|-------------|
| `Nimblesite.Lql.Core` | Core transpiler library and AST |
| `Lql` | Unified CLI transpiler tool (subcommands: `postgres`, `sqlite`) |
| `Nimblesite.Lql.Postgres` | `ToPostgreSql()` extension |
| `Nimblesite.Lql.SQLite` | `ToSqlite()` extension |
| `Nimblesite.Lql.SqlServer` | `ToSqlServer()` extension |
| `LqlExtension` | VS Code extension (TypeScript) |
| `lql-lsp-rust` | Language server (Rust, ANTLR-generated parser) |
| `Nimblesite.Lql.TypeProvider.FSharp` | F# type provider for compile-time validation |

## Pipeline operators

| Operator | Purpose |
|----------|---------|
| `filter(fn(row) => ...)` | WHERE |
| `select(col1, col2, ...)` | SELECT projection |
| `join(Table, on = ...)` | INNER JOIN (plus `left_join`, `right_join`, `full_join`) |
| `group_by(col)` | GROUP BY |
| `order_by(col [asc|desc])` | ORDER BY |
| `limit(n)` | LIMIT / TOP |
| `distinct()` | DISTINCT |

Aggregates include `count`, `sum`, `avg`, `min`, `max`. Parameters are declared with `@name`.

## Related documentation

- LQL language spec: [docs/specs/lql-spec.md](../docs/specs/lql-spec.md)
- LQL design system: [docs/specs/lql-design-system.md](../docs/specs/lql-design-system.md)
- LSP reference (used by IDE): [lql-lsp-rust/crates/lql-reference.md](lql-lsp-rust/crates/lql-reference.md)
