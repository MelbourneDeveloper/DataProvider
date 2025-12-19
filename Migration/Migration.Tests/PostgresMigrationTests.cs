namespace Migration.Tests;

/// <summary>
/// E2E tests for PostgreSQL migrations using Testcontainers.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Usage",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "Disposed via IAsyncLifetime.DisposeAsync"
)]
public sealed class PostgresMigrationTests : IAsyncLifetime
{
    private PostgreSqlContainer _postgres = null!;
    private NpgsqlConnection _connection = null!;
    private readonly ILogger _logger = NullLogger.Instance;

    public async Task InitializeAsync()
    {
        _postgres = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("migration_test")
            .WithUsername("test")
            .WithPassword("test")
            .Build();

        await _postgres.StartAsync().ConfigureAwait(false);

        _connection = new NpgsqlConnection(_postgres.GetConnectionString());
        await _connection.OpenAsync().ConfigureAwait(false);
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync().ConfigureAwait(false);
        await _postgres.DisposeAsync().ConfigureAwait(false);
    }

    [Fact]
    public void CreateDatabaseFromScratch_SingleTable_Success()
    {
        // Arrange
        var schema = Schema
            .Define("Test")
            .Table(
                "public",
                "users",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("email", PortableTypes.VarChar(255), c => c.NotNull())
                        .Column("name", PortableTypes.VarChar(100))
                        .Index("idx_users_email", "email", unique: true)
            )
            .Build();

        // Act
        var emptySchema = PostgresSchemaInspector.Inspect(_connection, "public", _logger);
        Assert.True(emptySchema is SchemaResultOk);

        var operations = SchemaDiff.Calculate(
            ((SchemaResultOk)emptySchema).Value,
            schema,
            logger: _logger
        );
        Assert.True(operations is OperationsResultOk);

        var ops = ((OperationsResultOk)operations).Value;

        var result = MigrationRunner.Apply(
            _connection,
            ops,
            PostgresDdlGenerator.Generate,
            MigrationOptions.Default,
            _logger
        );

        // Assert
        Assert.True(
            result is MigrationApplyResultOk,
            $"Migration failed: {(result as MigrationApplyResultError)?.Value}"
        );

        // Verify table exists
        var inspected = PostgresSchemaInspector.Inspect(_connection, "public", _logger);
        Assert.True(inspected is SchemaResultOk);
        var inspectedSchema = ((SchemaResultOk)inspected).Value;
        Assert.Contains(inspectedSchema.Tables, t => t.Name == "users");
    }

    [Fact]
    public void CreateDatabaseFromScratch_MultipleTablesWithForeignKeys_Success()
    {
        // Arrange
        var schema = Schema
            .Define("Test")
            .Table(
                "public",
                "customers",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("email", PortableTypes.VarChar(255), c => c.NotNull())
            )
            .Table(
                "public",
                "invoices",
                t =>
                    t.Column("id", PortableTypes.BigInt, c => c.PrimaryKey().Identity())
                        .Column("customer_id", PortableTypes.Uuid, c => c.NotNull())
                        .Column("total", PortableTypes.Decimal(12, 2), c => c.NotNull())
                        .Column(
                            "created_at",
                            PortableTypes.DateTimeOffset,
                            c => c.NotNull().Default("CURRENT_TIMESTAMP")
                        )
                        .ForeignKey("customer_id", "customers", "id", ForeignKeyAction.Cascade)
            )
            .Build();

        // Act
        var emptySchema = (
            (SchemaResultOk)PostgresSchemaInspector.Inspect(_connection, "public", _logger)
        ).Value;

        var operations = (
            (OperationsResultOk)SchemaDiff.Calculate(emptySchema, schema, logger: _logger)
        ).Value;

        var result = MigrationRunner.Apply(
            _connection,
            operations,
            PostgresDdlGenerator.Generate,
            MigrationOptions.Default,
            _logger
        );

        // Assert
        Assert.True(
            result is MigrationApplyResultOk,
            $"Migration failed: {(result as MigrationApplyResultError)?.Value}"
        );

        var inspected = (
            (SchemaResultOk)PostgresSchemaInspector.Inspect(_connection, "public", _logger)
        ).Value;

        Assert.Contains(inspected.Tables, t => t.Name == "customers");
        Assert.Contains(inspected.Tables, t => t.Name == "invoices");

        var invoicesTable = inspected.Tables.First(t => t.Name == "invoices");
        Assert.NotEmpty(invoicesTable.ForeignKeys);
    }

