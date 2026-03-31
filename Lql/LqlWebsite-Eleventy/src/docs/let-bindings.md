---
layout: layouts/docs.njk
title: Let Bindings
description: Storing intermediate results with let bindings in LQL.
---

Let bindings allow you to name intermediate query results and reuse them, making complex queries more readable and composable.

## Basic Syntax

```
let name = expression
```

## Simple Example

```
let active_users = users |> filter(fn(row) => row.users.status = 'active')

active_users |> select(active_users.name, active_users.email)
```

## Building Complex Queries

Let bindings shine when building multi-step analytics:

```
-- Step 1: Join and filter
let joined =
    users
    |> join(orders, on = users.id = orders.user_id)
    |> filter(fn(row) => row.orders.status = 'completed')

-- Step 2: Aggregate
joined
|> group_by(users.id)
|> select(
    users.name,
    count(*) as total_orders,
    sum(orders.total) as revenue,
    avg(orders.total) as avg_order_value
)
|> filter(fn(row) => row.revenue > 1000)
|> order_by(revenue desc)
|> limit(10)
```

## Reusability

Define a filtered dataset once and use it in multiple contexts:

```
let engineering = employees
    |> filter(fn(row) => row.employees.department = 'Engineering')

-- Use for different analyses
engineering |> select(engineering.name, engineering.salary)
engineering |> group_by(engineering.level) |> select(engineering.level, avg(engineering.salary) as avg_salary)
```
