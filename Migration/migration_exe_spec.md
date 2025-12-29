# Migration.Cli Specification

NOTE: leave the JSON serialization/deserialization code as is for now, but deactivate it. The core will eventually offer JSON, but we are focusing on YAML for now.

## Overview

`Migration/Migration.Cli/Migration.Cli.csproj` is the **single, canonical CLI tool** for creating databases from schema definitions. All projects that need to spin up a database for code generation MUST use this executable. There is no other way.

## Architecture

Migration.Cli contains the DLLs for both SQLite and Postgres migrations. It is database-agnostic at the interface level - callers specify a YAML schema file path, and the CLI handles the rest.

## Usage

```
dotnet run --project Migration/Migration.Cli/Migration.Cli.csproj -- \
    --schema path/to/schema.yaml \
    --output path/to/database.db \
    --provider [sqlite|postgres]
```

## Schema Input: YAML Only

NOTE: leave the JSON serialization/deserialization code as is for now, but deactivate it. The core will eventually offer JSON, but we are focusing on YAML for now.

The CLI accepts **only YAML schema files**. It does not accept:
- C# code references
- Inline schema definitions
- Project references to schema classes

If a project defines its schema in C# code (e.g., `ExampleSchema.cs`, `ClinicalSchema.cs`), that schema MUST be serialized to YAML first. The YAML file is then passed to Migration.Cli.

### Schema-to-YAML Workflow

1. Project defines schema in C# using `SchemaDefinition` API
2. Build step converts C# schema to YAML file (one-time or as pre-build target)
3. Migration.Cli reads YAML and creates database
4. DataProvider code generation runs against the created database

## Why YAML?

- **No circular dependencies**: Migration.Cli has zero project references to consumer projects
- **Clean build order**: YAML files are static assets, not compiled code
- **Portable**: Schema can be versioned, diffed, and shared without compilation
- **Single tool**: One CLI handles all schemas for all projects

## Forbidden Patterns

- Individual `*BuildDb` projects per consumer (causes circular builds)
- `<Compile Include="../OtherProject/Schema.cs">` in Migration.Cli (circular dependency)
- Multiple CLI tools for database creation
- Hardcoded schema names/switches in the CLI
- CLI tool referencing actual schemas

## MSBuild Integration

Consumer projects call Migration.Cli in a pre-build target:

```xml
<Target Name="CreateBuildDatabase" BeforeTargets="GenerateDataProvider">
    <Exec Command='dotnet run --project "$(SolutionDir)Migration/Migration.Cli/Migration.Cli.csproj" -- --schema "$(MSBuildProjectDirectory)/schema.yaml" --output "$(MSBuildProjectDirectory)/build.db"' />
</Target>
```

No project references. No schema includes. Just a path to a YAML file.