    [Fact]
    public void UpgradeExistingDatabase_AddColumn_Success()
    {
        // Arrange - Create initial schema
        var v1 = Schema
            .Define("Test")
            .Table(
                "public",
                "products",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("name", PortableTypes.VarChar(200))
            )
            .Build();

        var emptySchema = (
            (SchemaResultOk)PostgresSchemaInspector.Inspect(_connection, "public", _logger)
        ).Value;
        var v1Ops = (
            (OperationsResultOk)SchemaDiff.Calculate(emptySchema, v1, logger: _logger)
        ).Value;
        _ = MigrationRunner.Apply(
            _connection,
            v1Ops,
            PostgresDdlGenerator.Generate,
            MigrationOptions.Default,
            _logger
        );

        // Define v2 with new columns
        var v2 = Schema
            .Define("Test")
            .Table(
                "public",
                "products",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("name", PortableTypes.VarChar(200))
                        .Column("price", PortableTypes.Decimal(10, 2))
                        .Column("sku", PortableTypes.VarChar(50))
            )
            .Build();

        // Act
        var currentSchema = (
            (SchemaResultOk)PostgresSchemaInspector.Inspect(_connection, "public", _logger)
        ).Value;

        var upgradeOps = (
            (OperationsResultOk)SchemaDiff.Calculate(currentSchema, v2, logger: _logger)
        ).Value;

        // Should have 2 AddColumn operations
        Assert.Equal(2, upgradeOps.Count);
        Assert.All(upgradeOps, op => Assert.IsType<AddColumnOperation>(op));

        var result = MigrationRunner.Apply(
            _connection,
            upgradeOps,
            PostgresDdlGenerator.Generate,
            MigrationOptions.Default,
            _logger
        );

        // Assert
        Assert.True(result is MigrationApplyResultOk);

        var finalSchema = (
            (SchemaResultOk)PostgresSchemaInspector.Inspect(_connection, "public", _logger)
        ).Value;
        var products = finalSchema.Tables.Single(t => t.Name == "products");
        Assert.Equal(4, products.Columns.Count);
    }

    [Fact]
    public void UpgradeExistingDatabase_AddTable_Success()
    {
        // Arrange
        var v1 = Schema
            .Define("Test")
            .Table(
                "public",
                "categories",
                t =>
                    t.Column("id", PortableTypes.Int, c => c.PrimaryKey().Identity())
                        .Column("name", PortableTypes.VarChar(100))
            )
            .Build();

        var emptySchema = (
            (SchemaResultOk)PostgresSchemaInspector.Inspect(_connection, "public", _logger)
        ).Value;
        var v1Ops = (
            (OperationsResultOk)SchemaDiff.Calculate(emptySchema, v1, logger: _logger)
        ).Value;
        _ = MigrationRunner.Apply(
            _connection,
            v1Ops,
            PostgresDdlGenerator.Generate,
            MigrationOptions.Default,
            _logger
        );

        // v2 adds a new table
        var v2 = Schema
            .Define("Test")
            .Table(
                "public",
                "categories",
                t =>
                    t.Column("id", PortableTypes.Int, c => c.PrimaryKey().Identity())
                        .Column("name", PortableTypes.VarChar(100))
            )
            .Table(
                "public",
                "items",
                t =>
                    t.Column("id", PortableTypes.BigInt, c => c.PrimaryKey().Identity())
                        .Column("category_id", PortableTypes.Int, c => c.NotNull())
                        .Column("title", PortableTypes.VarChar(300), c => c.NotNull())
                        .ForeignKey("category_id", "categories", "id")
            )
            .Build();

        // Act
        var currentSchema = (
            (SchemaResultOk)PostgresSchemaInspector.Inspect(_connection, "public", _logger)
        ).Value;

        var upgradeOps = (
            (OperationsResultOk)SchemaDiff.Calculate(currentSchema, v2, logger: _logger)
        ).Value;

        Assert.Single(upgradeOps);
        Assert.IsType<CreateTableOperation>(upgradeOps[0]);

        var result = MigrationRunner.Apply(
            _connection,
            upgradeOps,
            PostgresDdlGenerator.Generate,
            MigrationOptions.Default,
            _logger
        );

        // Assert
        Assert.True(result is MigrationApplyResultOk);

        var finalSchema = (
            (SchemaResultOk)PostgresSchemaInspector.Inspect(_connection, "public", _logger)
        ).Value;
        Assert.Contains(finalSchema.Tables, t => t.Name == "items");
    }

