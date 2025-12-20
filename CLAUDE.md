# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

# Rules

## Multi-Agent Coordination (Too Many Cooks)
- Keep your key! It's critical. Do not lose it!
- Check messages regularly, lock files before editing, unlock after
- Don't edit locked files; signal intent via plans and messages
- Coordinator: keep delegating via messages. Worker: keep asking for tasks via messages
- Telegraph EVERYTHING with messages and plan updates
- Clean up expired locks routinely

## Coding Rules

- **NEVER THROW EXCEPTIONS** - Always return `Result<T>` for fallible operations. Wrap anything that can fail in try/catch
- **No casting or using ! for nulls** - Only pattern matching on type
- **DO NOT USE GIT** <-- ⛔️ Source control is illegal for you 🙅🏼
- **Do not supress analyzer warnings/errors** <-- Illegal
- **NO CLASSES** - Use records and static methods. FP style with pure static methods
- **Copious logging with ILogger** - Especially in the sync projects
- **NO INTERFACES** - Use `Action<T>` or `Func<T>` for abstractions
- **AVOID ASSIGNMENTS** - Use expressions where possible
- **You MUST close type hierarchies** - Make the constructor restricted so it is not possible to create new implementations from the base type. Eg:
```csharp
public abstract partial record Result<TSuccess, TFailure>
{
    // [...]
    /// </summary>
    private Result() { }
    // [...]
}
``
- **Static extension methods on IDbConnection and IDbTransaction only** - No classes for data access
- **Don't use statements like if** - use pattern matching switch expressions on type
⛔️ wrong
```csharp
if (triggerResult is Result<bool, SyncError>.Error<bool, SyncError> triggerErr)
```
- **Skipping tests = ⛔️ ILLEGAL** - Failing tests = OK. Aggressively unskip tests
- **Test at the highest level** - Avoid mocks. Only full integration testing
- **Always use type aliases (using) for result types** - Don't write like this: `new Result<string, SqlError>.Ok`
- **No singletons** - Inject `Func` into static methods
- **Immutable types!** - Use records. Don't use `List<T>`. Use `ImmutableList` `FrozenSet` or `ImmutableArray`
- **NO REGEX** - Parse SQL with ANTLR .g4 grammars or SqlParserCS library
- **All public members require XMLDOC** - Except in test projects
- **Keep files under 450 LOC**
- **One type per file** (except small records)
- **No commented-out code** - Delete it
- **No consecutive Console.WriteLine** - Use single string interpolation
- **No placeholders** - If incomplete, leave LOUD compilation error with TODO
- **Never use Fluent Assertions**

## Testing
- Use e2e tests with zero mocking where possible
- Fall back on unit testing only when absolutely necessary
- Create MEANINGFUL tests that test REAL WORLD use cases
- All projects must have 100% test coverage and a Stryker Mutator testing score of 70% or above. Use [Stryker Mutator](https://stryker-mutator.io/docs/stryker-net/getting-started/) as the ultimate arbiter of test quality

## Architecture Overview

This repository contains multiple related, but distinct suites:

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

**Samples**
- Medical: All medical data MUST conform to the [FHIR spec](https://build.fhir.org/resourcelist.html).

## Project Configuration

- .NET 9.0, C# latest with nullable enabled
- All warnings as errors (TreatWarningsAsErrors=true)
- Central config in `Directory.Build.props` - don't duplicate in .csproj files
- xUnit for testing with Moq

## Code Generation Note

This is a code generation project. Don't generate code manually that is the responsibility of the generator. Check for existing types/methods before creating new ones.
