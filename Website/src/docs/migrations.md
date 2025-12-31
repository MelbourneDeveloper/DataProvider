---
layout: layouts/docs.njk
title: Migrations
description: YAML-based database migrations with the Migration CLI.
---

## Overview

DataProvider uses YAML-based migrations to manage database schema changes. The Migration CLI tool applies migrations to your database.

## Creating Migrations

Create a YAML file in your migrations folder:

```yaml
# migrations/001_create_orders.yaml
up:
  - CREATE TABLE Orders (
      Id INT PRIMARY KEY IDENTITY,
      Name NVARCHAR(255) NOT NULL,
      Status NVARCHAR(50) NOT NULL,
      CreatedAt DATETIME2 DEFAULT GETUTCDATE()
    )

down:
  - DROP TABLE Orders
```

## Running Migrations

Use the Migration CLI:

```bash
# Apply all pending migrations
dotnet migration up

# Rollback the last migration
dotnet migration down

# Check migration status
dotnet migration status
```

## Migration Naming

Migrations are applied in alphabetical order. Use numbered prefixes:

```
001_create_users.yaml
002_create_orders.yaml
003_add_order_items.yaml
```

## Environment Variables

Configure your database connection:

```bash
export MIGRATION_CONNECTION_STRING="Server=localhost;Database=MyDb;..."
```

## Next Steps

- [DataProvider Documentation](/docs/dataprovider/)
- [Getting Started](/docs/getting-started/)
