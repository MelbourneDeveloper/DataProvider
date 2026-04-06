---
layout: layouts/docs.njk
title: Syntax Overview
description: Complete overview of LQL syntax and language features.
---

LQL uses a functional pipeline syntax where data flows through a series of transformations using the pipeline operator `|>`.

## Basic Structure

Every LQL query starts with a table reference and pipes data through operations:

```
table_name |> operation1(...) |> operation2(...)
```

## Table References

Simply name the table to start a query:

```
users
employees
orders
```

## Pipeline Operator

The `|>` operator passes the result of the left side to the right side:

```
users |> select(users.id, users.name)
```

## Column References

Columns are referenced with the `table.column` syntax:

```
users.id
users.name
orders.total
```

## Lambda Expressions

Lambdas use the `fn(param) => expression` syntax:

```
filter(fn(row) => row.users.age > 18)
filter(fn(row) => row.users.status = 'active' and row.users.age > 21)
```

## Let Bindings

Store intermediate results with `let`:

```
let active_users = users |> filter(fn(row) => row.users.status = 'active')

active_users |> select(active_users.name, active_users.email)
```

## Aliases

Use `as` to rename columns in output:

```
users |> select(
    users.name,
    users.salary * 12 as annual_salary
)
```

## Comments

Single-line comments start with `--`:

```
-- Get all active users
users |> filter(fn(row) => row.users.active)
```

## Operators

### Comparison
`=`, `>`, `<`, `>=`, `<=`, `!=`

### Logical
`and`, `or`

### Arithmetic
`+`, `-`, `*`, `/`

### Sorting
`asc`, `desc`
