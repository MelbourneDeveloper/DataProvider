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
| `select_distinct(cols...)` | `SELECT DISTINCT` | `select_distinct(name, email)` |
| `filter(fn(row) => cond)` | `WHERE` | `filter(fn(row) => row.age > 18)` |
| `join(table, on = cond)` | `INNER JOIN` | `join(orders, on = users.id = orders.user_id)` |
| `left_join(table, on = cond)` | `LEFT JOIN` | `left_join(orders, on = users.id = orders.user_id)` |
| `cross_join(table)` | `CROSS JOIN` | `cross_join(dates)` |
| `group_by(cols...)` | `GROUP BY` | `group_by(department)` |
| `having(cond)` | `HAVING` | `having(count(*) > 5)` |
| `order_by(col dir)` | `ORDER BY` | `order_by(name asc)` or `order_by(age desc)` |
| `limit(n)` | `LIMIT` | `limit(10)` |
| `offset(n)` | `OFFSET` | `offset(20)` |
| `union` | `UNION` | query1 `|> union |>` query2 |
| `union_all` | `UNION ALL` | query1 `|> union_all |>` query2 |
| `insert(target)` | `INSERT INTO...SELECT` | `|> insert(archive_users)` |

## Aggregate Functions

`count(*)`, `count(col)`, `sum(col)`, `avg(col)`, `max(col)`, `min(col)`

## String Functions

`concat(a, b)`, `substring(col, start, len)`, `length(col)`, `trim(col)`, `upper(col)`, `lower(col)`

## Math Functions

`round(col, decimals)`, `floor(col)`, `ceil(col)`, `abs(col)`, `sqrt(col)`

## Date/Time Functions

`current_date()`, `extract(part, col)`, `date_trunc(part, col)`

## Window Functions

Window functions use the `OVER` clause with optional `PARTITION BY` and `ORDER BY`:

```lql
employees
|> select(
    name,
    department,
    salary,
    row_number() over (partition by department order by salary desc) as rank
)
```

Available window functions: `row_number()`, `rank()`, `dense_rank()`, `lag(col)`, `lead(col)`

## Other Functions

`coalesce(a, b, ...)`, `exists(subquery)`

## Subquery Operators

### EXISTS

```lql
customers
|> filter(fn(row) => exists(
    orders |> filter(fn(o) => o.customer_id = row.id)
))
|> select(*)
```

### IN

```lql
users
|> filter(fn(row) => row.id in (
    orders |> select(user_id)
))
|> select(*)
```

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

Comparison: `=`, `!=`, `<>`, `>`, `<`, `>=`, `<=`, `like`
Logical: `and`, `or`, `not`
Arithmetic: `+`, `-`, `*`, `/`, `%`
String: `||` (concatenation)
Other: `is null`, `is not null`, `in`, `exists`

## Case Expressions

```
case when condition then value else other_value end
```

## Qualified Column Names

Access columns via `table.column`: `users.name`, `orders.total`

## Column Aliases

```lql
select(price * 1.1 as total, upper(name) as upper_name)
```

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

-- Left join
users
|> left_join(orders, on = users.id = orders.user_id)
|> select(users.name, orders.total)

-- Let binding with filter
let vip = customers |> filter(fn(row) => row.tier = 'gold') in
vip |> select(name, email) |> order_by(name asc)

-- Arithmetic expressions
products
|> select(name, price, price * 0.1 as tax, price * 1.1 as total)
|> filter(fn(row) => row.total > 100)

-- Window function
employees
|> select(
    name,
    department,
    salary,
    row_number() over (partition by department order by salary desc) as dept_rank
)

-- Union
active_users |> select(name, email)
|> union
|> archived_users |> select(name, email)

-- Pagination
users |> order_by(name asc) |> limit(10) |> offset(20) |> select(*)

-- Subquery with insert
users
|> filter(fn(row) => row.last_login < '2024-01-01')
|> select(id, name, email)
|> insert(inactive_users)

-- Exists subquery
customers
|> filter(fn(row) => exists(
    orders |> filter(fn(o) => o.customer_id = row.id)
))
|> select(name, email)
```

## Completion Context

When completing LQL code:
- After `|>`: suggest pipeline operations (select, filter, join, left_join, cross_join, group_by, order_by, having, limit, offset, union, union_all, insert, select_distinct)
- After `table.`: suggest column names for that table
- Inside `fn(row) =>`: suggest row.column expressions
- Inside `select(...)`: suggest column names, aggregate functions, window functions, expressions
- Inside `filter(...)`: suggest comparison expressions
- After `join(` or `left_join(`: suggest table names
- After `order_by(`: suggest column names, then `asc`/`desc`
- After `over (`: suggest `partition by` and `order by`
