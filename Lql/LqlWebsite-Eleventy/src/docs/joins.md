---
layout: layouts/docs.njk
title: Joins
description: Combining data from multiple tables with LQL join operations.
---

LQL supports multiple join types for combining data from different tables.

## Inner Join

Returns only rows that have matching values in both tables:

```
users
|> join(orders, on = users.id = orders.user_id)
|> select(users.name, orders.total, orders.status)
```

## Left Join

Returns all rows from the left table, with matching rows from the right table (or NULL):

```
users
|> left_join(orders, on = users.id = orders.user_id)
|> select(users.name, orders.total)
```

## Multiple Joins

Chain joins to combine more than two tables:

```
users
|> join(orders, on = users.id = orders.user_id)
|> join(products, on = orders.product_id = products.id)
|> select(users.name, products.name, orders.quantity)
```

## Join with Filter

Combine joins with filtering:

```
users
|> join(orders, on = users.id = orders.user_id)
|> filter(fn(row) => row.orders.status = 'completed')
|> select(users.name, orders.total)
```

## Join with Aggregation

```
users
|> join(orders, on = users.id = orders.user_id)
|> group_by(users.id, users.name)
|> select(
    users.name,
    count(*) as total_orders,
    sum(orders.total) as revenue
)
|> order_by(revenue desc)
```
