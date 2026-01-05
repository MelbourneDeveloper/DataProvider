# DataProvider

A .NET source generator that creates compile-time safe database extension methods from SQL queries. DataProvider eliminates runtime SQL errors by validating queries at compile time and generating strongly-typed C# code.

## Features

- **Compile-Time Safety** - SQL queries are validated during compilation, catching errors before runtime
- **Auto-Generated Extensions** - Creates extension methods on `IDbConnection` and `IDbTransaction`
- **Schema Inspection** - Automatically inspects database schema to generate appropriate types
- **Result Type Pattern** - All operations return `Result<T,E>` types for explicit error handling
- **Multi-Database Support** - Currently supports SQLite and SQL Server
- **LQL Integration** - Seamlessly works with Lambda Query Language files

## How It Works

1. **Define SQL Queries** - Place `.sql` or `.lql` files in your project
2. **Configure Generation** - Set up `DataProvider.json` configuration
3. **Build Project** - Source generators create extension methods during compilation
4. **Use Generated Code** - Call type-safe methods with full IntelliSense support

## Installation

### SQLite
```xml
<PackageReference Include="DataProvider.SQLite" Version="*" />
```

### SQL Server
```xml
<PackageReference Include="DataProvider.SqlServer" Version="*" />
```

## Database Schema Setup (Migrations)

DataProvider requires a database with schema to exist **before** code generation runs. The schema allows the generator to introspect table structures and generate correct types.

### Required Build Order

```
1. Export C# Schema to YAML (if schema defined in code)
2. Run Migration.Cli to create database from YAML
3. Run DataProvider code generation
```

### Using Migration.Cli

Migration.Cli is the **single canonical tool** for creating databases from schema definitions. All projects that need a build-time database MUST use this tool.

```bash
dotnet run --project Migration/Migration.Cli/Migration.Cli.csproj -- \
    --schema path/to/schema.yaml \
    --output path/to/database.db \
    --provider sqlite
```

### MSBuild Integration

Configure your `.csproj` to run migrations before code generation:

```xml
<!-- Create database with schema before code generation -->
<Target Name="CreateDatabaseSchema" BeforeTargets="GenerateDataProvider">
    <Exec Command='dotnet run --project "$(SolutionDir)Migration/Migration.Cli/Migration.Cli.csproj" -- --schema "$(MSBuildProjectDirectory)/schema.yaml" --output "$(MSBuildProjectDirectory)/build.db" --provider sqlite' />
</Target>

<!-- Generate C# from SQL using DataProvider.SQLite.Cli -->
<Target Name="GenerateDataProvider" BeforeTargets="BeforeCompile;CoreCompile">
    <Exec Command='dotnet run --project "$(SolutionDir)DataProvider/DataProvider.SQLite.Cli/DataProvider.SQLite.Cli.csproj" -- --project-dir "$(MSBuildProjectDirectory)" --config "$(MSBuildProjectDirectory)/DataProvider.json" --out "$(MSBuildProjectDirectory)/Generated"' />
</Target>
```

### YAML Schema Format

See [this](Migration/migration_exe_spec.md)

### Exporting C# Schemas to YAML

If your schema is defined in C# code using the Migration fluent API:

```csharp
var schema = Schema.Define("my_schema")
    .Table("Customer", t => t
        .Column("Id", Text, c => c.PrimaryKey())
        .Column("Name", Text, c => c.NotNull())
    )
    .Build();

// Export to YAML
SchemaYamlSerializer.ToYamlFile(schema, "schema.yaml");
```

### Avoiding Circular Dependencies

**CRITICAL:** Projects that use DataProvider code generation MUST NOT have circular dependencies with the CLI tools.

**The Problem:** If your project references `DataProvider.csproj` AND runs `DataProvider.SQLite.Cli` as a build target, you create an infinite build loop:
```
YourProject → DataProvider → (build target) DataProvider.SQLite.Cli → DataProvider → ...
```

**How to Fix:**
1. **Remove the `ProjectReference` to DataProvider.csproj** from projects that run the CLI as a build target
2. **Use raw YAML schemas checked into git** - do NOT export C# schemas to YAML at build time
3. **Migration.Cli is safe** - it does NOT depend on DataProvider, only on Migration projects

**The Rule:** YAML schema files are source of truth. Check them into git. Never generate them at build time. The C# → YAML export is a one-time developer action, not a build step.

**Correct pattern:**
```xml
<!-- SAFE: Migration.Cli has no DataProvider dependency -->
<Target Name="CreateDatabaseSchema" BeforeTargets="GenerateDataProvider">
    <Exec Command='dotnet run --project Migration.Cli -- --schema schema.yaml --output build.db' />
</Target>

<!-- SAFE: DataProvider.SQLite.Cli runs AFTER schema exists, no circular ref -->
<Target Name="GenerateDataProvider" BeforeTargets="BeforeCompile">
    <Exec Command='dotnet run --project DataProvider.SQLite.Cli -- ...' />
</Target>
```

