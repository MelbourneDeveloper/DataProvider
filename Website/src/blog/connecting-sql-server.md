---
layout: layouts/blog.njk
title: Connecting to SQL Server with DataProvider
description: Install the SQL Server runtime package, generate extension methods from your queries, and call them with Result-based error handling.
date: 2024-04-08
dateModified: 2026-04-12
author: DataProvider Team
tags:
  - .NET
  - SQL
  - Database
  - post
---

DataProvider targets SQL Server as a first-class runtime. This guide shows you how to install the package, generate your first extension method, and call it safely.

## Setup

Install the SQL Server runtime package plus the `DataProvider` CLI tool:

```bash
dotnet add package Nimblesite.DataProvider.SqlServer --version 0.9.6-beta

dotnet new tool-manifest
dotnet tool install DataProvider --version 0.9.6-beta
```

## Generate from a query file

Write `GetCustomers.sql`:

```sql
SELECT Id, Name, Email
FROM Customers
WHERE Active = @active
```

Add it to `DataProvider.json`:

```json
{
  "connectionString": "Server=localhost;Database=MyDb;Trusted_Connection=true",
  "queries": [
    { "name": "GetCustomers", "sqlFile": "GetCustomers.sql" }
  ]
}
```

Generate the extension methods:

```bash
dotnet DataProvider sqlserver --project-dir . --config DataProvider.json --out ./Generated
```

## Call the generated method

```csharp
using Microsoft.Data.SqlClient;
using Nimblesite.DataProvider.Core;
using MyApp.Generated;

await using var connection = new SqlConnection("Server=localhost;Database=MyDb;Trusted_Connection=true");
await connection.OpenAsync();

var result = await connection.GetCustomersAsync(active: true);

if (result is Result<IReadOnlyList<GetCustomersRow>, SqlError>.Ok ok)
{
    foreach (var customer in ok.Value)
        Console.WriteLine($"{customer.Id}: {customer.Name}");
}
```

DataProvider **never throws** on query failure. Pattern match on `Result<T, SqlError>` and handle both branches explicitly.

Check out the [full documentation](/docs/getting-started/) for the end-to-end walkthrough.
