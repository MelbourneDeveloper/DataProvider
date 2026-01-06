# CLAUDE.md

## Multi-Agent (Too Many Cooks)
- Keep your key! Don't lose it!
- Check messages, lock files before editing, unlock after
- Don't edit locked files; telegraph intent via plans/messages
- Coordinator: delegate. Worker: ask for tasks. Update plans constantly.

## Coding Rules

- **NEVER THROW** - Return `Result<T,E>``. Wrap failures in try/catch
- **No casting/!** - Pattern match on type only
- **NO GIT** - Source control is illegal
- **No suppressing warnings** - Illegal
- **No raw SQL inserts/updates** - Use generated extensions
- **Use DataProvider Migrations to spin up DBs** - ⛔️ SQL for creating db schema = ILLEGAL (schema.sql = ILLEGAL).  Use the Migration.CLI with YAML. This is the ONLY valid tool to migrate dbs unless the app itself spins up the migrations in code.
- **NO CLASSES** - Records + static methods (FP style)
- **PRIVATE/INTERNAL BY DEFAULT** - Don't expose types/members that users don't need
- **Copious ILogger** - Especially sync projects
- **NO INTERFACES** - Use `Action<T>`/`Func<T>`
- **Expressions over assignments**
- **Routinely format with csharpier** - `dotnet csharpier .` <- In root folder
- **Named parameters** - No ordinal calls
- **Close type hierarchies** - Private constructors:
```csharp
public abstract partial record Result<TSuccess, TFailure> { private Result() { } }
```
- **Skipping tests = ⛔️ ILLEGAL** - Failing tests = OK. Aggressively unskip tests
- **Test at the highest level** - Avoid mocks. Only full integration testing
- **Keep files under 450 LOC and functions under 20 LOC**
- **Always use type aliases (using) for result types** - Don't write like this: `new Result<string, SqlError>.Ok`
- **All tables must have a SINGLE primary key**
- **Primary keys MUST be UUIDs**
- **No singletons** - Inject `Func` into static methods
- **Immutable types!** - Use records. Don't use `List<T>`. Use `ImmutableList` `FrozenSet` or `ImmutableArray`
- **No in-memory dbs** - Real dbs all the way
- **NO REGEX** - Parse SQL with ANTLR .g4 grammars or SqlParserCS library
- **All public members require XMLDOC** - Except in test projects
- **One type per file** (except small records)
- **No commented-out code** - Delete it
- **No consecutive Console.WriteLine** - Use single string interpolation
- **No placeholders** - If incomplete, leave LOUD compilation error with TODO
- **Never use Fluent Assertions**

## CSS
- **MINIMAL CSS** - Do not duplicate CSS clases
- **Name classes after component, NOT section** - Sections should not have their own CSS classes

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
