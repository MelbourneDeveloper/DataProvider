# DataProvider NuGet Package Release Plan

## Goal
Release DataProvider packages to NuGet.org so apps can consume them without git submodules.

---

## Decisions
- **Naming**: Libraries depending on DataProvider use `DataProvider.*` prefix; standalone libs keep simple names
- **Publish to**: NuGet.org (public)
- **Release trigger**: Git tags (`v*`)
- **Outcome**: Already on NuGet, just reference it

---

## Packages

### Libraries

| Package | Description | Dependencies |
|---------|-------------|--------------|
| `Selecta` | Utility library | None |
| `Migration` | Schema definition/DDL | YamlDotNet |
| `Migration.SQLite` | SQLite DDL generator | Migration |
| `Migration.Postgres` | Postgres DDL generator | Migration, Npgsql |
| `DataProvider` | Core code generation | Selecta, Outcome |
| `DataProvider.SQLite` | SQLite source generator | DataProvider, Selecta, Antlr4 |

### CLI Tools (dotnet tools)

| Tool | Command | Install |
|------|---------|---------|
| `DataProvider.SQLite.Cli` | `dataprovider-sqlite` | `dotnet tool install DataProvider.SQLite.Cli -g` |
| `DataProvider.Postgres.Cli` | `dataprovider-postgres` | `dotnet tool install DataProvider.Postgres.Cli -g` |
| `Migration.Cli` | `migration-cli` | `dotnet tool install Migration.Cli -g` |

---

## Files to Modify

| File | Action |
|------|--------|
| `Directory.Build.props` | Add shared NuGet metadata |
| `Other/Selecta/Selecta.csproj` | Add PackageId, Description |
| `Migration/Migration/Migration.csproj` | Add PackageId, Description |
| `Migration/Migration.SQLite/Migration.SQLite.csproj` | Add PackageId, Description |
| `Migration/Migration.Postgres/Migration.Postgres.csproj` | Add PackageId, Description |
| `Migration/Migration.Cli/Migration.Cli.csproj` | Add PackAsTool, ToolCommandName |
| `DataProvider/DataProvider/DataProvider.csproj` | Add PackageId, Description |
| `DataProvider/DataProvider.SQLite/DataProvider.SQLite.csproj` | Add PackageId, Description |
| `DataProvider/DataProvider.Postgres.Cli/DataProvider.Postgres.Cli.csproj` | Add PackAsTool, ToolCommandName |
| `DataProvider/DataProvider.SQLite.Cli/DataProvider.SQLite.Cli.csproj` | Add PackAsTool, ToolCommandName |
| `.github/workflows/release.yml` | **CREATE** - Automated release workflow |

---

## Release Workflow

**File: `.github/workflows/release.yml`**

```yaml
name: Release NuGet Packages

on:
  push:
    tags:
      - 'v*'

env:
  DOTNET_SKIP_FIRST_TIME_EXPERIENCE: true
  DOTNET_CLI_TELEMETRY_OPTOUT: true

jobs:
  publish:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'

      - name: Extract version from tag
        id: version
        run: echo "VERSION=${GITHUB_REF#refs/tags/v}" >> $GITHUB_OUTPUT

      - name: Restore
        run: dotnet restore

      - name: Build
        run: dotnet build -c Release --no-restore

      - name: Test
        run: dotnet test -c Release --no-build

      - name: Pack libraries
        run: |
          dotnet pack Other/Selecta/Selecta.csproj -c Release -p:Version=${{ steps.version.outputs.VERSION }} -o ./nupkgs
          dotnet pack Migration/Migration/Migration.csproj -c Release -p:Version=${{ steps.version.outputs.VERSION }} -o ./nupkgs
          dotnet pack Migration/Migration.SQLite/Migration.SQLite.csproj -c Release -p:Version=${{ steps.version.outputs.VERSION }} -o ./nupkgs
          dotnet pack Migration/Migration.Postgres/Migration.Postgres.csproj -c Release -p:Version=${{ steps.version.outputs.VERSION }} -o ./nupkgs
          dotnet pack DataProvider/DataProvider/DataProvider.csproj -c Release -p:Version=${{ steps.version.outputs.VERSION }} -o ./nupkgs
          dotnet pack DataProvider/DataProvider.SQLite/DataProvider.SQLite.csproj -c Release -p:Version=${{ steps.version.outputs.VERSION }} -o ./nupkgs

      - name: Pack CLI tools
        run: |
          dotnet pack DataProvider/DataProvider.Postgres.Cli/DataProvider.Postgres.Cli.csproj -c Release -p:Version=${{ steps.version.outputs.VERSION }} -o ./nupkgs
          dotnet pack DataProvider/DataProvider.SQLite.Cli/DataProvider.SQLite.Cli.csproj -c Release -p:Version=${{ steps.version.outputs.VERSION }} -o ./nupkgs
          dotnet pack Migration/Migration.Cli/Migration.Cli.csproj -c Release -p:Version=${{ steps.version.outputs.VERSION }} -o ./nupkgs

      - name: Push to NuGet
        run: dotnet nuget push "./nupkgs/*.nupkg" --api-key ${{ secrets.NUGET_API_KEY }} --source https://api.nuget.org/v3/index.json --skip-duplicate

      - name: Create GitHub Release
        uses: softprops/action-gh-release@v1
        with:
          files: ./nupkgs/*.nupkg
          generate_release_notes: true
```

---

## Release Process

1. Make all .csproj changes
2. Add `NUGET_API_KEY` secret to GitHub repo settings
3. Tag and push: `git tag v0.1.0 && git push origin v0.1.0`
4. GitHub Actions runs automatically: build → test → pack → push to NuGet.org

---

## Versioning

- Semantic Versioning: `MAJOR.MINOR.PATCH`
- Start at `0.1.0`
- All packages share same version (monorepo style)
- Version from git tag: `v0.1.0` → version `0.1.0`
