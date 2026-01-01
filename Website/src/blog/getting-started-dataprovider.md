---
layout: layouts/blog.njk
title: Getting Started with the DataProvider Toolkit
description: An introduction to using the DataProvider toolkit in .NET
date: 2024-04-20
author: DataProvider Team
tags:
  - .NET
  - C#
  - post
---

DataProvider is a powerful toolkit for .NET developers that simplifies database connectivity and data access. In this post, we'll walk through the basics of getting started.

## Installation

First, add the DataProvider package to your project:

```bash
dotnet add package DataProvider
```

## Your First Query

With DataProvider installed, you can start executing queries immediately:

```csharp
using DataProvider;

var orders = connection.Query<Order>(
    "SELECT * FROM Orders WHERE Status = @status",
    new { status = "Active" }
);
```

## Type Safety

DataProvider generates type-safe extension methods at compile time, giving you IntelliSense support and compile-time checking.

## Next Steps

Check out our [documentation](/docs/getting-started/) for more detailed guides and examples.