    [Fact]
    public void UpgradeExistingDatabase_AddIndex_Success()
    {
        // Arrange
        var v1 = Schema
            .Define("Test")
            .Table(
                "public",
                "logs",
                t =>
                    t.Column("id", PortableTypes.BigInt, c => c.PrimaryKey().Identity())
                        .Column("message", PortableTypes.Text)
                        .Column("level", PortableTypes.VarChar(20))
            )
            .Build();

        var emptySchema = (
            (SchemaResultOk)PostgresSchemaInspector.Inspect(_connection, "public", _logger)
        ).Value;
        var v1Ops = (
            (OperationsResultOk)SchemaDiff.Calculate(emptySchema, v1, logger: _logger)
        ).Value;
        _ = MigrationRunner.Apply(
            _connection,
            v1Ops,
            PostgresDdlGenerator.Generate,
            MigrationOptions.Default,
            _logger
        );

        // v2 adds an index
        var v2 = Schema
            .Define("Test")
            .Table(
                "public",
                "logs",
                t =>
                    t.Column("id", PortableTypes.BigInt, c => c.PrimaryKey().Identity())
                        .Column("message", PortableTypes.Text)
                        .Column("level", PortableTypes.VarChar(20))
                        .Index("idx_logs_level", "level")
            )
            .Build();

        // Act
        var currentSchema = (
            (SchemaResultOk)PostgresSchemaInspector.Inspect(_connection, "public", _logger)
        ).Value;

        var upgradeOps = (
            (OperationsResultOk)SchemaDiff.Calculate(currentSchema, v2, logger: _logger)
        ).Value;

        Assert.Single(upgradeOps);
        Assert.IsType<CreateIndexOperation>(upgradeOps[0]);

        var result = MigrationRunner.Apply(
            _connection,
            upgradeOps,
            PostgresDdlGenerator.Generate,
            MigrationOptions.Default,
            _logger
        );

        // Assert
        Assert.True(result is MigrationApplyResultOk);

        var finalSchema = (
            (SchemaResultOk)PostgresSchemaInspector.Inspect(_connection, "public", _logger)
        ).Value;
        var logs = finalSchema.Tables.Single(t => t.Name == "logs");
        Assert.Contains(logs.Indexes, i => i.Name == "idx_logs_level");
    }

