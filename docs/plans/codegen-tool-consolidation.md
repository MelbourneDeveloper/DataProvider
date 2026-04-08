# PLAN: Consolidate DataProvider Codegen into a Single `DataProvider` Tool

> Implements [`../specs/codegen-source-generator.md`](../specs/codegen-source-generator.md).
> Supersedes the per-platform CLI projects (`Nimblesite.DataProvider.Postgres.Cli`, `Nimblesite.DataProvider.SQLite.Cli`).
> Migration tool (`DataProviderMigrate`) is **not** in scope and stays as-is per [CON-MIG-FIRST].

## Goal

Ship **one** `dotnet tool` package — `Nimblesite.DataProvider` — that performs codegen for **every** supported database platform (Postgres + SQLite to start, SqlServer + others later). Consumers add **one** `PackageReference` and get auto-invoked codegen via an MSBuild target shipped in the package's `build/` folder. No `.config/dotnet-tools.json`. No hand-rolled `<Target>` blocks. No `Generated/` folder gymnastics.

## Why

Current state: two separate CLI projects (`Postgres.Cli`, `SQLite.Cli`), each ~50–100 LOC of `System.CommandLine` plumbing around library calls. Consumers must wire dotnet-tool manifests, `<Exec>` blocks, and `Generated/` folders by hand. Versions drift between CLI and library packages. The full failure log lives in [`../specs/PROBLEM-cli-vs-library.md`](../specs/PROBLEM-cli-vs-library.md).

The spec resolves this by collapsing every platform CLI into one `DataProvider` tool whose entry point dispatches on `--platform` (or auto-detects from the connection string). Auto-invocation moves out of consumer csprojs and into the package's `build/Nimblesite.DataProvider.props` + `build/Nimblesite.DataProvider.targets`.

## Architecture (post-change)

```
Nimblesite.DataProvider (nupkg)
├── tools/net9.0/any/
│   ├── DataProvider.dll                           # tool entry point
│   ├── Nimblesite.DataProvider.Core.dll           # shared codegen
│   ├── Nimblesite.DataProvider.Postgres.dll       # platform impl
│   ├── Nimblesite.DataProvider.SQLite.dll         # platform impl
│   ├── Nimblesite.Lql.Core.dll
│   ├── Nimblesite.Lql.Postgres.dll
│   ├── Nimblesite.Lql.SQLite.dll
│   ├── Npgsql.dll                                 # bundled driver
│   ├── Microsoft.Data.Sqlite.dll                  # bundled driver
│   └── DotnetToolSettings.xml
├── build/
│   ├── Nimblesite.DataProvider.props              # AdditionalFiles, properties
│   └── Nimblesite.DataProvider.targets            # auto-invoke <Exec>
└── lib/netstandard2.0/
    └── _._                                        # placeholder, no runtime API
```

The tool is invoked once per build, with `--platform` resolved from `<DataProviderPlatform>` or sniffed from the connection string. The output directory is `obj/DataProviderGenerated/` and the generated files are added to `@(Compile)` by the same target.

## Files to Create

