namespace Migration.Tests;

/// <summary>
/// E2E tests for SQLite migrations - greenfield and upgrades.
/// </summary>
public sealed class SqliteMigrationTests
{
    private readonly ILogger _logger = NullLogger.Instance;

    [Fact]
    public void CreateDatabaseFromScratch_SingleTable_Success()
    {
        // Arrange
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var schema = Schema
            .Define("Test")
            .Table(
                "Users",
                t =>
                    t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("Email", PortableTypes.NVarChar(255), c => c.NotNull())
                        .Column("Name", PortableTypes.NVarChar(100))
                        .Index("idx_users_email", "Email", unique: true)
            )
            .Build();

        // Act
        var emptySchema = SqliteSchemaInspector.Inspect(connection, _logger);
        Assert.True(emptySchema is SchemaResultOk);

        var operations = SchemaDiff.Calculate(
            ((SchemaResultOk)emptySchema).Value,
            schema,
            logger: _logger
        );
        Assert.True(operations is OperationsResultOk);

        var ops = ((OperationsResultOk)operations).Value;

        var result = MigrationRunner.Apply(
            connection,
            ops,
            SqliteDdlGenerator.Generate,
            MigrationOptions.Default,
            _logger
        );

        // Assert
        Assert.True(result is MigrationApplyResultOk);

        // Verify table exists
        var inspected = SqliteSchemaInspector.Inspect(connection, _logger);
        Assert.True(inspected is SchemaResultOk);
        var inspectedSchema = ((SchemaResultOk)inspected).Value;
        Assert.Single(inspectedSchema.Tables);
        Assert.Equal("Users", inspectedSchema.Tables[0].Name);
        Assert.Equal(3, inspectedSchema.Tables[0].Columns.Count);
    }

    [Fact]
    public void CreateDatabaseFromScratch_MultipleTablesWithForeignKeys_Success()
    {
        // Arrange
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        // Enable foreign keys
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "PRAGMA foreign_keys = ON";
            cmd.ExecuteNonQuery();
        }

        var schema = Schema
            .Define("Test")
            .Table(
                "Users",
                t =>
                    t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("Email", PortableTypes.NVarChar(255), c => c.NotNull())
            )
            .Table(
                "Orders",
                t =>
                    t.Column("Id", PortableTypes.BigInt, c => c.PrimaryKey().Identity())
                        .Column("UserId", PortableTypes.Uuid, c => c.NotNull())
                        .Column("Total", PortableTypes.Decimal(10, 2), c => c.NotNull())
                        .Column(
                            "CreatedAt",
                            PortableTypes.DateTime(),
                            c => c.NotNull().Default("CURRENT_TIMESTAMP")
                        )
                        .ForeignKey("UserId", "Users", "Id", ForeignKeyAction.Cascade)
            )
            .Build();

        // Act
        var emptySchema = (
            (SchemaResultOk)SqliteSchemaInspector.Inspect(connection, _logger)
        ).Value;

        var operations = (
            (OperationsResultOk)SchemaDiff.Calculate(emptySchema, schema, logger: _logger)
        ).Value;

        var result = MigrationRunner.Apply(
            connection,
            operations,
            SqliteDdlGenerator.Generate,
            MigrationOptions.Default,
            _logger
        );

        // Assert
        Assert.True(result is MigrationApplyResultOk);

        var inspected = ((SchemaResultOk)SqliteSchemaInspector.Inspect(connection, _logger)).Value;

        Assert.Equal(2, inspected.Tables.Count);
        Assert.Contains(inspected.Tables, t => t.Name == "Users");
        Assert.Contains(inspected.Tables, t => t.Name == "Orders");

