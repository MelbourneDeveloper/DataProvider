---
name: build
description: Build the DataProvider .NET solution or specific projects. Use when asked to build, compile, or check for compilation errors.
disable-model-invocation: true
allowed-tools: Bash(dotnet build *)
---

# Build

Build the entire solution or a specific project.

## Full solution

```bash
dotnet build /Users/christianfindlay/Documents/Code/DataProvider/DataProvider.sln
```

## Specific project

If `$ARGUMENTS` names a component, build only that project:

| Argument | Project path |
|----------|-------------|
| dataprovider | DataProvider/DataProvider/DataProvider.csproj |
| dataprovider-sqlite | DataProvider/DataProvider.SQLite/DataProvider.SQLite.csproj |
| sqlite-cli | DataProvider/DataProvider.SQLite.Cli/DataProvider.SQLite.Cli.csproj |
| migration | Migration/Migration.Cli/Migration.Cli.csproj |
| lql | Lql/Lql/Lql.csproj |
| sync | Sync/Sync/Sync.csproj |
| gatekeeper | Gatekeeper/Gatekeeper.Api/Gatekeeper.Api.csproj |
| clinical | Samples/Clinical/Clinical.Api/Clinical.Api.csproj |
| scheduling | Samples/Scheduling/Scheduling.Api/Scheduling.Api.csproj |
| icd10 | Samples/ICD10/ICD10.Api/ICD10.Api.csproj |

If no argument is provided, build the full solution.

## Notes

- Generated `.g.cs` files are in `.gitignore` and must be generated at build time
- MSBuild targets in sample projects handle code generation automatically
- If stale `Generated/` folders cause issues, delete them to force regeneration