| Path | Purpose |
|---|---|
| `DataProvider/Nimblesite.DataProvider/Nimblesite.DataProvider.csproj` | Unified tool project. `PackAsTool=true`, `ToolCommandName=dataprovider`. References every platform impl. |
| `DataProvider/Nimblesite.DataProvider/Program.cs` | `Main` — `System.CommandLine` root with `--connection`, `--config`, `--output`, `--platform`. Dispatches into the platform-specific codegen library. ≤20 LOC per function, ≤450 LOC total. |
| `DataProvider/Nimblesite.DataProvider/build/Nimblesite.DataProvider.props` | `AdditionalFiles` for `*.lql`, `*.sql`, `DataProvider.json`. Declares default `<DataProviderOutputPath>$(IntermediateOutputPath)DataProviderGenerated</DataProviderOutputPath>`. |
| `DataProvider/Nimblesite.DataProvider/build/Nimblesite.DataProvider.targets` | `<Target Name="DataProviderCodegen" BeforeTargets="BeforeCompile">` — `<Exec>` invokes the bundled tool, then `<ItemGroup><Compile Include="$(DataProviderOutputPath)/**/*.g.cs" /></ItemGroup>`. |
| `DataProvider/Nimblesite.DataProvider.Tests/Nimblesite.DataProvider.Tests.csproj` | Integration tests against real Postgres + SQLite testcontainers. One test per `DPT001`–`DPT007`. |
| `DataProvider/Nimblesite.DataProvider.Postgres/Nimblesite.DataProvider.Postgres.csproj` | New Postgres platform library, **mirrors** `Nimblesite.DataProvider.SQLite` project layout. References `Antlr4.Runtime.Standard 4.13.1`, `Npgsql 9.0.2`, `Nimblesite.DataProvider.Core`, `Nimblesite.Sql.Model`, `Nimblesite.DataProvider.Migration.Core`. Implements [CON-PARSER-ONLY] + [CON-SHARED-CORE] + [DEPS-ANTLR] from the spec. |
| `DataProvider/Nimblesite.DataProvider.Postgres/Parsing/PostgreSQLLexer.g4` | Vendored verbatim from [`antlr/grammars-v4/sql/postgresql/PostgreSQLLexer.g4`](https://github.com/antlr/grammars-v4/blob/master/sql/postgresql/PostgreSQLLexer.g4). |
| `DataProvider/Nimblesite.DataProvider.Postgres/Parsing/PostgreSQLParser.g4` | Vendored verbatim from [`antlr/grammars-v4/sql/postgresql/PostgreSQLParser.g4`](https://github.com/antlr/grammars-v4/blob/master/sql/postgresql/PostgreSQLParser.g4). |
| `DataProvider/Nimblesite.DataProvider.Postgres/Parsing/PostgreSQLLexerBase.cs` | Vendored from `antlr/grammars-v4/sql/postgresql/CSharp/`, namespaced to `Nimblesite.DataProvider.Postgres.Parsing`. |
| `DataProvider/Nimblesite.DataProvider.Postgres/Parsing/PostgreSQLParserBase.cs` | Vendored from `antlr/grammars-v4/sql/postgresql/CSharp/`, namespaced to `Nimblesite.DataProvider.Postgres.Parsing`. |
| `DataProvider/Nimblesite.DataProvider.Postgres/Parsing/PostgreSQL{Lexer,Parser,ParserListener,ParserBaseListener,ParserVisitor,ParserBaseVisitor}.cs` | `antlr4`-generated, checked in. ~5 MB across 6 files. Per [PARSER-REGEN]. |
| `DataProvider/Nimblesite.DataProvider.Postgres/Parsing/PostgresAntlrParser.cs` | Thin facade implementing `ISqlParser`. ≤ 100 LOC. **Mirrors** `SqliteAntlrParser.cs` exactly. |
| `DataProvider/Nimblesite.DataProvider.Postgres/CodeGeneration/PostgresDatabaseEffects.cs` | Thin shell that hands a `NpgsqlConnection` factory + a Postgres type-mapper to the new Core `AdoNetDatabaseEffects`. ≤ 50 LOC. |
| `DataProvider/Nimblesite.DataProvider.Postgres/PostgresCodeGenerator.cs` | Thin shell that constructs a `CodeGenerationConfig` for Postgres and delegates to the new Core `SqlAntlrCodeGenerator`. ≤ 100 LOC. |
| `DataProvider/Nimblesite.DataProvider.Postgres.Tests/Nimblesite.DataProvider.Postgres.Tests.csproj` | xUnit test project. **Heavy** parser test coverage: simple SELECT, joined SELECT, CTEs, ILIKE, casts, RETURNING from INSERT/UPDATE/DELETE, `@param` extraction, `$N` extraction, projection alias preservation, qualified columns, function calls, GROUP BY, ORDER BY, subqueries, window functions, type casts. Codegen E2E tests against a real Postgres testcontainer. |
| `DataProvider/Nimblesite.DataProvider.Core/Parsing/AntlrSqlParameterExtractor.cs` | **Lifted** from `Nimblesite.DataProvider.SQLite/Parsing/SqliteParameterExtractor.cs`. Generic `IParseTree` walker that extracts `?` / `:name` / `@name` / `$name` parameter terminals. Both SQLite and Postgres call into it. The SQLite copy gets deleted. Implements [CON-SHARED-CORE]. |
| `DataProvider/Nimblesite.DataProvider.Core/CodeGeneration/DummyParameterValues.cs` | **Lifted** from `Nimblesite.DataProvider.SQLite/CodeGeneration/SqliteDatabaseEffects.GetDummyValueForParameter`. Name-based dummy resolver. Both SQLite and Postgres call into it. Implements [CON-SHARED-CORE]. |
| `DataProvider/Nimblesite.DataProvider.Core/CodeGeneration/AdoNetDatabaseEffects.cs` | **Lifted** from `SqliteDatabaseEffects`. Generic `IDatabaseEffects` implementation parameterised on a `Func<string, DbConnection>` connection factory + a `Func<Type, string, bool, string>` type mapper. SQLite + Postgres each construct one with their dialect's factory + mapper. The SQLite-specific version becomes a 10-line shell. Implements [CON-SHARED-CORE]. |
| `DataProvider/Nimblesite.DataProvider.Core/CodeGeneration/SqlAntlrCodeGenerator.cs` | **Lifted** from `SqliteCodeGenerator.GenerateCodeWithMetadata`. Generic codegen entry point taking `IDatabaseEffects` + a parser facade. `SqliteCodeGenerator` and `PostgresCodeGenerator` both reduce to ~30-line shells around it. Implements [CON-SHARED-CORE]. |

## Files to Modify

| Path | Change |
|---|---|
| `DataProvider/Nimblesite.DataProvider.Postgres.Cli/Nimblesite.DataProvider.Postgres.Cli.csproj` | Strip `<PackAsTool>`, `<ToolCommandName>`, `<IsPackable>`. Project becomes a private regression-test harness only. Mark `[Obsolete]` in `Program.cs`. |
| `DataProvider/Nimblesite.DataProvider.SQLite.Cli/Nimblesite.DataProvider.SQLite.Cli.csproj` | Same — strip tool packaging, mark obsolete. |
| `DataProvider/Nimblesite.DataProvider.Core/` | Extract per-platform codegen entry points into pure static methods returning `Result<GeneratedSource, CodegenError>`. No `Console`, no `Environment.Exit`, no `File.WriteAllText`. Per [DEPS-EXTRACT] in the spec. |
| `Directory.Build.props` | (a) Bump packages once `Nimblesite.DataProvider` is ready. (b) Remove `SqlParserCS` per [DEPS-SQLPARSER]. (c) De-conditionalise `Outcome` reference per [DEPS-OUTCOME]. |
| `docs/plans/RELEASE-PLAN.md` | Add `Nimblesite.DataProvider` to the CLI tools table. Remove the obsolete CLI rows. Add a `dotnet pack` line for it in the workflow snippet. |
| `.github/workflows/release.yml` | (Created in [`RELEASE-PLAN.md`](./RELEASE-PLAN.md).) Add the new tool's pack step. Drop the per-platform CLIs. |
| `DataProvider/DataProvider/Program.cs` | **Currently 2358 lines.** Slim to ≤ 50 lines: `Main` builds `RootCommand` containing `SqliteCli.BuildCommand()` + `PostgresCli.BuildCommand()` and dispatches. **Every hand-rolled SQL string-walker gets DELETED:** `QuoteAsAliases`, `HasUppercaseAscii`, `ExtractParameters`, `ParseSelectColumns`, `ParseColumnDefinition`, `InferTypeFromExpression`, `InferColumnTypesFromSql`, plus the regex calls in `InferColumnTypesFromSql`/`ParseColumnDefinition` that violate `CLAUDE.md`'s `NO REGEX` rule. None survive. The Postgres logic moves into `Nimblesite.DataProvider.Postgres` and `DataProvider/PostgresCli.cs` per [CON-PARSER-ONLY] + [CON-SHARED-CORE]. |
| `DataProvider/DataProvider/PostgresCli.cs` | **New file**. Mirrors `SqliteProgram.cs`/`SqliteCli`. Pure CLI host: option binding, file-loop driver, calls into `PostgresCodeGenerator.GenerateCodeWithMetadata`. **Zero parsing logic. Zero codegen logic. Zero string scanning.** ≤ 350 LOC. |
| `DataProvider/Nimblesite.DataProvider.SQLite/Parsing/SqliteParameterExtractor.cs` | **DELETED** (lifted to `Nimblesite.DataProvider.Core/Parsing/AntlrSqlParameterExtractor.cs`, called from both SQLite and Postgres). |
| `DataProvider/Nimblesite.DataProvider.SQLite/Parsing/SqliteAntlrParser.cs` | Updated to call `Nimblesite.DataProvider.Core.Parsing.AntlrSqlParameterExtractor.ExtractParameters(parseTree)` instead of the local extractor. |
| `DataProvider/Nimblesite.DataProvider.SQLite/CodeGeneration/SqliteDatabaseEffects.cs` | **Refactored to a thin shell.** Currently 184 LOC implementing connection-open + dummy-parameter-bind + reader-loop. After refactor: ≤ 30 LOC that constructs `Core.AdoNetDatabaseEffects` with a `SqliteConnection` factory + the SQLite type mapper. The reader loop, the dummy-value resolver, and the boilerplate try/catch all move to Core. |
| `DataProvider/Nimblesite.DataProvider.SQLite/SqliteCodeGenerator.cs` | **Refactored to a thin shell.** Currently 876 LOC. After refactor: ≤ 60 LOC that constructs the SQLite `CodeGenerationConfig` and delegates to `Core.SqlAntlrCodeGenerator`. The 800+ lines of orchestration move to Core. SQLite tests must continue to pass. |
| `DataProvider/DataProvider.sln` | Add `Nimblesite.DataProvider.Postgres` and `Nimblesite.DataProvider.Postgres.Tests` projects to the solution. |
| `DataProvider/DataProvider/DataProvider.csproj` | Add `<ProjectReference>` to `Nimblesite.DataProvider.Postgres`. The CLI consumes the new library instead of having its own copy. |

## Files to Delete (eventually)

| Path | When |
|---|---|
| `DataProvider/Nimblesite.DataProvider.Postgres.Cli/` | After consumers (HealthcareSamples, ai_cms) cut over to the unified tool **and** at least one release ships with both options. |
| `DataProvider/Nimblesite.DataProvider.SQLite.Cli/` | Same. |
| `Lql/Nimblesite.Lql.Cli.Postgres/` | Same — superseded by the unified tool's LQL pipeline. |
| `Lql/Nimblesite.Lql.Cli.SQLite/` | Same. |

The CLI test suites (`Nimblesite.Lql.Cli.SQLite.Tests`, etc.) stay alive as behaviour-equivalence harnesses pointed at the extracted Core library per [DEPS-EXTRACT], not at the obsolete CLIs.

## Consumer Impact

**Before** (the world `PROBLEM-cli-vs-library.md` describes):

```xml
<!-- consumer.csproj -->
<ItemGroup>
  <PackageReference Include="Nimblesite.DataProvider.Core" Version="..." />
  <PackageReference Include="Nimblesite.Lql.Postgres"      Version="..." />
</ItemGroup>

<!-- .config/dotnet-tools.json -->
{
  "tools": {
    "nimblesite.dataprovider.postgres.cli": { ... },
    "nimblesite.lql.cli.postgres":          { ... }
  }
}

<!-- consumer.csproj (continued) -->
<Target Name="GenerateDataProvider" BeforeTargets="BeforeCompile">
  <RemoveDir .../>
  <MakeDir   .../>
  <Exec Command="dotnet lql-postgres ..." />
  <Exec Command="dotnet dataprovider-postgres ..." />
  <Touch .../>
  <ItemGroup>
    <Compile Include="Generated/**/*.g.cs" />
  </ItemGroup>
</Target>
```

**After**:

```xml
<!-- consumer.csproj -->
<ItemGroup>
  <PackageReference Include="Nimblesite.DataProvider" Version="..." />
</ItemGroup>

<PropertyGroup>
  <DataProviderConnectionString>$(DATAPROVIDER_CONN)</DataProviderConnectionString>
  <DataProviderPlatform>Postgres</DataProviderPlatform> <!-- optional -->
</PropertyGroup>
```

That is the entire consumer-side delta. No tool manifest. No `<Target>`. No `Generated/`.

## Sequencing

1. **DEPS housekeeping** (preconditions for the extract step).
   - Delete `SqlParserCS` from `Directory.Build.props` per [DEPS-SQLPARSER].
   - Confirm `Outcome` ns2.0/ns2.1 multi-target is republished, bump version, drop the conditional reference per [DEPS-OUTCOME].
2. **Extract codegen into Core** per [DEPS-EXTRACT].
   - Move every non-`Main` static helper out of `Nimblesite.DataProvider.Postgres.Cli/Program.cs` and `Nimblesite.DataProvider.SQLite.Cli/Program.cs` into `Nimblesite.DataProvider.Core` (Postgres + SQLite folders).
   - One type per file, ≤450 LOC, ≤20 LOC per function. No `Console.WriteLine`, no `Environment.Exit`, no `File.WriteAllText`, no `System.CommandLine` in extracted code.
   - Existing CLI tests must still pass against the now-thin shims.
3. **Create unified tool project** `Nimblesite.DataProvider`.
   - `PackAsTool=true`, `ToolCommandName=dataprovider`, `TargetFramework=net9.0` (or `net10.0` to match the rest of the repo — pick one and stick with it).
   - `Program.cs` is a `System.CommandLine` root that parses args, picks the platform implementation from Core, runs codegen, writes the result to `--output`, exits 0 / non-zero with structured stderr per the spec's diagnostic table.
4. **Author `build/` props + targets**.
   - `.props` declares `AdditionalFiles` and the default `<DataProviderOutputPath>`.
   - `.targets` runs `<Exec>` against `$(DataProviderToolPath)` (resolved by NuGet at restore time), then `<ItemGroup><Compile Include="$(DataProviderOutputPath)/**/*.g.cs" /></ItemGroup>`.
5. **Tests** per [TEST-UNIT], [TEST-DIAG], [TEST-INC] in the spec.
   - One integration test per platform against a real testcontainer. **No in-memory DBs** per `CLAUDE.md`.
   - One test per `DPT001`–`DPT007`.
   - Incremental test: drive the tool twice with identical inputs, assert byte-for-byte identical output.
6. **Mark legacy CLI projects `[Obsolete]`** per [MIGRATE-CLI-FATE].
   - Strip `PackAsTool` so they stop publishing to nuget.org as tools.
   - Their test suites continue to run as Core regression harnesses.
7. **Update `RELEASE-PLAN.md` + the release workflow** to pack `Nimblesite.DataProvider` and stop packing the legacy per-platform CLIs.
8. **Cut over consumers** (HealthcareSamples, ai_cms) in their own PRs after the new tool ships to nuget.org. Not in scope for this plan.
9. **Delete legacy CLI projects** after one release cycle of overlap.

## Risks

| ID | Risk | Mitigation |
|---|---|---|
| RISK-DRIVER-COLLISION | Bundling `Npgsql` and `Microsoft.Data.Sqlite` (and eventually `Microsoft.Data.SqlClient`) in one tool means the tool's `tools/net9.0/any/` folder pulls in every transitive dep of every driver. Some may collide. | Run a `dotnet pack` + `dotnet tool install` smoke test on every CI build of the tool. Fail loud on `FileLoadException`/`TypeLoadException`. |
| RISK-PLATFORM-AUTODETECT | Auto-detecting platform from connection string is fragile (e.g. `Host=foo;...` vs `Data Source=foo.db`). | `--platform` is **required** when auto-detect is ambiguous. Default to required for v0; relax later only if a clean detector exists. |
| RISK-NATIVE-DEPS | SQLite ships native `e_sqlite3` libs per RID. SqlServer ships `sni.dll`. The tool must include the right RIDs. | Use `RuntimeIdentifiers` in the tool csproj covering `win-x64`, `linux-x64`, `osx-x64`, `osx-arm64`, `linux-arm64`. CI runs on each. |
| RISK-CON-MIG-FIRST | Tool runs at build time. If migrations didn't run first, introspection sees a stale schema. | [CON-MIG-FIRST] is consumer-side. Document loudly. The tool itself has no recourse — it reads what the DB says. |

## Open Questions

- **Tool TFM**: `net9.0` or `net10.0`? Migration tool currently targets one of these — match it for consistency.
- **Platform detection**: required parameter (simple, loud) vs sniff-from-connstring (convenient, fragile)? Default to **required** for v0.
- **Output path default**: `obj/DataProviderGenerated/` or `$(IntermediateOutputPath)DataProviderGenerated`? Pick the latter — works inside out-of-tree builds.

---

## TODO

- [ ] **DEPS** — Delete `SqlParserCS` `<PackageReference>` from `Directory.Build.props` per [DEPS-SQLPARSER].
- [ ] **DEPS** — Verify `Outcome` ns2.0/ns2.1 multi-target is on nuget.org; bump version + drop conditional in `Directory.Build.props` per [DEPS-OUTCOME].
- [ ] **EXTRACT** — Move codegen helpers from `Nimblesite.DataProvider.Postgres.Cli/Program.cs` into `Nimblesite.DataProvider.Core` (Postgres folder). One type per file, ≤450 LOC, ≤20 LOC per function. Returns `Result<GeneratedSource, CodegenError>`. No `Console`/`Environment.Exit`/`File.WriteAllText`/`System.CommandLine`.
- [ ] **EXTRACT** — Same for `Nimblesite.DataProvider.SQLite.Cli/Program.cs` → `Nimblesite.DataProvider.Core` (SQLite folder).
- [ ] **EXTRACT** — Run the existing CLI test suites against the now-thin shims; both must stay green as a regression gate.
- [ ] **TOOL** — Create `DataProvider/Nimblesite.DataProvider/Nimblesite.DataProvider.csproj`: `PackAsTool=true`, `ToolCommandName=dataprovider`, references Core + every platform impl + bundled drivers.
- [ ] **TOOL** — Write `Program.cs`: `System.CommandLine` root, parses `--connection`, `--config`, `--output`, `--platform`, dispatches into Core, writes structured stderr on failure, exits 0/non-zero per spec.
- [ ] **TOOL** — Add `RuntimeIdentifiers` for `win-x64;linux-x64;osx-x64;osx-arm64;linux-arm64` so SQLite native libs ship.
- [ ] **PROPS** — Author `build/Nimblesite.DataProvider.props`: `<AdditionalFiles>` for `*.lql`, `*.sql`, `DataProvider.json`; default `<DataProviderOutputPath>$(IntermediateOutputPath)DataProviderGenerated</DataProviderOutputPath>`.
- [ ] **TARGETS** — Author `build/Nimblesite.DataProvider.targets`: `<Target Name="DataProviderCodegen" BeforeTargets="BeforeCompile">` runs `<Exec>` against `$(DataProviderToolPath)`, then adds `Compile` items for every emitted `.g.cs`.
- [ ] **TESTS** — Create `Nimblesite.DataProvider.Tests` with one integration test per platform against a real testcontainer.
- [ ] **TESTS** — One test per `DPT001`–`DPT007` per [TEST-DIAG].
- [ ] **TESTS** — Incremental test per [TEST-INC]: identical inputs → byte-for-byte identical output.
- [ ] **TESTS** — Smoke-test `dotnet pack` + `dotnet tool install` on every CI run; fail on `FileLoadException`/`TypeLoadException`.
- [ ] **OBSOLETE** — Strip `PackAsTool` from `Nimblesite.DataProvider.Postgres.Cli.csproj` and `Nimblesite.DataProvider.SQLite.Cli.csproj`. Add `[Obsolete]` to their `Program` types.
- [ ] **OBSOLETE** — Same for `Nimblesite.Lql.Cli.Postgres` (if it exists) and `Nimblesite.Lql.Cli.SQLite`.
- [ ] **RELEASE** — Update [`RELEASE-PLAN.md`](./RELEASE-PLAN.md): add `Nimblesite.DataProvider` to the CLI tools table; drop legacy per-platform CLIs.
- [ ] **RELEASE** — Update `.github/workflows/release.yml`: add `dotnet pack DataProvider/Nimblesite.DataProvider/...`; remove the legacy CLI pack steps.
- [ ] **DOCS** — Update `Nimblesite.DataProvider.Postgres.Cli/README.md` and `.SQLite.Cli/README.md` with `[Obsolete]` notice pointing at the unified tool.
- [ ] **CONSUMER** — (Not in this repo) Cut HealthcareSamples + ai_cms over to `Nimblesite.DataProvider` in separate PRs after first release.
- [ ] **CLEANUP** — After one release cycle of overlap, delete legacy CLI projects entirely.
- [ ] **OPEN** — Decide tool TFM (`net9.0` vs `net10.0`) and lock it.
- [ ] **OPEN** — Decide whether `--platform` is required or sniffed from connection string. Default: **required**.