**Wrong pattern:**
```xml
<!-- BROKEN: Project references DataProvider AND runs CLI that depends on DataProvider -->
<ProjectReference Include="DataProvider.csproj" />  <!-- REMOVE THIS -->
```

### Forbidden Patterns

- **NO raw SQL DDL files** - Use Migration.Cli with YAML
- **NO individual BuildDb projects** - Use Migration.Cli (single tool)
- **NO `schema.sql` files** - YAML schemas only
- **NO code generation before schema creation** - Migration MUST run first
- **NO C# schema export at build time** - Export once, commit YAML to git

## Configuration

Create a `DataProvider.json` file in your project root:

```json
{
  "ConnectionString": "Data Source=mydatabase.db",
  "Namespace": "MyApp.DataAccess",
  "OutputDirectory": "Generated",
  "Queries": [
    {
      "Name": "GetCustomers",
      "SqlFile": "Queries/GetCustomers.sql"
    },
    {
      "Name": "GetOrders",
      "SqlFile": "Queries/GetOrders.lql"
    }
  ]
}
```

## Usage Examples

### Simple Query

SQL file (`GetCustomers.sql`):
```sql
SELECT Id, Name, Email 
FROM Customers 
WHERE IsActive = @isActive
```

Generated C# usage:
```csharp
using var connection = new SqliteConnection(connectionString);
var result = await connection.GetCustomersAsync(isActive: true);

if (result.IsSuccess)
{
    foreach (var customer in result.Value)
    {
        Console.WriteLine($"{customer.Name}: {customer.Email}");
    }
}
else
{
    Console.WriteLine($"Error: {result.Error.Message}");
}
```

### With LQL

LQL file (`GetOrders.lql`):
```lql
Order
|> join(Customer, on = Order.CustomerId = Customer.Id)
|> filter(fn(row) => row.Order.OrderDate >= @startDate)
|> select(Order.Id, Order.Total, Customer.Name)
```

This automatically generates:
```csharp
var orders = await connection.GetOrdersAsync(
    startDate: DateTime.Now.AddDays(-30)
);
```

### Transaction Support

```csharp
using var connection = new SqliteConnection(connectionString);
connection.Open();
using var transaction = connection.BeginTransaction();

var insertResult = await transaction.InsertCustomerAsync(
    name: "John Doe",
    email: "john@example.com"
);

if (insertResult.IsSuccess)
{
    transaction.Commit();
}
else
{
    transaction.Rollback();
}
```

## Grouping Configuration

For complex result sets with joins, configure grouping in a `.grouping.json` file:

```json
{
  "PrimaryKey": "Id",
  "GroupBy": ["Id"],
  "Collections": {
    "Addresses": {
      "ForeignKey": "CustomerId",
      "Properties": ["Street", "City", "State"]
    }
  }
}
```

## Architecture

DataProvider follows functional programming principles:

- **No Classes** - Uses records and static extension methods
- **No Exceptions** - Returns `Result<T,E>` types for all operations
- **Pure Functions** - Static methods with no side effects
- **Expression-Based** - Prefers expressions over statements

## Project Structure

```
DataProvider/
├── DataProvider/              # Core library and base types
├── DataProvider.SQLite/       # SQLite implementation
│   ├── Parsing/              # ANTLR grammar and parsers
│   └── SchemaInspection/     # Schema discovery
├── DataProvider.SqlServer/    # SQL Server implementation
│   └── SchemaInspection/
├── DataProvider.Example/      # Example usage
└── DataProvider.Tests/        # Unit tests
```

## Testing

Run tests with:
```bash
dotnet test DataProvider.Tests/DataProvider.Tests.csproj
```

## Performance

- **Zero Runtime Overhead** - All SQL parsing and validation happens at compile time
- **Minimal Allocations** - Uses value types and expressions where possible
- **Async/Await** - Full async support for all database operations

## Logging

Generated methods support optional `ILogger` injection for observability. Pass an `ILogger` instance to any generated method:

```csharp
var result = await connection.GetCustomersAsync(isActive: true, logger: _logger);
```

Logging includes query timing, parameter values (debug level), row counts, and structured error context. Zero overhead when logger is null.

## Error Handling

All methods return `Result<T,E>` types:

```csharp
var result = await connection.ExecuteQueryAsync();

var output = result switch
{
    { IsSuccess: true } => ProcessData(result.Value),
    { Error: SqlError error } => HandleError(error),
    _ => "Unknown error"
};
```

## Contributing

1. Follow the functional programming style (no classes, no exceptions)
2. Keep files under 450 lines
3. All public members must have XML documentation
4. Run `dotnet csharpier .` before committing
5. Ensure all tests pass

## License

MIT License

## Author

MelbourneDeveloper - [ChristianFindlay.com](https://christianfindlay.com)