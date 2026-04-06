# DataProvider

A .NET source generator that creates compile-time safe database extension methods from SQL queries. Validates queries at compile time and generates strongly-typed C# code with `Result<T,E>` error handling.

Supports SQLite and SQL Server. Works with both `.sql` and `.lql` files.

## Quick Start

```xml
<PackageReference Include="DataProvider.SQLite" Version="*" />
```

```csharp
var result = await connection.GetCustomersAsync(isActive: true);
```

## Documentation

- Full usage and configuration details are in the [DataProvider website docs](../Website/src/docs/dataprovider.md)
- Migration CLI spec: [docs/specs/migration-cli-spec.md](../docs/specs/migration-cli-spec.md)
