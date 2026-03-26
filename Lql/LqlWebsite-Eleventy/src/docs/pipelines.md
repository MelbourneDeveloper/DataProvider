---
layout: layouts/docs.njk
title: Pipeline Operators
description: Deep dive into LQL pipeline operations - select, filter, join, group_by, and more.
---

Pipeline operators are the core of LQL. Each operation takes the result of the previous step and transforms it.

## select

Choose which columns to include in the output:

```
users |> select(users.id, users.name, users.email)
```

Select all columns:

```
users |> select(*)
```

With computed columns:

```
products |> select(
    products.name,
    products.price * products.quantity as total_value,
    round(products.price / 2, 2) as half_price
)
```

## filter

Filter rows using lambda expressions:

```
users |> filter(fn(row) => row.users.age > 18)
```

Combine conditions with `and` / `or`:

```
employees |> filter(fn(row) =>
    row.employees.salary > 50000 and
    row.employees.department = 'Engineering'
)
```

## join

Inner join two tables:

```
users |> join(orders, on = users.id = orders.user_id)
```

## left_join

Left join preserving all rows from the left table:

```
users |> left_join(orders, on = users.id = orders.user_id)
```

## group_by

Group rows by one or more columns:

```
orders |> group_by(orders.status)
```

Multiple grouping columns:

```
orders |> group_by(orders.user_id, orders.status)
```

## having

Filter groups after aggregation:

```
orders
|> group_by(orders.user_id)
|> having(fn(group) => count(*) > 5)
|> select(orders.user_id, count(*) as order_count)
```

## order_by

Sort results ascending or descending:

```
users |> order_by(users.name asc)
users |> order_by(users.created_at desc)
```

## limit / offset

Pagination:

```
users |> limit(10)
users |> limit(10) |> offset(20)
```

## distinct

Remove duplicate rows:

```
orders |> select(orders.status) |> distinct()
```

## union

Combine results from two queries:

```
active_users |> union(inactive_users)
```

## Chaining Operations

The real power comes from chaining multiple operations:

```
users
|> join(orders, on = users.id = orders.user_id)
|> filter(fn(row) => row.orders.status = 'completed')
|> group_by(users.id, users.name)
|> select(
    users.name,
    count(*) as total_orders,
    sum(orders.total) as revenue
)
|> having(fn(group) => count(*) > 2)
|> order_by(revenue desc)
|> limit(10)
```
