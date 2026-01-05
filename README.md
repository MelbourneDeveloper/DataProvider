# DataProvider Suite

DataProvider is a complete toolkit for .NET database access that prioritizes **type safety** in the same way that common ORMs do. It provides source-generated SQL extensions, a cross-database query language, offline-first synchronization, and schema migrations.

## Philosophy

DataProvider fixes the issues that have plagued .NET data access for decades:

**The simplicity and safety of an ORM** but without the issues that come along with them. DataProvider generates extension methods directly from your SQL. You write the queries. You see what executes. SQL errors result in compilation errors. No magic. Intellisense/Autocomplete in SQL coming soon.

**Sync data** across microservices or create occasionally connected apps that only sync when there is an internet connection.

**No Exceptions** Database operations fail. Networks drop. Constraints get violated. These aren't exceptional. They're expected. Every DataProvider operation returns `Result<T, Error>` instead of throwing. Pattern match on the result. Handle both cases explicitly. Your code becomes honest about what can go wrong.

**SQL is the source of truth.** Your database schema and queries define your application's data model. DataProvider works with this reality instead of fighting it. Define schemas in YAML. Write queries in SQL or LQL. Generate strongly-typed code from both.

## The Stack

| Component | Purpose |
|-----------|---------|
| [DataProvider](./DataProvider/README.md) | Source generator: SQL files become type-safe extension methods |
| [LQL](./Lql/README.md) | Lambda Query Language: Write once, transpile to any SQL dialect |
| [Migrations](./Migration/README.md) | YAML schemas: Database-agnostic, version-controlled schema definitions |
| [Sync](./Sync/README.md) | Offline-first: Bidirectional synchronization with conflict resolution |
| [Gatekeeper](./Gatekeeper/README.md) | Auth: WebAuthn authentication and role-based access control |

Each component works independently or together. Use what you need.

## Quick Example

Write SQL in a `.sql` file:

```sql
-- GetActiveCustomers.sql
SELECT c.Id, c.Name, a.City
FROM Customer c
JOIN Address a ON c.Id = a.CustomerId
WHERE c.IsActive = 1
LIMIT 100;
```

DataProvider generates type-safe extension methods at compile time:

```csharp
var result = await connection.GetActiveCustomersAsync(cancellationToken);
if (result.IsSuccess)
{
    foreach (var customer in result.Value)
    {
        Console.WriteLine($"{customer.Name} from {customer.City}");
    }
}
```

## Getting Started

### Prerequisites
- .NET 8.0 or later
- Visual Studio 2022 or VS Code
- Database (SQLite, SQL Server, or PostgreSQL)

### Installation

```bash
# Install the core package and database-specific package
dotnet add package DataProvider
dotnet add package DataProvider.SQLite  # or DataProvider.SqlServer
```

### Build from Source

```bash
git clone https://github.com/MelbourneDeveloper/DataProvider.git
cd DataProvider
dotnet build DataProvider.sln
dotnet test
dotnet csharpier .
```

## Performance

All components are designed for maximum performance:
- **Zero runtime overhead**: Generated code is pure ADO.NET
- **AOT compatible**: Full ahead-of-time compilation support
- **No reflection**: All code is generated at compile time
- **Minimal allocations**: Optimized for low memory usage

## Contributing

The main structure of the projects is not stable. Focus on bug fixes or small functionality additions. Log an issue or start a discussion to check if your ideas match the project goals.

1. Read the [CLAUDE.md](CLAUDE.md) file for code style guidelines
2. Ensure all tests pass
3. Format code with `dotnet csharpier .`
4. Submit pull requests to the `main` branch
