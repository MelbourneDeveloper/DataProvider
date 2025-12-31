---
layout: layouts/docs.njk
title: Installation
description: How to install DataProvider in your .NET project.
---

## NuGet Package

Install DataProvider via NuGet:

```bash
dotnet add package DataProvider
```

Or using the Package Manager Console:

```powershell
Install-Package DataProvider
```

## Database Providers

Install the provider for your database:

### SQL Server

```bash
dotnet add package DataProvider.SqlServer
```

### MySQL

```bash
dotnet add package DataProvider.MySql
```

### SQLite

```bash
dotnet add package DataProvider.Sqlite
```

## Requirements

- .NET 9.0 or later
- C# 13 or later
- Nullable reference types enabled

## Project Configuration

Add the following to your `.csproj`:

```xml
<PropertyGroup>
  <TargetFramework>net9.0</TargetFramework>
  <Nullable>enable</Nullable>
  <LangVersion>latest</LangVersion>
</PropertyGroup>
```

## Next Steps

- [Quick Start Guide](/docs/quick-start/)
- [DataProvider Documentation](/docs/dataprovider/)
