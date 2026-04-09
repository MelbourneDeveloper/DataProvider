# SPEC: DataProvider Codegen Tool

> **RIGID. NORMATIVE.** Resolves [PROBLEM-cli-vs-library.md](./PROBLEM-cli-vs-library.md). Database-agnostic. Consumer-agnostic.

## ⚠️ NAMING IS NON-NEGOTIABLE ⚠️

The tool, the package, the executable, and every internal lib scoped under the tool are **`DataProvider`** — bare, no `Nimblesite.` prefix, no `.Tool` / `.Cli` suffix. External libs the tool consumes (`Nimblesite.Lql.*`, `Nimblesite.Sql.Model`, `Outcome`) keep their existing names.

Renaming any `DataProvider.*` artifact to add a prefix or suffix is grounds for the renamer being purged from the data center. — repo owner.

## Overview

`DataProvider` is a console exe shipped **inside** the `DataProvider` NuGet package and invoked by an auto-imported `build/DataProvider.targets` file in the same package. At codegen time it opens a real driver connection, introspects the live schema, transpiles `.lql` → platform SQL, and emits typed C# data-access code into the consumer's `obj/` directory for `csc` to compile.

## CONSTRAINT

| ID | Constraint |
|---|---|
| CON-NOTOOLMANIFEST | Zero `.config/dotnet-tools.json`, zero `dotnet tool restore` in any consumer. The tool ships inside the package and runs from the auto-imported `.targets`. |
| CON-SIMPLE | One `PackageReference` + one MSBuild property per consumer csproj. No custom `<Target>`, no `<Compile Include="Generated/**">`, no `RemoveDir`/`MakeDir`/`Touch`, no `sed`. |
| CON-DBLIVE | Live driver connection + live schema introspection only. Static YAML/JSON schema parsing forbidden. Unreachable DB → loud diagnostic + build fail. |
| CON-NOFALLBACK | Zero fallbacks. Zero degraded modes. No offline cache. Build passes against a live DB or fails loud. |
| CON-MIG-FIRST | `dataprovider-migrate` runs against the live DB **before** every codegen invocation. Every flow (CI, local, IDE) runs migrations first. The migration tool stays. |
| CON-UNIVERSAL | One single mechanism for **every** consumer of **every** platform. No consumer-specific names, paths, or domain models. |
| CON-PLATFORM-AGNOSTIC | One single mechanism for **every** database platform. Identical contract. |
| CON-PROCESS-ISOLATION | Codegen runs in its own process, separate from `dotnet build` / MSBuild / IDE. Process isolation is the only way to load any TFM driver + any native dep on any host. |
| CON-PARSER-ONLY | Every platform parses input SQL with a **real parser** — its vendored ANTLR `.g4` grammar. Hand-rolled string scanning, regex against SQL tokens, char-by-char SQL walking, and any pseudo-lexer are ⛔️ ILLEGAL per `CLAUDE.md`. Postgres uses `antlr/grammars-v4/sql/postgresql` (derived from upstream `postgres/postgres/src/backend/parser/gram.y`). SQLite uses `antlr/grammars-v4/sql/sqlite`. |
| CON-SHARED-CORE | Per-platform libs are thin shells. Anything not strictly platform-specific lives in `DataProvider.Core`. The only platform-specific code allowed is: (a) vendored ANTLR `.g4` + generated parser sources, (b) `{Platform}AntlrParser` shell, (c) `{Platform}DatabaseEffects` shell (connection factory + SQL→C# type mapping), (d) `{Platform}CodeGenerator` shell that constructs the platform's `CodeGenerationConfig` for `Core.SqlAntlrCodeGenerator`. ≤ 5 author-written files per library, ≤ 200 LOC each excluding generated parser sources. **Duplication between platform libs is grounds for review rejection.** |

## PLATFORM

| Token | Database | Driver | Status |
|---|---|---|---|
| `Postgres` | PostgreSQL | `Npgsql` | day one |
| `SQLite` | SQLite | `Microsoft.Data.Sqlite` (`e_sqlite3` per RID) | day one |
| `SqlServer` | SQL Server | `Microsoft.Data.SqlClient` (`sni` per RID) | day one |

A platform qualifies iff its primary .NET driver loads inside the tool's net9.0 process. Adding a platform = one `{Platform}AntlrParser` + one `{Platform}DatabaseEffects` + one `{Platform}CodeGenerator` shell + one driver in the tool's deps + a CI run of [TEST-NATIVE]. **No consumer csproj changes.**

Platform selection is **always** explicit via `--platform` (passed by the auto-imported `.targets` file from `<DataProviderPlatform>`). No connection-string sniffing.

## PARSER

Per [CON-PARSER-ONLY], every platform parses with a vendored ANTLR grammar.

| Platform | Grammar source |
|---|---|
| `Postgres` | [`antlr/grammars-v4/sql/postgresql`](https://github.com/antlr/grammars-v4/tree/master/sql/postgresql), derived from upstream [`gram.y`](https://github.com/postgres/postgres/blob/master/src/backend/parser/gram.y) |
| `SQLite` | [`antlr/grammars-v4/sql/sqlite`](https://github.com/antlr/grammars-v4/tree/master/sql/sqlite) (already vendored) |
| `SqlServer` | TBD when introduced |

### PARSER-LAYOUT

```
DataProvider/Nimblesite.DataProvider.{Platform}/Parsing/
├── {Platform}Lexer.g4              # vendored grammar
├── {Platform}Parser.g4             # vendored grammar
├── {Platform}{Lexer,Parser}Base.cs # vendored C# helpers (if grammar uses them), namespaced
├── {Platform}{Lexer,Parser,ParserListener,ParserBaseListener,ParserVisitor,ParserBaseVisitor}.cs  # antlr4-generated, checked in
└── {Platform}AntlrParser.cs        # thin facade we author, ≤ 100 LOC
```

The parameter extractor and query-type listener live in `DataProvider.Core` and operate on any `IParseTree`. They are not duplicated per platform.

### PARSER-REGEN

To bump a grammar:

1. `curl` latest `.g4` + `CSharp/*.cs` from `antlr/grammars-v4/sql/{platform}` into `Parsing/`.
2. Wrap helper base files in `namespace Nimblesite.DataProvider.{Platform}.Parsing;`.
3. `java -jar antlr-4.13.1-complete.jar -Dlanguage=CSharp -visitor -listener -o Parsing -package Nimblesite.DataProvider.{Platform}.Parsing Parsing/{Platform}{Lexer,Parser}.g4`
4. Commit the regenerated `.cs` next to the updated `.g4`.
5. Run the platform's parser test suite.

`antlr4` is **not** a build dependency — only a maintenance dependency for regen.

## TOOL

| ID | Spec |
|---|---|
| TOOL-NAME | `DataProvider` |
| TOOL-PROJECT | `DataProvider/DataProvider/`. `<OutputType>Exe</OutputType>`, `<TargetFramework>net9.0</TargetFramework>`, `<AssemblyName>DataProvider</AssemblyName>`. **No** `PackAsTool=true`. |
| TOOL-ARGS | Required: `--connection`, `--config`, `--out`, `--platform`. Optional: `--namespace` (default `<RootNamespace>.DataProvider.Generated`), `--accessibility` (default `public`), `--verbosity`. |
| TOOL-DEPS | References `DataProvider.Core`, every `DataProvider.{Platform}`, every `Nimblesite.Lql.{Platform}`, `Nimblesite.Sql.Model`, `Outcome`, plus every database driver. **All TFMs are net9.0**. No netstandard2.0. |
| TOOL-PROCESS | Own process per invocation. Process isolation guarantees: any native driver dep loads cleanly; tool crashes do not corrupt MSBuild build-server state; tool memory fully reclaimed at exit; tool dll never locks files in IDE edit-rebuild cycles. |
| TOOL-EXIT | `0` on success, `1` on any error. Errors → **stderr** in MSBuild error format `path/to/file(line,col): error DPSGxxx: message` so `<Exec>` parses them into structured IDE errors. Diagnostics → stdout. |
| TOOL-PIPELINE | Per invocation: (1) validate `--connection` → `DPSG001`; (2) parse `--config` JSON → `DPSG006`; (3) open the platform driver per [TOOL-DBOPEN]; (4) introspect only the explicit `(schema, table)` pairs in `DataProvider.json` (never auto-`pg_catalog`/`information_schema`/`sys`); (5) for each `*.lql`, transpile `LqlStatementConverter.ToStatement(content).To{Platform}Sql()`; (6) feed SQL + `DatabaseSchema` into `DataAccessGenerator.Generate(...)`; (7) write `.g.cs` files to `--out`; (8) close + exit. |
| TOOL-DBOPEN | Synchronous open. Append `Pooling=false;Connect Timeout=5;Command Timeout=10` if absent. On any driver-level connection exception: catch once, emit `DPSG002`, exit `1`. Connection-string sanitised through the platform's connection-string builder before any diagnostic — host:port:database equivalent only, never password / auth. |
| TOOL-EMIT | Per-table `DataProvider.{schema}.{table}.g.cs`; per-LQL `DataProvider.lql.{slug}.g.cs`; per-SQL `DataProvider.sql.{slug}.g.cs`; aggregate `DataProvider.Extensions.g.cs`. Slug rules: replace `[/\\.]` → `_`, lowercase, non-ASCII → punycode. Collisions → `DPSG007`. Every generated file carries `[GeneratedCodeAttribute("DataProvider", "{version}")]` with **LF line endings**. |
| TOOL-NAMESPACE | Default `<RootNamespace>.DataProvider.Generated`. Override via `<DataProviderGeneratedNamespace>` → `--namespace`. Default accessibility `public`. Override via `<DataProviderGeneratedAccessibility>` → `--accessibility`. |
| TOOL-ADHOC | Same exe is invocable directly: `dotnet path/to/DataProvider.dll --connection "..." --config DataProvider.json --out /tmp/dp --verbosity diagnostic --platform Postgres`. Same binary, same args. No separate debug tool. |

### Diagnostic IDs

| ID | Severity | Meaning |
|---|---|---|
| DPSG001 | Error | `--connection` missing/empty |
| DPSG002 | Error | Driver `Connection.Open` failed (sanitised target + driver error) |
| DPSG003 | Error | Schema introspection query failed |
| DPSG004 | Error | `.lql` parse error (path, line, col, message) |
| DPSG005 | Error | LQL → platform SQL transpile error |
| DPSG006 | Error | `DataProvider.json` parse / schema validation error |
| DPSG007 | Error | Generated source emit collision |

## PKG

| ID | Spec |
|---|---|
| PKG-NAME | `DataProvider`. **One** package for the entire codegen surface. |
| PKG-LAYOUT | `lib/net9.0/` = runtime extension dlls. `build/DataProvider.targets` (auto-imported). `build/tool/net9.0/` = `DataProvider.dll` + `runtimeconfig.json` + every driver dll + every transitive managed dep + native runtime assets under `runtimes/{rid}/native/`. |
| PKG-DRIVERS | Every supported platform driver vendored under `build/tool/net9.0/`: `Npgsql`, `Microsoft.Data.Sqlite`, `Microsoft.Data.SqlClient`, every transitive managed dep, every native asset under `runtimes/{win-x64,linux-x64,linux-arm64,osx-x64,osx-arm64}/native/` (`e_sqlite3.{dll,so,dylib}`, `sni.dll` / `libMicrosoft.Data.SqlClient.SNI.{so,dylib}`). |
| PKG-VERSION | Package version, runtime extension dlls, and tool exe are all the same version by construction. Drift is structurally impossible. |

### TARGETS-FULL

```xml
<Project>
  <ItemGroup>
    <AdditionalFiles Include="$(MSBuildProjectDirectory)/**/*.lql" />
    <AdditionalFiles Include="$(MSBuildProjectDirectory)/DataProvider.json"
                     Condition="Exists('$(MSBuildProjectDirectory)/DataProvider.json')" />
  </ItemGroup>
  <Target Name="DataProviderCodegen"
          BeforeTargets="CoreCompile"
          Inputs="@(AdditionalFiles);$(MSBuildThisFileDirectory)tool/net9.0/DataProvider.dll"
          Outputs="$(IntermediateOutputPath)DataProvider/.timestamp">
    <Error Condition="'$(DataProviderConnectionString)' == ''"
           Code="DPSG001" Text="DataProviderConnectionString MSBuild property is not set." />
    <MakeDir Directories="$(IntermediateOutputPath)DataProvider" />
    <Exec Command="dotnet &quot;$(MSBuildThisFileDirectory)tool/net9.0/DataProvider.dll&quot; --connection &quot;$(DataProviderConnectionString)&quot; --config &quot;$(MSBuildProjectDirectory)/DataProvider.json&quot; --out &quot;$(IntermediateOutputPath)DataProvider&quot; --platform &quot;$(DataProviderPlatform)&quot; --namespace &quot;$(DataProviderGeneratedNamespace)&quot; --accessibility &quot;$(DataProviderGeneratedAccessibility)&quot;"
          IgnoreExitCode="false" ConsoleToMSBuild="true"
          CustomErrorRegularExpression="^.*\([0-9]+,[0-9]+\):\s*error\s+DPSG[0-9]+:.*$"
          CustomWarningRegularExpression="^.*\([0-9]+,[0-9]+\):\s*warning\s+DPSG[0-9]+:.*$" />
    <Touch Files="$(IntermediateOutputPath)DataProvider/.timestamp" AlwaysCreate="true" />
    <ItemGroup>
      <Compile Include="$(IntermediateOutputPath)DataProvider/**/*.g.cs" />
      <FileWrites Include="$(IntermediateOutputPath)DataProvider/**/*.g.cs" />
      <FileWrites Include="$(IntermediateOutputPath)DataProvider/.timestamp" />
    </ItemGroup>
  </Target>
</Project>
```

Auto-imported by NuGet from `build/DataProvider.targets`. Consumer never sees this file.

## CONSUMER

| ID | Spec |
|---|---|
| CONSUMER-CSPROJ | One `<PackageReference Include="DataProvider">`. One `<DataProviderConnectionString>$(DATAPROVIDER_CONN)</DataProviderConnectionString>`. One `<DataProviderPlatform>` (required, no auto-detection). Optional: `<DataProviderGeneratedNamespace>`, `<DataProviderGeneratedAccessibility>`. Nothing else. |
| CONSUMER-CONNSTR | Resolves through MSBuild from `$DATAPROVIDER_CONN`. Never committed. CI sets it in env. Local sets it via shell rc / `direnv`. IDE sets it via system env + IDE restart. |
| CONSUMER-DELETE | Migrating consumers delete: every `.config/dotnet-tools.json` legacy entry, every `<Target Name="GenerateDataProvider">`, every `Generated/` folder + `.gitignore` line, every `dotnet tool restore` step, every `<Exec>` calling a legacy DataProvider CLI, every `sed` codegen post-step. `dataprovider-migrate` stays per [CON-MIG-FIRST]. |
| CONSUMER-DETERMINISM | Build is deterministic across dev/CI iff both run the same migrations against the same schema first. Per [CON-MIG-FIRST]. The tool itself is deterministic on `(SHA256(schema bytes), SHA256(*.lql), SHA256(*.sql))`. |

## DEPS

| ID | Spec |
|---|---|
| DEPS-EXTRACT | Per platform, move every non-`Main` static method out of the legacy `DataProvider/DataProvider.{Platform}.Cli/Program.cs` into the new `DataProvider/DataProvider.{Platform}/` library (net9.0, one type per file, ≤450 LOC, ≤20 LOC per function). No `Console.WriteLine` (use `ILogger<T>`), no `Environment.Exit` (return `Result<T,CodegenError>`), no `File.WriteAllText` (return generated source as a string; the tool writes files). The legacy `Main` shrinks to a thin shim. Existing CLI tests must stay green. |
| DEPS-NS20 | Not needed. Tool is net9.0, runtime libs are net9.0, no analyzer host, no MSBuild Task host. |
| DEPS-SQLPARSER | `SqlParserCS` is dead weight in `Directory.Build.props`. Zero source imports across the repo. Delete the `<PackageReference>`. Zero compile/runtime impact. |
| DEPS-ANTLR | Every platform library references `Antlr4.Runtime.Standard 4.13.1`. Generated parser sources are checked in next to the `.g4` so downstream builds need only the runtime. **No build-time Java dependency anywhere.** |

## TEST

| ID | Spec |
|---|---|
| TEST-UNIT | `DataProvider.Tool.Tests` (net9.0) drives the tool E2E against a real platform testcontainer. No in-memory DBs. Asserts on literal generated source for fixed schema fixtures. |
| TEST-DIAG | One test per `DPSG001`–`DPSG007`. Each arranges the failure mode and asserts the canonical stderr line + exit code. |
| TEST-MSBUILD | A test project consumes the packed `DataProvider.{version}.nupkg` from a local feed, builds against a testcontainer, asserts `.g.cs` lands in `obj/DataProvider/`, asserts MSBuild surfaces `DPSGxxx` errors structurally in the IDE Error List. |
| TEST-NATIVE | Matrix runs on `win-x64`, `linux-x64`, `linux-arm64`, `osx-x64`, `osx-arm64`. Each combo loads its driver's native asset from `runtimes/` and runs an introspection against a testcontainer. Catches RID-specific packaging mistakes before they reach a consumer. |
| TEST-PARSER | Per platform, `Nimblesite.DataProvider.{Platform}.Tests` parses real fixture SQL and asserts on the resulting syntax tree shape (rule contexts, terminal nodes, parameter list, projection list). Mandatory for every platform under [CON-PARSER-ONLY]. |

## DX

| Scenario | Behavior |
|---|---|
| Fresh clone | `git clone` → set `DATAPROVIDER_CONN` → start DB → `dataprovider-migrate` → `dotnet build`. |
| DB unreachable | `DPSG002` (sanitised target + driver error). No fallback. No offline cache. |
| `dotnet restore` | Tool does **not** run during restore. No DB needed. |
| `dotnet pack` / `dotnet test` | Both invoke compile, therefore invoke the tool, therefore need a reachable DB. |

## RISK

| ID | Spec |
|---|---|
| RISK-NATIVE-RID | New RID = add native asset to package + republish. Mitigated by [TEST-NATIVE] running the full RID matrix in CI. |
| RISK-DETERMINISM | Live-DB introspection means dev/CI may diverge if schemas diverge. Mitigated by [CON-MIG-FIRST]. |
| RISK-FORK-COST | One `dotnet DataProvider.dll` fork per build. Cold start ≈ 100 ms. < 2 % of a typical build. Negligible. |
