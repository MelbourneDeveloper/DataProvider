# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Multi-Agent Coordination (Too Many Cooks)
- Keep your key! It's critical. Do not lose it!
- Check messages regularly, lock files before editing, unlock after
- Don't edit locked files; signal intent via plans and messages
- Coordinator: keep delegating via messages. Worker: keep asking for tasks via messages
- Clean up expired locks routinely
- Do not use Git unless asked by user

## Build Commands
```bash
dotnet build DataProvider.sln          # Build entire solution
dotnet test                            # Run all tests
dotnet test --filter "FullyQualifiedName~ClassName"  # Run specific test class
dotnet test --filter "FullyQualifiedName~MethodName" # Run single test
dotnet csharpier .                     # Format all code (run periodically)
```

## Architecture Overview

This repository contains two complementary projects:

**DataProvider** - Source generator creating compile-time safe extension methods from SQL files
- Core library in `DataProvider/DataProvider/` - base types, config records, code generation
- Database-specific implementations: `DataProvider.SQLite/`, `DataProvider.SqlServer/`
- Uses ANTLR grammars for SQL parsing (`Parsing/*.g4` files)
- Generates extension methods on `IDbConnection` and `IDbTransaction`
- Routinely format all C# code with `dotnet csharpier .`

**LQL (Lambda Query Language)** - Functional DSL that transpiles to SQL
- Core transpiler in `Lql/Lql/` - ANTLR grammar, pipeline steps, AST
- Database dialects: `Lql.SQLite/`, `Lql.SqlServer/`, `Lql.Postgres/`
- CLI tool: `LqlCli.SQLite/`
- Browser playground: `Lql.Browser/`

**Shared Libraries** in `Other/`:
- `Results/` - `Result<TValue, TError>` type for functional error handling
- `Selecta/` - SQL parsing and AST utilities

## Coding Rules (CRITICAL)

- **NEVER THROW EXCEPTIONS** - Always return `Result<T>` for fallible operations. Wrap anything that can fail in try/catch
- **NO CLASSES** - Use records and static methods. FP style with pure static methods
- **NO INTERFACES** - Use `Action<T>` or `Func<T>` for abstractions
- **AVOID ASSIGNMENTS** - Use expressions where possible
- **Static extension methods on IDbConnection and IDbTransaction only** - No classes for data access
- **Test at the highest level** - Avoid mocks. Only full integration testing
- **No singletons** - Inject `Func` into static methods
- **NO REGEX** - Parse SQL with ANTLR .g4 grammars or SqlParserCS library
- **All public members require XMLDOC** - Except in test projects
- **Keep files under 450 LOC**
- **One type per file** (except small records)
- **No commented-out code** - Delete it
- **No consecutive Console.WriteLine** - Use single string interpolation
- **No placeholders** - If incomplete, leave LOUD compilation error with TODO
- **Never use Fluent Assertions**

## Project Configuration

- .NET 9.0, C# latest with nullable enabled
- All warnings as errors (TreatWarningsAsErrors=true)
- Central config in `Directory.Build.props` - don't duplicate in .csproj files
- xUnit for testing with Moq

## Code Generation Note

This is a code generation project. Don't generate code manually that is the responsibility of the generator. Check for existing types/methods before creating new ones.
