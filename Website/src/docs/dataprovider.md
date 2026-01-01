---
layout: layouts/docs.njk
title: "DataProvider SQL Parser"
---

# DataProvider SQL Parser

A .NET source generator project that aims to parse SQL files and generate strongly-typed extension methods for multiple SQL database platforms.

CRITICAL: The generator connects to the database at compile time to get query metadata. If it doesn't connect, the generation fails with a compiler error.

**⚠️ Project Status: Early Development**
This project is in active development. Many features described below are partially implemented or planned for future releases.

## Overview

- **Input**: `.sql` files and optional `.grouping.json` configuration files
- **Output**: Generated extension methods on database-specific connections
- **Returns**: `Result<T, SqlError>` instead of throwing exceptions
- **Platforms**: SQL Server, SQLite (with extensible architecture for other databases)

## Current Implementation Status

### ✅ What Works
- Basic SQL file processing and code generation
- Result type pattern for error handling
- Database-specific source generators (SQL Server, SQLite)
- Extension method generation for specific connection types
- Basic parameter extraction
- Grouping configuration support via JSON files
- Directory.Build.props with comprehensive Roslyn analyzers

### ⚠️ Partially Implemented
- **SQL Parsing**: Currently uses SqlParserCS library but falls back to string manipulation for parameter extraction instead of proper AST traversal
- **Code Generation**: Basic structure in place but many areas marked with TODO comments
- **Schema Inspection**: Framework exists but not fully integrated with code generation

### ❌ Known Issues & Limitations
- **Regex Usage**: The main `DataProviderSourceGenerator` violates project rules by using regex for parameter extraction
- **Extension Target**: Currently generates extensions on `SqlConnection`/`SqliteConnection` rather than `IDbConnection`/`ITransaction` as originally planned
- **JOIN Analysis**: Not currently extracting JOIN information despite SqlStatement structure supporting it
- **SELECT List Extraction**: Not extracting column information from SELECT statements
- **Hardcoded Logic**: Much code generation is specific to example files rather than generic

## Usage

1. Add `.sql` files to your project as AdditionalFiles
2. Optionally add corresponding `.grouping.json` files for parent-child relationship mapping
3. Build project → extension methods auto-generated
4. Use generated methods:

```csharp
// SQLite (currently working)
var result = await sqliteConnection.GetInvoicesAsync(customerName, startDate, endDate);
// Returns: Result<ImmutableList<Invoice>, SqlError> with InvoiceLines collection

// SQL Server (planned)
var result = await sqlConnection.GetInvoicesAsync(customerName, startDate, endDate);
```

## Architecture

### Core Components

- **SqlFileGeneratorBase**: Base source generator with database-specific implementations
- **SqlStatement**: Generic SQL statement representation (partially populated)
- **ISqlParser**: Abstraction for parsing SQL across different dialects
- **ICodeGenerator**: Abstraction for generating database-specific code
- **Result<T,E>**: Functional programming style error handling (✅ Complete)

### Current Generators

- **DataProvider.SourceGenerator**: Main generator (⚠️ Uses regex - violates project rules)
- **DataProvider.SqlServer**: SQL Server specific generator using SqlParserCS
- **DataProvider.SQLite**: SQLite specific generator using SqlParserCS

### SQL Parsing Status

- **Library**: Uses SqlParserCS (✅ Good choice, no regex in parsing)
- **Parameter Extraction**: ⚠️ Falls back to string manipulation instead of AST traversal
- **Query Type Detection**: ✅ Basic implementation
- **JOIN Analysis**: ❌ Infrastructure exists but not populated
- **SELECT List Extraction**: ❌ Not implemented
- **Error Handling**: ✅ Graceful parsing failure with error messages

## Dependencies

- **Microsoft.CodeAnalysis** (source generation)
- **SqlParserCS** (SQL parsing across multiple dialects)
- **Microsoft.Data.SqlClient** (SQL Server support)
- **Microsoft.Data.Sqlite** (SQLite support)
- **System.Text.Json** (configuration file parsing)

