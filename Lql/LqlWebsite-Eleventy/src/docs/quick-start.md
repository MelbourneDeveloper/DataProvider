---
layout: layouts/docs.njk
title: Quick Start
description: Get up and running with LQL in minutes.
---

## Install

### NuGet Packages

```xml
<PackageReference Include="Lql.SQLite" Version="*" />
<PackageReference Include="Lql.Postgres" Version="*" />
<PackageReference Include="Lql.SqlServer" Version="*" />
```

### CLI Tool

```bash
dotnet tool install -g LqlCli.SQLite
```

## Your First Query

Write your first LQL query:

```
users |> select(users.id, users.name, users.email)
```

This transpiles to:

```sql
SELECT users.id, users.name, users.email FROM users
```

## Programmatic Usage

```csharp
using Lql;
using Lql.SQLite;

var lql = "Users |> filter(fn(row) => row.Age > 21) |> select(Name, Email)";
var sql = LqlCodeParser.Parse(lql).ToSql(new SQLiteContext());
```

## CLI Usage

```bash
lql --input query.lql --output query.sql
```

## Next Steps

- [Syntax Overview](/docs/syntax/) - Learn the full language syntax
- [Pipeline Operators](/docs/pipelines/) - Deep dive into pipeline operations
- [Playground](/playground/) - Try LQL interactively in your browser
