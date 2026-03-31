---
layout: layouts/docs.njk
title: Aggregation
description: Group by, aggregate functions, and having clauses in LQL.
---

LQL provides full aggregation support with group by, aggregate functions, and having clauses.

## Aggregate Functions

| Function | Description |
|----------|-------------|
| `count(*)` | Count all rows |
| `sum(column)` | Sum of values |
| `avg(column)` | Average of values |
| `min(column)` | Minimum value |
| `max(column)` | Maximum value |

## Basic Aggregation

```
orders
|> group_by(orders.status)
|> select(
    orders.status,
    count(*) as order_count
)
```

## Multiple Group Columns

```
orders
|> group_by(orders.user_id, orders.status)
|> select(
    orders.user_id,
    orders.status,
    count(*) as order_count,
    sum(orders.total) as total_amount
)
```

## Having Clause

Filter groups after aggregation using lambda expressions:

```
orders
|> group_by(orders.user_id)
|> having(fn(group) => count(*) > 2)
|> select(
    orders.user_id,
    count(*) as order_count,
    sum(orders.total) as total_amount,
    avg(orders.total) as avg_amount
)
```

## Complete Analytics Query

```
orders
|> group_by(orders.user_id, orders.status)
|> select(
    orders.user_id,
    orders.status,
    count(*) as order_count,
    sum(orders.total) as total_amount,
    avg(orders.total) as avg_amount
)
|> having(fn(group) => count(*) > 2)
|> order_by(total_amount desc)
```