## Project Structure

```
DataProvider/                      # Core types and interfaces
DataProvider.Dependencies/         # Result types and error handling
DataProvider.SourceGenerator/      # ⚠️ Main generator (uses regex)
DataProvider.SqlServer/           # SQL Server source generator
DataProvider.SQLite/              # SQLite source generator
DataProvider.Example/             # Usage examples and test SQL files
DataProvider.Tests/               # Unit tests
DataProvider.Example.Tests/       # Integration tests
```

## Configuration Files

### SQL Files
Standard SQL files with parameterized queries example:
```sql
SELECT i.Id, i.InvoiceNumber, l.Description, l.Amount
FROM Invoice i
JOIN InvoiceLine l ON l.InvoiceId = i.Id
WHERE i.CustomerName = @customerName
    AND (@startDate IS NULL OR i.InvoiceDate >= @startDate)
```

### Grouping Configuration Example (Optional)
```json
{
    "QueryName": "GetInvoices",
    "GroupingStrategy": "ParentChild",
    "ParentEntity": {
        "Name": "Invoice",
        "KeyColumns": ["Id"],
        "Columns": ["Id", "InvoiceNumber", "CustomerName"]
    },
    "ChildEntity": {
        "Name": "InvoiceLine",
        "KeyColumns": ["LineId"],
        "ParentKeyColumns": ["InvoiceId"],
        "Columns": ["LineId", "Description", "Amount"]
    }
}
```

## Project Rules & Standards

- **FP Style**: Pure static methods over class methods
- **Result Types**: No exceptions, all operations return `Result<T, SqlError>`
- **Null Safety**: Comprehensive Roslyn analyzers with strict null checking
- **Code Quality**: All warnings treated as errors, extensive static analysis
- **No Regex**: ⚠️ Currently violated - needs refactoring to use proper AST parsing
- **One Type Per File**: Clean organization with proper namespacing
- **Immutable**: Records over classes, immutable collections

## Roadmap

### High Priority Fixes
1. **Remove Regex Usage**: Refactor parameter extraction to use SqlParserCS AST properly
2. **Complete SQL Parsing**: Extract SELECT lists, tables, and JOIN information
3. **Fix Extension Targets**: Generate extensions on `IDbConnection`/`ITransaction` interfaces
4. **Generic Code Generation**: Remove hardcoded logic for specific examples

### Future Enhancements
1. **Schema Integration**: Use database schema inspection for type generation
2. **Multiple Result Sets**: Support for stored procedures with multiple result sets
3. **Query Optimization**: Analysis and suggestions for query performance
4. **Additional Databases**: PostgreSQL, MySQL support

## Example

Given a SQL file `GetInvoices.sql`:

```sql
SELECT
    i.Id,
    i.InvoiceNumber,
    i.CustomerName,
    l.Description,
    l.Amount
FROM Invoice i
JOIN InvoiceLine l ON l.InvoiceId = i.Id
WHERE i.CustomerName = @customerName
```

The generator currently creates code like this. These are only examples. Don't put invoice specific code in the code generator:

```csharp
// ⚠️ Current implementation - needs improvement
public static async Task<Result<ImmutableList<Invoice>, SqlError>> GetInvoicesAsync(
    this SqliteConnection connection,
    string customerName)
{
    // Generated implementation with basic error handling
    // TODO: Improve type mapping and parameter handling
}

public record Invoice(
    int Id,
    string InvoiceNumber,
    string CustomerName,
    ImmutableList<InvoiceLine> InvoiceLines
);

public record InvoiceLine(
    string Description,
    decimal Amount
);
```

## Contributing

This project follows strict coding standards enforced by Roslyn analyzers. Key principles:

- All warnings treated as errors
- Comprehensive null safety analysis
- Functional programming patterns preferred
- Result types instead of exceptions
- No regex - use proper parsing libraries
- Extensive XML documentation required

See `Directory.Build.props` and `CodeAnalysis.ruleset` for complete rules.