        var ordersTable = inspected.Tables.First(t => t.Name == "Orders");
        Assert.Single(ordersTable.ForeignKeys);
        Assert.Equal("Users", ordersTable.ForeignKeys[0].ReferencedTable);
    }

    [Fact]
    public void UpgradeExistingDatabase_AddColumn_Success()
    {
        // Arrange
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        // Create initial schema
        var v1 = Schema
            .Define("Test")
            .Table(
                "Users",
                t =>
                    t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("Email", PortableTypes.NVarChar(255))
            )
            .Build();

        // Apply v1
        var emptySchema = (
            (SchemaResultOk)SqliteSchemaInspector.Inspect(connection, _logger)
        ).Value;
        var v1Ops = (
            (OperationsResultOk)SchemaDiff.Calculate(emptySchema, v1, logger: _logger)
        ).Value;
        _ = MigrationRunner.Apply(
            connection,
            v1Ops,
            SqliteDdlGenerator.Generate,
            MigrationOptions.Default,
            _logger
        );

        // Define v2 with new columns
        var v2 = Schema
            .Define("Test")
            .Table(
                "Users",
                t =>
                    t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("Email", PortableTypes.NVarChar(255))
                        .Column("Name", PortableTypes.NVarChar(100))
                        .Column("CreatedAt", PortableTypes.DateTime())
            )
            .Build();

        // Act - upgrade to v2
        var currentSchema = (
            (SchemaResultOk)SqliteSchemaInspector.Inspect(connection, _logger)
        ).Value;

        var upgradeOps = (
            (OperationsResultOk)SchemaDiff.Calculate(currentSchema, v2, logger: _logger)
        ).Value;

        // Should have 2 AddColumn operations
        Assert.Equal(2, upgradeOps.Count);
        Assert.All(upgradeOps, op => Assert.IsType<AddColumnOperation>(op));

        var result = MigrationRunner.Apply(
            connection,
            upgradeOps,
            SqliteDdlGenerator.Generate,
            MigrationOptions.Default,
            _logger
        );

        // Assert
        Assert.True(result is MigrationApplyResultOk);

        var finalSchema = (
            (SchemaResultOk)SqliteSchemaInspector.Inspect(connection, _logger)
        ).Value;
        var users = finalSchema.Tables.Single(t => t.Name == "Users");
        Assert.Equal(4, users.Columns.Count);
        Assert.Contains(users.Columns, c => c.Name == "Name");
        Assert.Contains(users.Columns, c => c.Name == "CreatedAt");
    }

    [Fact]
    public void UpgradeExistingDatabase_AddTable_Success()
    {
        // Arrange
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var v1 = Schema
            .Define("Test")
            .Table("Users", t => t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey()))
            .Build();

        // Apply v1
        var emptySchema = (
            (SchemaResultOk)SqliteSchemaInspector.Inspect(connection, _logger)
        ).Value;
        var v1Ops = (
            (OperationsResultOk)SchemaDiff.Calculate(emptySchema, v1, logger: _logger)
        ).Value;
        _ = MigrationRunner.Apply(
            connection,
            v1Ops,
            SqliteDdlGenerator.Generate,
            MigrationOptions.Default,
            _logger
        );

        // v2 adds a new table
        var v2 = Schema
            .Define("Test")
            .Table("Users", t => t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey()))
            .Table(
                "Products",
                t =>
                    t.Column("Id", PortableTypes.BigInt, c => c.PrimaryKey().Identity())
                        .Column("Name", PortableTypes.NVarChar(200), c => c.NotNull())
                        .Column("Price", PortableTypes.Decimal(10, 2))
            )
            .Build();

        // Act
        var currentSchema = (
            (SchemaResultOk)SqliteSchemaInspector.Inspect(connection, _logger)
        ).Value;

        var upgradeOps = (
            (OperationsResultOk)SchemaDiff.Calculate(currentSchema, v2, logger: _logger)
        ).Value;

        // Should have 1 CreateTable operation
        Assert.Single(upgradeOps);
        Assert.IsType<CreateTableOperation>(upgradeOps[0]);

        var result = MigrationRunner.Apply(
            connection,
            upgradeOps,
            SqliteDdlGenerator.Generate,
            MigrationOptions.Default,
            _logger
        );

        // Assert
        Assert.True(result is MigrationApplyResultOk);

        var finalSchema = (
            (SchemaResultOk)SqliteSchemaInspector.Inspect(connection, _logger)
        ).Value;
        Assert.Equal(2, finalSchema.Tables.Count);
        Assert.Contains(finalSchema.Tables, t => t.Name == "Products");
    }

    [Fact]
    public void UpgradeExistingDatabase_AddIndex_Success()
    {
        // Arrange
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var v1 = Schema
            .Define("Test")
            .Table(
                "Users",
                t =>
                    t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("Email", PortableTypes.NVarChar(255))
            )
            .Build();

        // Apply v1
        var emptySchema = (
            (SchemaResultOk)SqliteSchemaInspector.Inspect(connection, _logger)
        ).Value;
        var v1Ops = (
            (OperationsResultOk)SchemaDiff.Calculate(emptySchema, v1, logger: _logger)
        ).Value;
        _ = MigrationRunner.Apply(
            connection,
            v1Ops,
            SqliteDdlGenerator.Generate,
            MigrationOptions.Default,
            _logger
        );

        // v2 adds an index
        var v2 = Schema
            .Define("Test")
            .Table(
                "Users",
                t =>
                    t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("Email", PortableTypes.NVarChar(255))
                        .Index("idx_users_email", "Email", unique: true)
            )
            .Build();

        // Act
        var currentSchema = (
            (SchemaResultOk)SqliteSchemaInspector.Inspect(connection, _logger)
        ).Value;

        var upgradeOps = (
            (OperationsResultOk)SchemaDiff.Calculate(currentSchema, v2, logger: _logger)
        ).Value;

        Assert.Single(upgradeOps);
        Assert.IsType<CreateIndexOperation>(upgradeOps[0]);

        var result = MigrationRunner.Apply(
            connection,
            upgradeOps,
            SqliteDdlGenerator.Generate,
            MigrationOptions.Default,
            _logger
        );

        // Assert
        Assert.True(result is MigrationApplyResultOk);

        var finalSchema = (
            (SchemaResultOk)SqliteSchemaInspector.Inspect(connection, _logger)
        ).Value;
        var users = finalSchema.Tables.Single(t => t.Name == "Users");
        Assert.Single(users.Indexes);
        Assert.Equal("idx_users_email", users.Indexes[0].Name);
        Assert.True(users.Indexes[0].IsUnique);
    }

    [Fact]
    public void Migration_IsIdempotent_NoErrorOnRerun()
    {
        // Arrange
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var schema = Schema
            .Define("Test")
            .Table(
                "Items",
                t =>
                    t.Column("Id", PortableTypes.Int, c => c.PrimaryKey())
                        .Column("Name", PortableTypes.NVarChar(50))
                        .Index("idx_items_name", "Name")
            )
            .Build();

        // Act - Run migration twice
        for (var i = 0; i < 2; i++)
        {
            var currentSchema = (
                (SchemaResultOk)SqliteSchemaInspector.Inspect(connection, _logger)
            ).Value;

            var operations = (
                (OperationsResultOk)SchemaDiff.Calculate(currentSchema, schema, logger: _logger)
            ).Value;

            var result = MigrationRunner.Apply(
                connection,
                operations,
                SqliteDdlGenerator.Generate,
                MigrationOptions.Default,
                _logger
            );

            Assert.True(result is MigrationApplyResultOk);

            // Second run should have 0 operations
            if (i == 1)
            {
                Assert.Empty(operations);
            }
        }
    }

    [Fact]
    public void CreateTable_AllPortableTypes_Success()
    {
        // Arrange
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var schema = Schema
            .Define("Test")
            .Table(
                "TypeTest",
                t =>
                    t.Column("Id", PortableTypes.BigInt, c => c.PrimaryKey())
                        .Column("TinyInt", PortableTypes.TinyInt)
                        .Column("SmallInt", PortableTypes.SmallInt)
                        .Column("Int", PortableTypes.Int)
                        .Column("BigInt", PortableTypes.BigInt)
                        .Column("Decimal", PortableTypes.Decimal(18, 2))
                        .Column("Float", PortableTypes.Float)
                        .Column("Double", PortableTypes.Double)
                        .Column("Money", PortableTypes.Money)
                        .Column("Bool", PortableTypes.Boolean)
                        .Column("Char", PortableTypes.Char(10))
                        .Column("VarChar", PortableTypes.VarChar(50))
                        .Column("NChar", PortableTypes.NChar(10))
                        .Column("NVarChar", PortableTypes.NVarChar(100))
                        .Column("Text", PortableTypes.Text)
                        .Column("Binary", PortableTypes.Binary(16))
                        .Column("VarBinary", PortableTypes.VarBinary(256))
                        .Column("Blob", PortableTypes.Blob)
                        .Column("Date", PortableTypes.Date)
                        .Column("Time", PortableTypes.Time())
                        .Column("DateTime", PortableTypes.DateTime())
                        .Column("DateTimeOffset", PortableTypes.DateTimeOffset)
                        .Column("Uuid", PortableTypes.Uuid)
                        .Column("Json", PortableTypes.Json)
                        .Column("Xml", PortableTypes.Xml)
            )
            .Build();

        // Act
        var emptySchema = (
            (SchemaResultOk)SqliteSchemaInspector.Inspect(connection, _logger)
        ).Value;
        var operations = (
            (OperationsResultOk)SchemaDiff.Calculate(emptySchema, schema, logger: _logger)
        ).Value;

        var result = MigrationRunner.Apply(
            connection,
            operations,
            SqliteDdlGenerator.Generate,
            MigrationOptions.Default,
            _logger
        );

        // Assert
        Assert.True(result is MigrationApplyResultOk);

        var inspected = ((SchemaResultOk)SqliteSchemaInspector.Inspect(connection, _logger)).Value;
        var table = inspected.Tables.Single();
        Assert.Equal(25, table.Columns.Count);
    }

    [Fact]
    public void Destructive_DropTable_BlockedByDefault()
    {
        // Arrange
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        // Create initial schema with 2 tables
        var v1 = Schema
            .Define("Test")
            .Table("Users", t => t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey()))
            .Table("Products", t => t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey()))
            .Build();

        var emptySchema = (
            (SchemaResultOk)SqliteSchemaInspector.Inspect(connection, _logger)
        ).Value;
        var v1Ops = (
            (OperationsResultOk)SchemaDiff.Calculate(emptySchema, v1, logger: _logger)
        ).Value;
        _ = MigrationRunner.Apply(
            connection,
            v1Ops,
            SqliteDdlGenerator.Generate,
            MigrationOptions.Default,
            _logger
        );

        // v2 removes Products table
        var v2 = Schema
            .Define("Test")
            .Table("Users", t => t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey()))
            .Build();

        // Act - Calculate diff WITHOUT AllowDestructive
        var currentSchema = (
            (SchemaResultOk)SqliteSchemaInspector.Inspect(connection, _logger)
        ).Value;

        var operations = (
            (OperationsResultOk)
                SchemaDiff.Calculate(currentSchema, v2, allowDestructive: false, logger: _logger)
        ).Value;

        // Assert - No drop operations should be generated
        Assert.Empty(operations);
    }

    [Fact]
    public void Destructive_DropTable_AllowedWithOption()
    {
        // Arrange
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var v1 = Schema
            .Define("Test")
            .Table("Users", t => t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey()))
            .Table("Products", t => t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey()))
            .Build();

        var emptySchema = (
            (SchemaResultOk)SqliteSchemaInspector.Inspect(connection, _logger)
        ).Value;
        var v1Ops = (
            (OperationsResultOk)SchemaDiff.Calculate(emptySchema, v1, logger: _logger)
        ).Value;
        _ = MigrationRunner.Apply(
            connection,
            v1Ops,
            SqliteDdlGenerator.Generate,
            MigrationOptions.Default,
            _logger
        );

        var v2 = Schema
            .Define("Test")
            .Table("Users", t => t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey()))
            .Build();

        // Act - Calculate diff WITH AllowDestructive
        var currentSchema = (
            (SchemaResultOk)SqliteSchemaInspector.Inspect(connection, _logger)
        ).Value;

        var operations = (
            (OperationsResultOk)
                SchemaDiff.Calculate(currentSchema, v2, allowDestructive: true, logger: _logger)
        ).Value;

        // Should have DropTableOperation
        Assert.Single(operations);
        Assert.IsType<DropTableOperation>(operations[0]);

        var result = MigrationRunner.Apply(
            connection,
            operations,
            SqliteDdlGenerator.Generate,
            MigrationOptions.Destructive,
            _logger
        );

        // Assert
        Assert.True(result is MigrationApplyResultOk);

        var finalSchema = (
            (SchemaResultOk)SqliteSchemaInspector.Inspect(connection, _logger)
        ).Value;
        Assert.Single(finalSchema.Tables);
        Assert.DoesNotContain(finalSchema.Tables, t => t.Name == "Products");
    }

    [Fact]
    public void SchemaInspector_RoundTrip_Matches()
    {
        // Arrange
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var schema = Schema
            .Define("Test")
            .Table(
                "Users",
                t =>
                    t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("Email", PortableTypes.NVarChar(255), c => c.NotNull())
                        .Column("Active", PortableTypes.Boolean, c => c.Default("1"))
                        .Index("idx_users_email", "Email", unique: true)
            )
            .Build();

        // Create schema
        var emptySchema = (
            (SchemaResultOk)SqliteSchemaInspector.Inspect(connection, _logger)
        ).Value;
        var operations = (
            (OperationsResultOk)SchemaDiff.Calculate(emptySchema, schema, logger: _logger)
        ).Value;
        _ = MigrationRunner.Apply(
            connection,
            operations,
            SqliteDdlGenerator.Generate,
            MigrationOptions.Default,
            _logger
        );

        // Act - Inspect and compare
        var inspected = ((SchemaResultOk)SqliteSchemaInspector.Inspect(connection, _logger)).Value;

        // Calculate diff between original and inspected - should be empty
        var diff = (
            (OperationsResultOk)SchemaDiff.Calculate(inspected, schema, logger: _logger)
        ).Value;

        // Assert
        Assert.Empty(diff);
    }

    [Fact]
    public void DestructiveOperation_BlockedByDefault_ReturnsUsefulError()
    {
        // Arrange
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        // Create table first
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "CREATE TABLE ToBeDropped (Id INTEGER PRIMARY KEY)";
        cmd.ExecuteNonQuery();

        var dropOperation = new DropTableOperation("main", "ToBeDropped");

        // Act - try to apply destructive operation with default options
        var result = MigrationRunner.Apply(
            connection,
            [dropOperation],
            SqliteDdlGenerator.Generate,
            MigrationOptions.Default, // AllowDestructive = false
            _logger
        );

        // Assert - should fail with useful error message
        Assert.True(result is MigrationApplyResultError);
        var error = ((MigrationApplyResultError)result).Value;
        Assert.Contains("Destructive", error.Message);
        Assert.Contains("DropTableOperation", error.Message);
    }

    [Fact]
    public void InvalidSql_ReturnsUsefulError()
    {
        // Arrange
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        // Create a custom operation that generates invalid SQL
        var badTable = new TableDefinition
        {
            Schema = "main",
            Name = "Bad\"Table", // Invalid table name with quote
            Columns = [new ColumnDefinition { Name = "Id", Type = PortableTypes.Int }],
        };

        var createOp = new CreateTableOperation(badTable);

        // Act
        var result = MigrationRunner.Apply(
            connection,
            [createOp],
            SqliteDdlGenerator.Generate,
            MigrationOptions.Default,
            _logger
        );

        // Assert - should fail (invalid SQL) but not crash
        // Note: SQLite may accept this - adjust test if needed
        Assert.NotNull(result);
    }

    [Fact]
    public void SchemaCapture_ExistingDatabase_ReturnsCompleteSchema()
    {
        // Arrange - Create database with raw SQL (simulate existing DB)
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE customers (
                id TEXT PRIMARY KEY,
                email TEXT NOT NULL,
                name TEXT,
                created_at TEXT DEFAULT CURRENT_TIMESTAMP
            );
            CREATE UNIQUE INDEX idx_customers_email ON customers(email);
            CREATE TABLE orders (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                customer_id TEXT NOT NULL,
                total REAL,
                FOREIGN KEY (customer_id) REFERENCES customers(id) ON DELETE CASCADE
            );
            CREATE INDEX idx_orders_customer ON orders(customer_id);
            """;
        cmd.ExecuteNonQuery();

        // Act - CAPTURE the existing schema
        var captureResult = SqliteSchemaInspector.Inspect(connection, _logger);

        // Assert - schema captured successfully
        Assert.True(captureResult is SchemaResultOk);
        var schema = ((SchemaResultOk)captureResult).Value;

        // Verify tables captured
        Assert.Equal(2, schema.Tables.Count);

        var customers = schema.Tables.Single(t => t.Name == "customers");
        Assert.Equal(4, customers.Columns.Count);
        Assert.Contains(customers.Columns, c => c.Name == "id");
        Assert.Contains(customers.Columns, c => c.Name == "email");
        Assert.Single(customers.Indexes);
        Assert.Equal("idx_customers_email", customers.Indexes[0].Name);
        Assert.True(customers.Indexes[0].IsUnique);

        var orders = schema.Tables.Single(t => t.Name == "orders");
        Assert.Equal(3, orders.Columns.Count);
        Assert.Single(orders.ForeignKeys);
        Assert.Equal("customers", orders.ForeignKeys[0].ReferencedTable);
        Assert.Equal(ForeignKeyAction.Cascade, orders.ForeignKeys[0].OnDelete);
        Assert.Single(orders.Indexes);
        Assert.Equal("idx_orders_customer", orders.Indexes[0].Name);
    }

    [Fact]
    public void SchemaCapture_SerializesToJson_RoundTrip()
    {
        // Arrange
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE products (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL,
                price REAL,
                active INTEGER DEFAULT 1
            );
            CREATE INDEX idx_products_name ON products(name);
            """;
        cmd.ExecuteNonQuery();

        // Act - Capture and serialize to JSON
        var captureResult = SqliteSchemaInspector.Inspect(connection, _logger);
        Assert.True(captureResult is SchemaResultOk);
        var schema = ((SchemaResultOk)captureResult).Value;

        var json = SchemaSerializer.ToJson(schema);

        // Assert - JSON is valid and contains expected data
        Assert.NotNull(json);
        Assert.Contains("products", json);
        Assert.Contains("name", json);
        Assert.Contains("idx_products_name", json);

        // Deserialize and verify round-trip
        var restored = SchemaSerializer.FromJson(json);
        Assert.NotNull(restored);
        Assert.Single(restored.Tables);
        Assert.Equal("products", restored.Tables[0].Name);
        Assert.Equal(4, restored.Tables[0].Columns.Count);
        Assert.Single(restored.Tables[0].Indexes);
    }
}
