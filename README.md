# DataProvider & Lambda Query Language (LQL)

This repository contains two complementary projects that work together to provide compile-time safe database access and a functional query language for .NET applications.

[Try Lambda Query Language live](https://melbournedeveloper.github.io/DataProvider/#playground) in the browser now.

This is what LQL looks like when querying an SQLite database. App source [here](Lql/Lql.Browser).

![LQL DB Browser](lqldbbrowser.png)


## Aims

This project delivers compile-time safe, high-performance data access inspired by [F# Type Providers](https://learn.microsoft.com/en-us/dotnet/fsharp/tutorials/type-providers/) like [FSharp.Data.SqlClient](https://fsprojects.github.io/FSharp.Data.SqlClient/) and [SQLProvider](https://fsprojects.github.io/SQLProvider/). It solves the fundamental problems with existing ORMs through two complementary approaches: source generation and a functional query language.

### The Problems With Current Popular Data Access Approaches

#### Dapper
Runtime reflection means no compile-time type checking, no nullability guarantees, and potential incompatibility with AOT.

#### Entity Framework
LINQ expressions can't express complex queries like SQL can. It also gives you poor query optimization control. The abstraction layer adds overhead and complexity, making debugging and performance tuning difficult.

#### Query Objects (Common in CQRS Systems)
The most common problem is with the object-based query pattern popular in CQRS architectures. They usually start simple but accumulate business logic, filters, includes, and special cases over years until they're incomprehensible. 

Each developer adds their own fragment without understanding the whole, creating queries that no one fully understands and nobody can refactor. The abstraction that was meant to simplify queries becomes more complex than the SQL it was trying to hide. 

For example, this kind of query object is common in .NET codebases. The person looking at this code has no context of what the fields mean, or how these will be converted to SQL at runtime. 

```csharp
public class GetInvoicesQuery : IQuery<List<InvoiceDto>>
  {
      public int? CustomerId { get; set; }
      public DateTime? StartDate { get; set; }
      public DateTime? EndDate { get; set; }
      public bool? IsPaid { get; set; }
      public bool IncludeDeleted { get; set; }
      public decimal? MinAmount { get; set; }
      public decimal? MaxAmount { get; set; }
      public List<int> ExcludedCustomerIds { get; set; } = new();
      public bool OnlyOverdue { get; set; }
      public int? DaysOverdue { get; set; }
      public bool IncludeLineItems { get; set; }
      public bool IncludeCustomerDetails { get; set; }
      public bool ApplyLegacyDiscounts { get; set; }
      public string? Region { get; set; }
      public bool ExcludeDisputed { get; set; }
      public bool OnlyRecurring { get; set; }
      public List<string> ProductCodes { get; set; } = new();
      public bool GroupByCustomer { get; set; }
      public bool IncludePendingApproval { get; set; }
      public int? SalesRepId { get; set; }
      public bool ApplySpecialPricingRules { get; set; }
      public DateTime? SpecialPricingCutoffDate { get; set; }
      public bool ExcludeTestAccounts { get; set; }
      public bool OnlyWithCreditNotes { get; set; }
      public bool UseFiscalYearDates { get; set; }
      public int? FiscalYearOffset { get; set; }
  }
```

### The Solution

DataProvider generates pure C# code at compile time from your queries. There is no reflection, resulting in raw ADO.NET performance. It fully supports AOT compilation and leverages nullable reference types for complete null safety. Your queries are type-checked during compilation, catching errors before deployment while maintaining full SQL control.

LQL (Lambda Query Language) complements this by providing a functional pipeline syntax that transpiles to native SQL. Instead of archaic procedural SQL (T-SQL, PL/pgSQL), you write queries with lambda expressions and pipeline operators that feel natural to C# developers, and allow for complex business logic in triggers and functions.

LQL enables portable queries across databases while still allowing platform-specific SQL when needed or preferred. You can express triggers, functions, and stored procedures in maintainable, FP style code that transpiles to your database's native procedural SQL, or C# in the case of SQLite.

Together, they provide:
- Portability of querying and business logic at the database level with a well-designed FP style language
- Generate compile-time safe C# methods from either source
- Full control over SQL optimization with complete type safety

In other words, you can just write simple queries with SQL or LQL and get the same kind of compile-time safety that EF provides.

## Projects Overview

### 1. DataProvider
A source generator that creates compile-time safe extension methods for database operations from SQL files. It generates strongly-typed C# code based on your SQL queries and database schema, ensuring type safety and eliminating runtime SQL errors.

**Key Features:**
- Compile-time SQL validation against actual database schema
- Auto-generated extension methods on `IDbConnection` and `IDbTransaction`
- Support for SQLite, SQL Server, and PostgreSQL (coming soon)
- Automatic schema inspection and incremental code generation
- Result type pattern for functional error handling (no exceptions)
- Full AOT compilation support
- Zero runtime overhead - pure ADO.NET performance

[View DataProvider Documentation →](./DataProvider/README.md)

### 2. Lambda Query Language (LQL)
A functional pipeline-style DSL that transpiles to SQL. LQL provides a more intuitive and composable way to write database queries using lambda expressions and pipeline operators, bringing functional programming paradigms to database queries.

**Key Features:**
- Functional pipeline syntax using `|>` operator for query composition
- Lambda expressions for filtering, mapping, and transformations
- Cross-database support (PostgreSQL, SQLite, SQL Server)
- VS Code extension with syntax highlighting and IntelliSense
- CLI tools for transpilation and validation
- Support for triggers, functions, and stored procedures
- Browser-based playground for experimentation

[View LQL Documentation →](./Lql/README.md)

## How They Work Together

DataProvider and LQL integrate seamlessly:

1. **Write queries in LQL** - Use the intuitive pipeline syntax to express your database queries
2. **Transpile to SQL** - LQL files (`.lql`) are automatically converted to SQL files
3. **Generate type-safe code** - DataProvider source generators create extension methods from the SQL
4. **Use in your application** - Call the generated methods with full IntelliSense and compile-time safety

### Example Workflow

```lql
// GetActiveCustomers.lql
Customer
|> join(Address, on = Customer.Id = Address.CustomerId)
|> filter(fn(row) => row.Customer.IsActive = true and row.Address.Country = 'USA')
|> select({
    CustomerId = Customer.Id,
    CustomerName = Customer.Name,
    City = Address.City,
    State = Address.State
})
|> orderBy(Customer.Name)
|> limit(100)
```

This LQL query gets transpiled to optimized SQL:

```sql
SELECT 
    c.Id AS CustomerId,
    c.Name AS CustomerName,
    a.City,
    a.State
FROM Customer c
JOIN Address a ON c.Id = a.CustomerId
WHERE c.IsActive = 1 AND a.Country = 'USA'
ORDER BY c.Name
LIMIT 100;
```

And DataProvider generates type-safe extension methods:

```csharp
// Auto-generated extension method with full IntelliSense
var result = await connection.GetActiveCustomersAsync(cancellationToken);
if (result.IsSuccess)
{
    foreach (var customer in result.Value)
    {
        Console.WriteLine($"{customer.CustomerName} from {customer.City}, {customer.State}");
    }
}
```

## Getting Started

### Prerequisites
- .NET 8.0 or later
- Visual Studio 2022 or VS Code
- Database (SQLite, SQL Server, or PostgreSQL)

### Installation

#### For DataProvider:
```bash
# Install the core package and database-specific package
dotnet add package DataProvider
dotnet add package DataProvider.SQLite  # or DataProvider.SqlServer
```

#### For LQL:
```bash
# Install the LQL transpiler
dotnet tool install -g LqlCli.SQLite

# Install VS Code extension
code --install-extension lql-lang
```

#### Build from Source:
```bash
# Clone the repository
git clone https://github.com/MelbourneDeveloper/DataProvider.git
cd DataProvider

# Build the solution
dotnet build DataProvider.sln

# Run tests
dotnet test

# Format code
dotnet csharpier .
```

## Repository Structure

```
DataProvider/
├── DataProvider/              # Core DataProvider projects
│   ├── DataProvider/          # Core library and source generators
│   ├── DataProvider.SQLite/   # SQLite-specific implementation
│   ├── DataProvider.SqlServer/# SQL Server-specific implementation
│   └── DataProvider.Example/  # Example usage and patterns
├── Lql/                       # Lambda Query Language projects
│   ├── Lql/                   # Core LQL parser and transpiler
│   ├── Lql.SQLite/            # SQLite dialect support
│   ├── Lql.SqlServer/         # SQL Server dialect support
│   ├── Lql.Postgres/          # PostgreSQL dialect support
│   ├── Lql.Browser/           # Browser-based playground
│   ├── LqlCli.SQLite/         # Command-line transpiler
│   └── LqlExtension/          # VS Code language extension
├── Other/
│   ├── Results/               # Functional Result<T> type implementation
│   └── Selecta/               # SQL parsing and AST utilities
└── Tests/                     # Comprehensive test suites
    ├── DataProvider.Tests/
    └── Lql.Tests/
```

## Performance

Both DataProvider and LQL are designed for maximum performance:
- **Zero runtime overhead**: Generated code is pure ADO.NET
- **AOT compatible**: Full ahead-of-time compilation support
- **No reflection**: All code is generated at compile time
- **Minimal allocations**: Optimized for low memory usage

## Roadmap

- [ ] PostgreSQL tests for DataProvider
- [ ] Advanced LQL features (window functions, CTEs)
- [ ] Visual Studio extension for LQL
- [ ] Migration tooling
- [ ] Sync framework
- [ ] Business logic at the database level with LQL

## Contributing

Contributions are welcome! Please:
1. Read the [CLAUDE.md](CLAUDE.md) file for code style guidelines
2. Ensure all tests pass
3. Format code with `dotnet csharpier .`
4. Submit pull requests to the `main` branch