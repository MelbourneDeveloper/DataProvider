# LQL — Lambda Query Language Reference

## Syntax

Pipeline-style DSL that transpiles to SQL. All operations chain with `|>`.

```
table |> operation1(...) |> operation2(...) |> select(...)
```

## Pipeline Operations

| Operation | SQL Equivalent | Syntax |
|-----------|---------------|--------|
| `select(cols...)` | `SELECT` | `select(name, email, age)` or `select(*)` |
| `filter(fn(row) => cond)` | `WHERE` | `filter(fn(row) => row.age > 18)` |
| `join(table, on = cond)` | `JOIN` | `join(orders, on = users.id = orders.user_id)` |
| `group_by(cols...)` | `GROUP BY` | `group_by(department)` |
| `order_by(col dir)` | `ORDER BY` | `order_by(name asc)` or `order_by(age desc)` |
| `having(cond)` | `HAVING` | `having(count(*) > 5)` |
| `limit(n)` | `LIMIT` | `limit(10)` |
| `offset(n)` | `OFFSET` | `offset(20)` |
| `union` | `UNION` | query1 `|> union |>` query2 |
| `insert(target)` | `INSERT INTO...SELECT` | `|> insert(archive_users)` |
| `distinct` | `DISTINCT` | `select(distinct name)` |

## Aggregate Functions

`count(*)`, `count(col)`, `sum(col)`, `avg(col)`, `max(col)`, `min(col)`

## String Functions

`concat(a, b)`, `substring(col, start, len)`, `length(col)`, `trim(col)`, `upper(col)`, `lower(col)`

## Math Functions

`round(col, decimals)`, `floor(col)`, `ceil(col)`, `abs(col)`, `sqrt(col)`

## Let Bindings

```
let active_users = users |> filter(fn(row) => row.status = 'active') in
active_users |> select(name, email)
```

## Lambda Functions

```
fn(row) => row.column > value
fn(row) => row.price * 0.1
```

## Operators

Comparison: `=`, `!=`, `>`, `<`, `>=`, `<=`
Logical: `and`, `or`, `not`
Arithmetic: `+`, `-`, `*`, `/`
Other: `is null`, `is not null`, `in`, `like`, `exists`

## Case Expressions

```
case when condition then value else other_value end
```

## Qualified Column Names

Access columns via `table.column`: `users.name`, `orders.total`

## Identifier Rules

- Case-insensitive (transpiles to lowercase)
- Cannot start with a number
- Never quoted in output SQL

## Examples

```lql
-- Simple query
users |> filter(fn(row) => row.age > 18) |> select(name, email)

-- Join with aggregation
orders
|> join(customers, on = orders.customer_id = customers.id)
|> group_by(customers.name)
|> select(customers.name, sum(orders.total) as total_spent)
|> order_by(total_spent desc)

-- Let binding with filter
let vip = customers |> filter(fn(row) => row.tier = 'gold') in
vip |> select(name, email) |> order_by(name asc)

-- Arithmetic expressions
products
|> select(name, price, price * 0.1 as tax, price * 1.1 as total)
|> filter(fn(row) => row.total > 100)

-- Subquery with insert
users
|> filter(fn(row) => row.last_login < '2024-01-01')
|> select(id, name, email)
|> insert(inactive_users)
```

## Completion Context

When completing LQL code:
- After `|>`: suggest pipeline operations (select, filter, join, group_by, order_by, etc.)
- After `table.`: suggest column names for that table
- Inside `fn(row) =>`: suggest row.column expressions
- Inside `select(...)`: suggest column names, aggregate functions, expressions
- Inside `filter(...)`: suggest comparison expressions
- After `join(`: suggest table names
- After `order_by(`: suggest column names, then `asc`/`desc`
