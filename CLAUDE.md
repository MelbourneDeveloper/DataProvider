# CLAUDE.md

## Multi-Agent (Too Many Cooks)
- Keep your key! Don't lose it!
- Check messages, lock files before editing, unlock after
- Don't edit locked files; telegraph intent via plans/messages
- Coordinator: delegate. Worker: ask for tasks. Update plans constantly.

## Coding Rules

- **NEVER THROW** - Return `Result<T>`. Wrap failures in try/catch
- **No casting/!** - Pattern match on type only
- **NO GIT** - Source control is illegal
- **No suppressing warnings** - Illegal
- **No raw SQL inserts/updates** - Use generated extensions
- **Use DataProvider Migrations to spin up DBs** - ⛔️ SQL for creating db schema = ILLEGAL (schema.sql = ILLEGAL).  Use the Migration.CLI with YAML. This is the ONLY valid tool to migrate dbs unless the app itself spins up the migrations in code.
- **NO CLASSES** - Records + static methods (FP style)
- **Copious ILogger** - Especially sync projects
- **NO INTERFACES** - Use `Action<T>`/`Func<T>`
- **Expressions over assignments**
- **Routinely format with csharpier** - `dotnet csharpier .` <- In root folder
- **Named parameters** - No ordinal calls
- **Close type hierarchies** - Private constructors:
```csharp
public abstract partial record Result<TSuccess, TFailure> { private Result() { } }
```
- **Extension methods on IDbConnection/IDbTransaction only**
- **Pattern match, don't if** - Switch expressions on type
- **No skipping tests** - Failing = OK, Skip = illegal
- **E2E tests only** - No mocks, integration testing
- **Type aliases for Results** - `using XResult = Result<X, XError>`
- **Immutable** - Records, `ImmutableList`, `FrozenSet`, `ImmutableArray`
- **NO REGEX** - ANTLR or SqlParserCS
- **XMLDOC on public members** - Except tests
- **< 450 LOC per file**
- **No commented code** - Delete it
- **No placeholders** - Leave compile errors with TODO

## Testing
- E2E with zero mocking
- 100% coverage, Stryker score 70%+
- Medical data: [FHIR spec](https://build.fhir.org/resourcelist.html)

## Architecture

| Component | Path | Purpose |
|-----------|------|---------|
| DataProvider | `DataProvider/` | Source gen for SQL -> extension methods |
| LQL | `Lql/` | Lambda Query Language -> SQL transpiler |
| Sync | `Sync/` | Offline-first bidirectional sync |
| Gatekeeper | `Gatekeeper/` | WebAuthn + RBAC auth |
| Samples | `Samples/` | Clinical, Scheduling, Dashboard |

## Config
- .NET 9.0, C# latest, nullable, warnings as errors
- Central config in `Directory.Build.props`
- Format: `dotnet csharpier .`
