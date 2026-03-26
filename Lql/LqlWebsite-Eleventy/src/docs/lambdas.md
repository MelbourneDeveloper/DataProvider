---
layout: layouts/docs.njk
title: Lambda Expressions
description: Using lambda expressions for filtering and data transformation in LQL.
---

Lambda expressions are the functional core of LQL. They provide type-safe, composable predicates for filtering and transforming data.

## Syntax

```
fn(parameter) => expression
```

The parameter represents a row of data. Access columns using `parameter.table.column`:

```
fn(row) => row.users.age > 18
```

## In filter

The most common use is filtering rows:

```
users |> filter(fn(row) => row.users.active = true)
```

## Compound Expressions

Combine conditions with `and` and `or`:

```
employees |> filter(fn(row) =>
    row.employees.salary > 50000 and
    row.employees.salary < 100000
)
```

```
users |> filter(fn(row) =>
    row.users.role = 'admin' or
    row.users.role = 'superadmin'
)
```

## In having

Lambdas also work with `having` to filter groups:

```
orders
|> group_by(orders.user_id)
|> having(fn(group) => count(*) > 5)
|> select(orders.user_id, count(*) as order_count)
```

## Comparison Operators

| Operator | Meaning |
|----------|---------|
| `=` | Equal |
| `!=` | Not equal |
| `>` | Greater than |
| `<` | Less than |
| `>=` | Greater than or equal |
| `<=` | Less than or equal |

## String Comparisons

```
users |> filter(fn(row) => row.users.name = 'Alice')
users |> filter(fn(row) => row.users.status != 'inactive')
```

## Arithmetic in Lambdas

```
products |> filter(fn(row) =>
    row.products.price * row.products.quantity > 1000
)
```
