# SPEC: DataProvider Codegen Tool

> **RIGID. NORMATIVE.** Co-authored by `DataProvider` and `DataProviderSamples`.
> Resolves [PROBLEM-cli-vs-library.md](./PROBLEM-cli-vs-library.md).
> Database-agnostic. Consumer-agnostic. Applies to **every** platform and **every** consumer.
> Roslyn source generator and MSBuild Task DLL approaches were both evaluated and rejected.
> See [`#rejected-architectures`](#rejected-architectures) for the rationale.
>
> ---
>
> ## ⚠️ NAMING IS NON-NEGOTIABLE ⚠️
>
> **The tool is named `DataProvider`. Just `DataProvider`. Nothing else.**
>
> | Artifact | Name | Forbidden |
> |---|---|---|
> | Tool command | `DataProvider` | `Nimblesite.DataProvider`, `dp`, `dataprovider-cli`, anything with a prefix or suffix |
> | NuGet package | `DataProvider` | `Nimblesite.DataProvider`, `DataProvider.Tool`, `DataProvider.Cli` |
> | Tool executable | `DataProvider.dll` (run via `dotnet DataProvider.dll`) | `Nimblesite.DataProvider.dll`, `DataProviderTool.dll` |
> | Tool project | `DataProvider/DataProvider/DataProvider.csproj` | Anything else |
> | Internal libs scoped under the tool | `DataProvider.Core`, `DataProvider.Postgres`, `DataProvider.SQLite` | `Nimblesite.DataProvider.Core`, etc. |
> | External libs the tool consumes | `Nimblesite.Lql.Core`, `Nimblesite.Lql.{Platform}`, `Nimblesite.Sql.Model`, `Outcome` (these stay as-is — they are not the tool) | renaming them is out of scope |
>
> **Renaming the tool, the package, the executable, or any `DataProvider.*` library to add a `Nimblesite.` prefix or any other prefix/suffix is grounds for the renamer being purged from the data center for eternity. — repo owner, on the record.**
>
> ---

## Overview

The DataProvider codegen tool is a console executable shipped **inside** the `DataProvider` NuGet package. It is invoked automatically by an auto-imported MSBuild `.targets` file in the same package, with no consumer-side state. The tool opens a live database connection at codegen time, introspects the schema, transpiles `.lql` files to platform SQL, and emits typed C# data-access code into the consumer's `obj/` directory for the C# compiler to pick up.

## CONSTRAINT

| ID | Constraint |
|---|---|
| CON-NOTOOLMANIFEST | Zero `.config/dotnet-tools.json` entries in any consumer. Zero `dotnet tool restore` steps in any consumer build path. The tool ships **inside** the `DataProvider` NuGet package and is invoked from an auto-imported `.targets` file shipped in the same package. The consumer never references the tool by name and never types `dotnet tool install`. |
| CON-SIMPLE | One `PackageReference` plus one MSBuild property per consumer csproj. No custom `<Target>`. No `<Compile Include="Generated/**">`. No `RemoveDir`/`MakeDir`/`Touch` in the consumer. No post-processing `sed`. |
| CON-DBLIVE | The tool opens a real driver connection and introspects the live schema. Static schema parsing (YAML / JSON) is forbidden. Unreachable DB → loud structured `Diagnostic` and build fail. |
| CON-NOFALLBACK | Zero fallbacks. Zero degraded modes. No offline cache. No "MSBuild Task plan B". The build either passes against a live DB or fails loud. |
| CON-MIG-FIRST | Migrations run **before** codegen, **always**. The migration runner (`dataprovider-migrate`) executes against the live DB before any consumer build invokes the codegen tool. CI, local, IDE — every flow runs migrations first. The migration tool stays. |
| CON-UNIVERSAL | The same single mechanism applies to **every** project consuming **any** platform of `DataProvider`. No consumer-specific names, paths, or domain models. |
| CON-PLATFORM-AGNOSTIC | The same single mechanism applies to **every** database platform: Postgres, SQLite, and any future platform. Identical contract. |
| CON-PROCESS-ISOLATION | Codegen runs in its **own** process, separate from `dotnet build` / MSBuild / the IDE. Process isolation is the only mechanism that loads any TFM driver, any native dependency, on any host, today and forever. |

## PLATFORM

The tool ships with first-class support for these platforms:

| Platform token | Database | Driver | Status |
|---|---|---|---|
| `Postgres` | PostgreSQL | `Npgsql` (latest stable) | shipped from day one |
| `SQLite` | SQLite | `Microsoft.Data.Sqlite` (latest stable, native `e_sqlite3` bundled per RID) | shipped from day one |
| `SqlServer` | SQL Server | `Microsoft.Data.SqlClient` (latest stable, native `sni.dll` bundled per RID) | shipped from day one |

The existing `DataProvider.SqlServer` runtime library and `Nimblesite.Lql.SqlServer` LQL transpiler library are folded into the tool's dependency closure as part of the migration. SQL Server is shipped from day one — not "planned later".

Future platforms (`MySQL`, `Oracle`, `DuckDB`, …) join the table when their introspection is implemented. Each new platform is one new `SchemaIntrospector` implementation and one new driver in the tool's dependency closure. **Adding a platform does not require any change in any consumer csproj.**

### PLATFORM-INCLUSION

A database platform qualifies for DataProvider codegen **iff** its primary .NET driver can load and open a connection inside the tool's `net9.0` process. Because the tool runs in its own process, this is true for **every** managed driver and **every** native-asset driver shipped on nuget.org — there is no analyzer ALC or MSBuild host TFM constraint to clear. Each new platform requires only an introspection implementation and a CI run of [TEST-NATIVE] on every supported RID.

### PLATFORM-SELECTION

The platform is selected at codegen time by:

1. The `--platform` argument passed by the auto-imported `.targets` file from the consumer's `<DataProviderPlatform>` MSBuild property, or
2. Inference from the connection string format if `<DataProviderPlatform>` is not set (e.g., `Host=...` → Postgres, `Data Source=*.db` → SQLite).

### PLATFORM-CONTRACT

Every platform implements the same shared contract:

```csharp
namespace DataProvider.{Platform};

public static class SchemaIntrospector
{
    public static Result<DatabaseSchema, CodegenError> Run(
        string connectionString,
        string configJson,
        CancellationToken ct);
}
```

`DatabaseSchema`, `DatabaseTable`, `DatabaseColumn`, and `CodegenError` live in `DataProvider.Core` and are platform-agnostic. The platform-specific `SchemaIntrospector` is the only piece that opens a connection of the platform's driver type and runs platform-specific introspection queries.

## TOOL

| ID | Spec |
|---|---|
| TOOL-NAME | `DataProvider` |
| TOOL-PROJECT | `DataProvider/DataProvider.Tool/`. `<OutputType>Exe</OutputType>`. `<TargetFramework>net9.0</TargetFramework>`. `<AssemblyName>DataProvider</AssemblyName>`. **No** `PackAsTool=true`. The exe ships inside the runtime NuGet package, not as a separate `dotnet tool install` package. |
| TOOL-INVOCATION | `dotnet "$(MSBuildThisFileDirectory)tool/net9.0/DataProvider.dll" --connection "..." --config "..." --out "..." [--platform "..."] [--namespace "..."] [--accessibility "..."]`. The auto-imported `.targets` file in the package builds the command line; the consumer never types it. |
| TOOL-ARGS | Required: `--connection <connection-string>`, `--config <path-to-DataProvider.json>`, `--out <output-directory>`. Optional: `--platform <Postgres\|SQLite\|...>` (inferred from `--connection` if absent), `--namespace <root-namespace>` (default `<RootNamespace>.DataProvider.Generated`), `--accessibility <internal\|public>` (default `public`), `--verbosity <quiet\|normal\|diagnostic>`. |
| TOOL-DEPS | The tool references `DataProvider.Core`, `DataProvider.Postgres`, `DataProvider.SQLite`, `DataProvider.SqlServer`, `Nimblesite.Lql.Core`, `Nimblesite.Lql.Postgres`, `Nimblesite.Lql.SQLite`, `Nimblesite.Lql.SqlServer`, `Nimblesite.Sql.Model`, `Outcome`, plus every database driver for every supported platform (`Npgsql`, `Microsoft.Data.Sqlite`, `Microsoft.Data.SqlClient`). **All TFMs are net9.0**. No netstandard2.0 multi-targeting. No polyfills. No retargeting work. |
| TOOL-PROCESS | The tool runs in its own process for each codegen invocation. Process isolation guarantees: the tool can load any native driver dependency (`sni.dll`, `e_sqlite3.{so,dylib,dll}`, future native libs) without interfering with MSBuild's process; tool crashes do not corrupt MSBuild build-server state; tool memory is fully reclaimed at process exit; file locks on the tool's own dll never block VS edit-rebuild cycles. |
| TOOL-EXIT | Exit code `0` on success, `1` on any error. Errors are written to **stderr** in the canonical MSBuild error format `path/to/file(line,col): error DPSGxxx: message` so that `<Exec>` parses them into structured MSBuild errors that surface in the IDE Error List with click-to-line. Non-error diagnostic output goes to **stdout**. |
| TOOL-PIPELINE | Per invocation, the tool: (1) validates `--connection` non-empty → `DPSG001`. (2) parses `--config` JSON → `DPSG006`. (3) opens a single connection of the platform's driver type per [TOOL-DBOPEN]. (4) introspects only the `(schema, table)` pairs explicitly listed in `DataProvider.json` — **no implicit "all schemas"**. `pg_catalog`, `information_schema`, `sys`, etc. are never auto-included. (5) for each `*.lql` in the project: `LqlStatementConverter.ToStatement(content).To{Platform}Sql()`. (6) feeds the resulting SQL plus `DatabaseSchema` into `DataAccessGenerator.Generate(...)` from `DataProvider.Core`. (7) writes generated `.g.cs` files to `--out`. (8) closes the connection. (9) exits. |
| TOOL-DBOPEN | Synchronous open. `Pooling=false` is appended to the connection string when not present so no socket survives the process. `Connect Timeout=5;Command Timeout=10` appended when not present, capping any IDE freeze on an unreachable DB to five seconds. On any driver-level connection exception: catch once at the boundary, emit `DPSG002` to stderr, exit `1`. The connection string is sanitised through the platform's connection-string builder before any diagnostic — host:port:database equivalent only, never password, never any auth field. |
| TOOL-EMIT | Generated file naming is stable, deterministic, collision-free, identical across every platform: per-table `DataProvider.{schema}.{table}.g.cs`; per-LQL `DataProvider.lql.{slug}.g.cs`; per-SQL `DataProvider.sql.{slug}.g.cs`; aggregate extensions `DataProvider.Extensions.g.cs`. Slug rules: replace `[/\\.]` with `_`, lowercase, **non-ASCII Unicode → punycode** (so `公开.users` → `xn--gmqs0a.users`). Uniqueness validated within each run; collisions → `DPSG007`. Every generated file carries `[GeneratedCodeAttribute("DataProvider", "{version}")]` and uses **LF line endings only**. |
| TOOL-NAMESPACE | Default namespace is `<consumer's RootNamespace>.DataProvider.Generated`. Override via `<DataProviderGeneratedNamespace>` MSBuild property → `--namespace`. Default accessibility is `public` (consumer data-access types cross assembly boundaries). Override via `<DataProviderGeneratedAccessibility>internal</DataProviderGeneratedAccessibility>` → `--accessibility`. |
| TOOL-ADHOC | The same tool exe is invocable directly by a developer for ad-hoc debugging: `dotnet path/to/DataProvider.dll --connection "..." --config DataProvider.json --out /tmp/dp --verbosity diagnostic`. The build-time invocation and the dev invocation use the **same binary, same args**. No separate debug tool. |

### Diagnostic IDs

`DPSG` stands for "DataProvider Source Generator" (preserved from the rejected Roslyn approach so existing references remain valid).

| ID | Severity | Meaning |
|---|---|---|
| DPSG001 | Error | `--connection` argument missing or empty (or `<DataProviderConnectionString>` MSBuild property unset) |
| DPSG002 | Error | Driver `Connection.Open` failed (sanitised target host:port:database + driver error) |
| DPSG003 | Error | Schema introspection query failed (query name + driver error) |
| DPSG004 | Error | `.lql` parse error (file path, line, column, message) |
| DPSG005 | Error | LQL → platform SQL transpilation error (file path, message) |
| DPSG006 | Error | `DataProvider.json` parse / schema validation error |
| DPSG007 | Error | Generated source emit collision (offending hint name) |

## PKG

| ID | Spec |
|---|---|
| PKG-NAME | `DataProvider`. **One** package for the entire codegen surface. The package contains the runtime extension methods, the tool exe + every driver + every transitive dep, and the auto-imported `.targets`. |
| PKG-LAYOUT | `lib/net9.0/` contains the runtime extension dlls (`DataProvider.{Core,Postgres,SQLite,SqlServer}.dll`, `Nimblesite.Lql.{Core,Postgres,SQLite,SqlServer}.dll`, `Nimblesite.Sql.Model.dll`, `Outcome.dll`). `build/DataProvider.targets` (auto-imported by NuGet). `build/tool/net9.0/DataProvider.dll` + `DataProvider.runtimeconfig.json` + every driver dll + every transitive managed dep + every native runtime asset under `build/tool/net9.0/runtimes/{rid}/native/`. |
| PKG-DRIVERS | The package vendors **every** supported platform driver in `build/tool/net9.0/`: `Npgsql.dll`, `Microsoft.Data.Sqlite.dll`, `Microsoft.Data.SqlClient.dll`, every transitive managed dep, and every native runtime asset under `build/tool/net9.0/runtimes/{win-x64,linux-x64,linux-arm64,osx-x64,osx-arm64}/native/` — including `e_sqlite3.{dll,so,dylib}` for SQLite and `sni.dll` (Windows) / `libMicrosoft.Data.SqlClient.SNI.so` (Linux) / `libMicrosoft.Data.SqlClient.SNI.dylib` (macOS) for SqlServer. The `DataProvider.runtimeconfig.json` points the runtime asset resolver at the package's local `runtimes/` tree. |
| PKG-VERSION | The package version, the runtime extension dlls, and the tool exe are **all the same version** by construction (single `dotnet pack` output of one solution). Version drift between tool and runtime is structurally impossible. |
| PKG-SIZE | The package size grows linearly with the number of supported platform drivers. This is acceptable: a consumer pays one-time download for forever-future flexibility. No driver version conflicts because everything is private to the tool process. |
| PKG-TARGETS | The auto-imported `build/DataProvider.targets` declares the codegen target. See [TARGETS-FULL] below. |

### [TARGETS-FULL]

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
           Code="DPSG001"
           Text="DataProviderConnectionString MSBuild property is not set." />

    <MakeDir Directories="$(IntermediateOutputPath)DataProvider" />

    <Exec Command="dotnet &quot;$(MSBuildThisFileDirectory)tool/net9.0/DataProvider.dll&quot; --connection &quot;$(DataProviderConnectionString)&quot; --config &quot;$(MSBuildProjectDirectory)/DataProvider.json&quot; --out &quot;$(IntermediateOutputPath)DataProvider&quot; --platform &quot;$(DataProviderPlatform)&quot; --namespace &quot;$(DataProviderGeneratedNamespace)&quot; --accessibility &quot;$(DataProviderGeneratedAccessibility)&quot;"
          IgnoreExitCode="false"
          ConsoleToMSBuild="true"
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

The consumer never sees this file. It is auto-imported by NuGet because it lives at `build/DataProvider.targets` inside the package.

## CONSUMER

| ID | Spec |
|---|---|
| CONSUMER-CSPROJ | One `PackageReference` to `DataProvider`. One property `<DataProviderConnectionString>$(DATAPROVIDER_CONN)</DataProviderConnectionString>`. Optional: `<DataProviderPlatform>` (auto-detected from the connection string if absent), `<DataProviderGeneratedNamespace>`, `<DataProviderGeneratedAccessibility>`. Nothing else. |
| CONSUMER-MULTIDB | A multi-database csproj declares `<DataProviderConnectionString>` plus `<DataProviderPlatform>` per build configuration, or splits into one csproj per platform. The same single package handles all platforms. |
| CONSUMER-TESTPROJ | Test projects that consume generated types reference the same package and declare their own `<DataProviderConnectionString>`. Test projects participate in codegen identically to production csprojs. |
| CONSUMER-CONNSTR | Value resolves through MSBuild from the environment variable `DATAPROVIDER_CONN`. Never committed to source. CI sets it in workflow env. Local terminal sets it via shell rc / `direnv`. IDE sets it via system environment (`setx` on Windows, `launchctl setenv` on macOS, `~/.profile` on Linux) and IDE restart. |
| CONSUMER-DELETE | When migrating from the previous CLI-based pipeline, every consumer deletes: every `.config/dotnet-tools.json` entry for `dataprovider-postgres`, `dataprovider-sqlite`, `dataprovider-sqlserver`, `lql-postgres`, `lql-sqlite`, `lql-sqlserver` (and any other legacy per-platform DataProvider CLI); every `<Target Name="GenerateDataProvider">`; every `Generated/` folder + `.gitignore` entry; every `dotnet tool restore` step; every `<Exec>` that calls a legacy DataProvider CLI; every `sed` codegen post-step. The `dataprovider-migrate` tool stays per [CON-MIG-FIRST]. |
| CONSUMER-DETERMINISM | The build is deterministic across dev and CI iff dev and CI run the same migrations against the same schema before invoking the tool. Per [CON-MIG-FIRST] this is enforced by every consumer's build orchestration (`make ci`, `dotnet build` wrapper, etc.). The tool itself is deterministic on `(SHA256(schema bytes), SHA256(*.lql), SHA256(*.sql))`. |

## DEPS

| ID | Spec |
|---|---|
| DEPS-EXTRACT | For every platform, the existing `DataProvider/DataProvider.{Platform}.Cli/Program.cs` contains every platform-specific introspection and code-emission function inline. There is no `DataProvider.{Platform}` library today. The extraction creates one library per platform at `DataProvider/DataProvider.{Platform}/` targeting **net9.0** (single TFM). It moves every non-`Main` static method out of `Program.cs` into the new library, one type per file, ≤450 LOC per file, ≤20 LOC per function, per `CLAUDE.md`. Extraction rules: no `Console.WriteLine` (use `ILogger<T>`), no `Environment.Exit` (return `Result<T, CodegenError>`), no `File.WriteAllText` in the library (the library returns generated source as strings; the tool writes files). The legacy CLI's `Main` shrinks to a thin shim that constructs an `ILogger` and calls the library. Existing CLI tests continue to pass. **Sunk cost — required regardless of architecture.** |
| DEPS-NS20 | Not needed. The tool is net9.0. The runtime extension dlls in `lib/net9.0/` are net9.0. There is no analyzer host, no MSBuild Task host, no netstandard2.0 chokepoint. `DataProvider.Core`, `Nimblesite.Lql.Core`, `Nimblesite.Lql.Postgres`, `Nimblesite.Lql.SQLite`, `Nimblesite.Sql.Model`, `Outcome` all stay net9.0. Zero retarget work. |
| DEPS-SQLPARSER | `SqlParserCS` is dead weight in `Directory.Build.props`. Zero source imports across the entire repository (verified by audit). Removed via one-line deletion of the `<PackageReference Include="SqlParserCS" ...>` declaration. Zero compile/runtime impact. |

## DX

| Scenario | Behavior |
|---|---|
| Fresh clone | `git clone` → set `DATAPROVIDER_CONN` → start DB → `dataprovider-migrate` → `dotnet build`. Five steps, identical for every consumer. |
| DB unreachable | Build fails with `DPSG002` (sanitised target + driver error). No fallback. No offline cache. Per [CON-DBLIVE]. |
| Editor cold-start, DB down | Every csproj that references generated types shows `DPSG002` in the IDE Error List until the DB starts and the next build runs. Deliberate cost of [CON-DBLIVE]. |
| `dotnet restore` | The tool does **not** run during restore. Restore does no compile work. No DB needed. |
| `dotnet pack` / `dotnet test` | Both invoke compile, therefore invoke the tool, therefore need a reachable DB per [CON-DBLIVE]. |
| Ad-hoc dev iteration | `dotnet path/to/DataProvider.dll --connection "..." --config DataProvider.json --out /tmp/dp --verbosity diagnostic` runs the same binary outside the build for inspection and debugging. |

## MIGRATE

| Step | Action |
|---|---|
| 1 | Delete the `SqlParserCS` `<PackageReference>` from `Directory.Build.props` per [DEPS-SQLPARSER]. |
| 2 | For each platform (Postgres, SQLite, SqlServer): extract the new `DataProvider.{Platform}` library per [DEPS-EXTRACT]. The existing `Nimblesite.DataProvider.SqlServer` library and `Nimblesite.Lql.SqlServer` library are folded in (renamed to `DataProvider.SqlServer` to comply with the naming brand at the top of this spec). |
| 3 | Create the new `DataProvider/DataProvider.Tool/` console exe project per [TOOL-PROJECT]. Reference every platform's library + driver. Implement [TOOL-PIPELINE]. Add tests per [TEST-*]. |
| 4 | Configure the runtime csproj `DataProvider` to pack: (a) the runtime extension dlls under `lib/net9.0/`, (b) the tool exe + every driver + every transitive dep + every `runtimes/{rid}/native/` asset under `build/tool/net9.0/`, (c) the `DataProvider.targets` under `build/`. |
| 5 | Run [TEST-NATIVE] across the full RID matrix for **every** platform — Postgres, SQLite, SqlServer. |
| 6 | Publish `DataProvider` to nuget.org. |
| 7 | **DELETE** every legacy CLI project from the repository: `DataProvider/Nimblesite.DataProvider.Postgres.Cli/`, `DataProvider/Nimblesite.DataProvider.SQLite.Cli/`, `Lql/Nimblesite.Lql.Cli.Postgres/`, `Lql/Nimblesite.Lql.Cli.SQLite/`, plus the corresponding `*.Tests` projects. Their nuget.org packages get one final release with a deprecation notice pointing at `DataProvider`, then they are unlisted. The migration tool (`dataprovider-migrate`) is the **only** preserved legacy tool and stays per [CON-MIG-FIRST]. |

### MIGRATE-CLI-FATE

The legacy per-platform codegen CLIs are **deleted** from the repository per [MIGRATE] step 7. They are not preserved as debug utilities. The unified `DataProvider` tool's `--verbosity diagnostic` flag and ad-hoc invocation per [TOOL-ADHOC] cover every debugging use case the legacy CLIs covered. **Only** the migration tool (`dataprovider-migrate`) survives, per [CON-MIG-FIRST].

## TEST

| ID | Spec |
|---|---|
| TEST-UNIT | `DataProvider.Tool.Tests` (net9.0) drives the tool end-to-end against a real platform testcontainer for each supported platform. No in-memory databases per `CLAUDE.md`. Asserts on literal generated source for fixed schema fixtures. |
| TEST-DIAG | One test per `DPSG001`–`DPSG007`. Each arranges the failure mode and asserts the canonical stderr line and exit code. |
| TEST-MSBUILD | A test project consumes the packed `DataProvider.{version}.nupkg` from a local feed, builds against a testcontainer, and asserts the build produces expected `.g.cs` files in `obj/DataProvider/`. Verifies the auto-imported `.targets` wires correctly through `<Exec>` and that `CustomErrorRegularExpression` parses errors into MSBuild structured errors that surface in the IDE Error List. |
| TEST-NATIVE | The test matrix runs on `win-x64`, `linux-x64`, `linux-arm64`, `osx-x64`, `osx-arm64`. Each combination loads its platform driver's native asset (`e_sqlite3.{dll,so,dylib}` for SQLite; `sni.dll` Windows / `libMicrosoft.Data.SqlClient.SNI.so` Linux / `libMicrosoft.Data.SqlClient.SNI.dylib` macOS for SqlServer; future Oracle `libclntsh`, future DuckDB `libduckdb`) from the package's `runtimes/` tree and asserts a successful introspection against a testcontainer. Catches RID-specific packaging mistakes before they reach a consumer. Postgres has no native dep but is still in the matrix. |

## RISK

| ID | Spec |
|---|---|
| RISK-NATIVE-RID | The package vendors native driver assets for every supported RID. A new RID (e.g., `linux-musl-x64`, `linux-bionic-arm64`) requires adding the corresponding native asset to the package and re-publishing. Mitigation: [TEST-NATIVE] runs the full RID matrix in CI for every release. |
| RISK-DETERMINISM | Live-DB introspection means dev and CI may diverge if their schemas diverge. Mitigated by [CON-MIG-FIRST] — the migration tool runs the same YAML migrations against both, producing identical schemas. |
| RISK-PKG-SIZE | The package vendors every supported driver and every native asset, growing with the platform count. Acceptable: consumers pay one-time download cost for forever-future flexibility. No driver version conflicts because everything is private to the tool process. |
| RISK-FORK-COST | Each build forks one `dotnet DataProvider.dll` process. Cold start ≈ 100 ms. Codegen runs once per build (single `<Exec>` call), so the fork cost is < 2% of a typical 5–30 s build. Negligible. |

## Rejected architectures

The following architectures were considered and rejected before this spec was written:

| Architecture | Why rejected |
|---|---|
| Roslyn `IIncrementalGenerator` shipped as analyzer asset | Roslyn analyzers are hard-locked to `netstandard2.0` by Microsoft. Not every database driver ships ns2.0. The architecture cannot satisfy `[CON-UNIVERSAL]`. |
| MSBuild Task DLL invoked from auto-imported `.targets` | MSBuild Tasks load into MSBuild's host process. Native driver dependencies (`sni.dll`, `e_sqlite3.{so,dylib}`, future native libs) are unreliable inside MSBuild's process across all hosts (`dotnet build`, Visual Studio, JetBrains Rider) and all RIDs. Task crashes corrupt MSBuild build-server state. The Task assembly stays loaded across builds in IDE scenarios, locking files for edit-rebuild cycles. Process isolation is the only way to guarantee universal driver support. |
| `dotnet tool` packaged via `PackAsTool=true` and installed via `dotnet tool install` | Requires a `.config/dotnet-tools.json` manifest in every consumer + `dotnet tool restore` step in CI. Version drifts from the runtime library reference. Failure modes match every problem documented in [PROBLEM-cli-vs-library.md]. |
| Pre-build `<Exec>` calling a separate CLI tool package | Same problems as `PackAsTool` plus splits the tool from the runtime library across two NuGet packages, reintroducing the version drift documented in [PROBLEM-cli-vs-library.md]. |

The chosen architecture (this spec) is **a console exe shipped inside the runtime NuGet package, invoked from an auto-imported `.targets` file in the same package**. It has none of the above failure modes, requires zero consumer-side state, requires zero retarget work, and supports any database driver and any native dependency on any host.

## AGREEMENT

| Agent | Status |
|---|---|
| `DataProviderSamples` | ✅ AGREED |
| `DataProvider` | (countersign here) |
| Repo owner | Final approval. |
