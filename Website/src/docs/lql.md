---
layout: layouts/docs.njk
title: Lambda Query Language (LQL)
description: Cross-database business logic with functional pipeline syntax.
---

LQL (Lambda Query Language) is a critical component of the DataProvider framework that enables **portable business logic across database platforms**. Instead of writing platform-specific SQL or stored procedures, you write queries once in LQL and transpile them to PostgreSQL, SQLite, or SQL Server.

## Why LQL?

Database business logic traditionally locks you into a specific platform. Triggers, functions, and complex queries written for PostgreSQL won't work on SQL Server. LQL solves this by providing a unified functional syntax that transpiles to native SQL for each platform.

**Write once, run anywhere:**

```lql
Customer
|> join(Order, on = Customer.Id = Order.CustomerId)
|> filter(fn(row) => row.Order.Total > 1000)
|> select(Customer.Name, Order.Total)
|> order_by(Order.Total DESC)
```

This transpiles to optimized native SQL for your target database.

## Role in DataProvider

LQL integrates directly with DataProvider's source generator:

1. Write `.lql` files in your project
2. LQL transpiles to SQL during build
3. DataProvider generates type-safe C# extension methods
4. Use with full IntelliSense and compile-time safety

This means your database logic is both portable AND type-safe.

## Supported Platforms

- PostgreSQL
- SQLite
- SQL Server

## Full Documentation

For complete syntax reference, examples, and interactive playground:

**[Visit lql.dev →](https://lql.dev)**
