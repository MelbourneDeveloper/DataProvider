---
layout: layouts/docs.njk
title: Installation
description: Install LQL packages and tools for your project.
---

## NuGet Packages

LQL provides dialect-specific packages for each target database:

```xml
<!-- SQLite -->
<PackageReference Include="Lql.SQLite" Version="*" />

<!-- PostgreSQL -->
<PackageReference Include="Lql.Postgres" Version="*" />

<!-- SQL Server -->
<PackageReference Include="Lql.SqlServer" Version="*" />
```

## CLI Tool

Install the LQL CLI for command-line transpilation:

```bash
dotnet tool install -g LqlCli.SQLite
```

## F# Type Provider

For compile-time validated LQL queries in F#:

```xml
<PackageReference Include="Lql.TypeProvider.FSharp" Version="*" />
```

## VS Code Extension

Search for **LQL** in VS Code Extensions marketplace for:

- Syntax highlighting
- IntelliSense completions
- Real-time diagnostics
- Hover documentation
- Document formatting

## Requirements

- .NET 9.0 or later
- One of the supported databases: SQLite, PostgreSQL, or SQL Server
