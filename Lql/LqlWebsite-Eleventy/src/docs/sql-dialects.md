---
layout: layouts/docs.njk
title: SQL Dialects
description: How LQL transpiles to PostgreSQL, SQL Server, and SQLite.
---

LQL is database platform independent. The same query transpiles to correct SQL for each target database.

## Supported Dialects

| Dialect | Package | Status |
|---------|---------|--------|
| PostgreSQL | `Lql.Postgres` | Full support |
| SQL Server | `Lql.SqlServer` | Full support |
| SQLite | `Lql.SQLite` | Full support |

## Example

This LQL query:

```
users
|> filter(fn(row) => row.users.age > 18)
|> select(users.name, users.email)
|> order_by(users.name asc)
|> limit(10)
```

### PostgreSQL Output

```sql
SELECT users.name, users.email
FROM users
WHERE users.age > 18
ORDER BY users.name ASC
LIMIT 10
```

### SQL Server Output

```sql
SELECT TOP 10 users.name, users.email
FROM users
WHERE users.age > 18
ORDER BY users.name ASC
```

### SQLite Output

```sql
SELECT users.name, users.email
FROM users
WHERE users.age > 18
ORDER BY users.name ASC
LIMIT 10
```

## Dialect Differences Handled by LQL

LQL abstracts away common dialect differences:

- **LIMIT/TOP** - PostgreSQL and SQLite use `LIMIT`, SQL Server uses `TOP`
- **String concatenation** - `||` vs `+`
- **Boolean literals** - `TRUE`/`FALSE` vs `1`/`0`
- **ILIKE** - PostgreSQL-specific case-insensitive LIKE

## Programmatic Dialect Selection

```csharp
using Lql;
using Lql.Postgres;
using Lql.SqlServer;
using Lql.SQLite;

var lql = "Users |> select(Users.Name)";
var statement = LqlStatementConverter.ToStatement(lql);

// Generate for each dialect
var postgres = statement.ToPostgreSql();
var sqlServer = statement.ToSqlServer();
var sqlite = statement.ToSQLite();
```
