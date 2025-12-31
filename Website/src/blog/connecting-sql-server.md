---
layout: layouts/blog.njk
title: Connecting to SQL Server with DataProvider
description: How to connect to SQL Server using the DataProvider toolkit.
date: 2024-04-08
author: DataProvider Team
tags:
  - .NET
  - SQL
  - Database
  - post
---

Connecting to SQL Server with DataProvider is straightforward. This guide shows you how to set up your connection and start querying.

## Setup

First, install the SQL Server provider:

```bash
dotnet add package DataProvider.SqlServer
```

## Connection

Create your connection:

```csharp
using DataProvider.SqlServer;

var connectionString = "Server=localhost;Database=MyDb;...";
using var connection = new SqlConnection(connectionString);
var provider = new SqlServerProvider(connection);
```

## Querying

Now you can execute queries:

```csharp
var customers = provider.Query<Customer>(
    "SELECT * FROM Customers WHERE Active = 1"
);
```

Check out the [full documentation](/docs/getting-started/) for more details.
