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

1. Schema is defined in a **separate Migrations assembly** (e.g., `MyProject.Migrations/`) with NO dependencies on generated code
2. Build step compiles the Migrations assembly first
3. Schema.Export.Cli exports C# schema to YAML file
4. Migration.Cli reads YAML and creates database
5. DataProvider code generation runs against the created database
6. Main project (e.g., `MyProject.Api/`) compiles with generated code

### CRITICAL: Separate Migrations Assemblies

**Schemas MUST be in separate assemblies to avoid circular build dependencies.**

**Naming convention: Always use `*.Migrations` suffix, never `*.Schema` or `*BuildDb`.**

Correct pattern:
```
MyProject.Migrations/       # Schema definition only, NO generated code deps
  └── MyProjectSchema.cs    # Defines SchemaDefinition
MyProject.Api/              # References MyProject.Migrations, has generated code
  └── Generated/            # DataProvider generated code
```

The Migrations assembly:
- Contains ONLY the `SchemaDefinition` class
- References ONLY `Migration` (for schema types)
- Has NO dependencies on generated code
- Can be built BEFORE code generation runs

The API/main assembly:
- References the Migrations assembly
- Contains generated code from DataProvider
- Is built AFTER code generation

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
- **Schema classes in the same project as generated code** (causes circular build deps)
- Migrations assemblies with dependencies on generated code
- Using `*.Schema` or `*BuildDb` naming (must use `*.Migrations`)

## MSBuild Integration

Consumer projects call Schema.Export.Cli then Migration.Cli in pre-build targets:

```xml
<!-- Step 1: Export C# schema to YAML (Migrations assembly must be built first) -->
<Target Name="ExportSchemaToYaml" BeforeTargets="CreateBuildDatabase">
    <Exec Command='dotnet run --project "$(SolutionDir)Migration/Schema.Export.Cli/Schema.Export.Cli.csproj" -- --assembly "$(SolutionDir)MyProject.Migrations/bin/Debug/net9.0/MyProject.Migrations.dll" --type "MyProject.Migrations.MyProjectSchema" --output "$(MSBuildProjectDirectory)/schema.yaml"' />
</Target>

<!-- Step 2: Create database from YAML -->
<Target Name="CreateBuildDatabase" BeforeTargets="GenerateDataProvider">
    <Exec Command='dotnet run --project "$(SolutionDir)Migration/Migration.Cli/Migration.Cli.csproj" -- --schema "$(MSBuildProjectDirectory)/schema.yaml" --output "$(MSBuildProjectDirectory)/build.db" --provider sqlite' />
</Target>
```

No project references. No schema includes. Just paths to assemblies and YAML files.

## Build Order (CRITICAL)

To avoid circular dependencies, the build order MUST be:

```
1. Migration/Migration.csproj           # Core types
2. MyProject.Migrations/                # Schema definition (refs Migration only)
3. Schema.Export.Cli                    # Export schema to YAML
4. Migration.Cli                        # Create DB from YAML
5. DataProvider code generation         # Generate C# from DB
6. MyProject.Api/                       # Main project with generated code
```

The Migrations assembly MUST NOT reference:
- The API/main project
- Any generated code
- Any project that depends on generated code