    [Fact]
    public void Migration_IsIdempotent_NoErrorOnRerun()
    {
        // Arrange
        var schema = Schema
            .Define("Test")
            .Table(
                "public",
                "settings",
                t =>
                    t.Column("key", PortableTypes.VarChar(100), c => c.PrimaryKey())
                        .Column("value", PortableTypes.Text)
            )
            .Build();

        // Act - Run migration twice
        for (var i = 0; i < 2; i++)
        {
            var currentSchema = (
                (SchemaResultOk)PostgresSchemaInspector.Inspect(_connection, "public", _logger)
            ).Value;

            var operations = (
                (OperationsResultOk)SchemaDiff.Calculate(currentSchema, schema, logger: _logger)
            ).Value;

            var result = MigrationRunner.Apply(
                _connection,
                operations,
                PostgresDdlGenerator.Generate,
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
    public void CreateTable_PostgresNativeTypes_Success()
    {
        // Arrange
        var schema = Schema
            .Define("Test")
            .Table(
                "public",
                "type_test",
                t =>
                    t.Column("id", PortableTypes.BigInt, c => c.PrimaryKey())
                        .Column("uuid_col", PortableTypes.Uuid)
                        .Column("json_col", PortableTypes.Json)
                        .Column("bool_col", PortableTypes.Boolean)
                        .Column("timestamp_col", PortableTypes.DateTimeOffset)
                        .Column("decimal_col", PortableTypes.Decimal(18, 4))
            )
            .Build();

        // Act
        var emptySchema = (
            (SchemaResultOk)PostgresSchemaInspector.Inspect(_connection, "public", _logger)
        ).Value;
        var operations = (
            (OperationsResultOk)SchemaDiff.Calculate(emptySchema, schema, logger: _logger)
        ).Value;

        var result = MigrationRunner.Apply(
            _connection,
            operations,
            PostgresDdlGenerator.Generate,
            MigrationOptions.Default,
            _logger
        );

        // Assert
        Assert.True(result is MigrationApplyResultOk);

        // Verify native types are used
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT column_name, data_type
            FROM information_schema.columns
            WHERE table_name = 'type_test'
            """;
        using var reader = cmd.ExecuteReader();

        var columns = new Dictionary<string, string>();
        while (reader.Read())
        {
            columns[reader.GetString(0)] = reader.GetString(1);
        }

        Assert.Equal("uuid", columns["uuid_col"]);
        Assert.Equal("jsonb", columns["json_col"]);
        Assert.Equal("boolean", columns["bool_col"]);
        Assert.Contains("timestamp", columns["timestamp_col"]);
    }

    [Fact]
    public void Destructive_DropTable_AllowedWithOption()
    {
        // Arrange - Create initial tables
        var v1 = Schema
            .Define("Test")
            .Table(
                "public",
                "keepers",
                t => t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
            )
            .Table("public", "dropme", t => t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey()))
            .Build();

        var emptySchema = (
            (SchemaResultOk)PostgresSchemaInspector.Inspect(_connection, "public", _logger)
        ).Value;
        var v1Ops = (
            (OperationsResultOk)SchemaDiff.Calculate(emptySchema, v1, logger: _logger)
        ).Value;
        _ = MigrationRunner.Apply(
            _connection,
            v1Ops,
            PostgresDdlGenerator.Generate,
            MigrationOptions.Default,
            _logger
        );

        // v2 removes dropme table
        var v2 = Schema
            .Define("Test")
            .Table(
                "public",
                "keepers",
                t => t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
            )
            .Build();

        // Act
        var currentSchema = (
            (SchemaResultOk)PostgresSchemaInspector.Inspect(_connection, "public", _logger)
        ).Value;

        var operations = (
            (OperationsResultOk)
                SchemaDiff.Calculate(currentSchema, v2, allowDestructive: true, logger: _logger)
        ).Value;

        Assert.Single(operations);
        Assert.IsType<DropTableOperation>(operations[0]);

        var result = MigrationRunner.Apply(
            _connection,
            operations,
            PostgresDdlGenerator.Generate,
            MigrationOptions.Destructive,
            _logger
        );

        // Assert
        Assert.True(result is MigrationApplyResultOk);

        var finalSchema = (
            (SchemaResultOk)PostgresSchemaInspector.Inspect(_connection, "public", _logger)
        ).Value;
        Assert.DoesNotContain(finalSchema.Tables, t => t.Name == "dropme");
        Assert.Contains(finalSchema.Tables, t => t.Name == "keepers");
    }
}
