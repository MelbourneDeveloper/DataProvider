# Lambda Query Language (LQL)

A functional pipeline-style DSL that transpiles to SQL. Write database logic once, run it on PostgreSQL, SQLite, or SQL Server.

```lql
Users
|> filter(fn(row) => row.Age > 18 and row.Status = 'active')
|> join(Orders, on = Users.Id = Orders.UserId)
|> select(Users.Name, sum(Orders.Total) as TotalSpent)
```

## Projects

| Project | Description |
|---------|-------------|
| `Nimblesite.Lql.Core` | Core transpiler library |
| `Nimblesite.Lql.Cli.SQLite` | CLI transpiler tool |
| `LqlExtension` | VS Code extension (TypeScript) |
| `lql-lsp-rust` | Language server (Rust) |
| `Nimblesite.Lql.TypeProvider.FSharp` | F# type provider for compile-time validation |

## Documentation

- LQL spec: [docs/specs/lql-spec.md](../docs/specs/lql-spec.md)
- LQL design system: [docs/specs/lql-design-system.md](../docs/specs/lql-design-system.md)
- LQL reference (compiled into LSP): [lql-lsp-rust/crates/lql-reference.md](lql-lsp-rust/crates/lql-reference.md)
- Website docs: [Website/src/docs/lql.md](../Website/src/docs/lql.md)
