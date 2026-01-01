namespace Migration;

/// <summary>
/// Calculates the difference between two schema definitions.
/// Produces a list of operations to transform current schema into desired schema.
/// </summary>
/// <example>
/// <code>
/// // Compare current database schema against desired schema
/// var currentSchema = await schemaInspector.InspectAsync(connection);
/// var desiredSchema = Schema.Define("mydb")
///     .Table("users", t => t
///         .Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
///         .Column("email", PortableTypes.VarChar(255), c => c.NotNull())
///         .Column("name", PortableTypes.VarChar(100)))  // New column
///     .Build();
///
/// // Calculate safe (additive-only) migration operations
/// var result = SchemaDiff.Calculate(currentSchema, desiredSchema);
/// if (result is OperationsResult.Ok&lt;IReadOnlyList&lt;SchemaOperation&gt;, MigrationError&gt; ok)
/// {
///     foreach (var op in ok.Value)
///     {
///         var ddl = ddlGenerator.Generate(op);
///         await connection.ExecuteAsync(ddl);
///     }
/// }
///
/// // Or allow destructive changes (DROP operations)
/// var destructiveResult = SchemaDiff.Calculate(
///     currentSchema, desiredSchema, allowDestructive: true);
/// </code>
/// </example>
public static class SchemaDiff
{
    /// <summary>
    /// Calculate operations needed to transform current schema into desired schema.
    /// By default, only produces additive operations (safe upgrades).
    /// </summary>
    /// <param name="current">Current database schema</param>
    /// <param name="desired">Desired target schema</param>
    /// <param name="allowDestructive">If true, include DROP operations</param>
    /// <param name="logger">Logger for diagnostics</param>
    /// <returns>List of operations to apply</returns>
    public static OperationsResult Calculate(
        SchemaDefinition current,
        SchemaDefinition desired,
        bool allowDestructive = false,
        ILogger? logger = null
    )
    {
        try
        {
            var operations = new List<SchemaOperation>();

            // Use table name only for matching (schema-agnostic comparison)
            // This handles differences between SQLite (main) and Postgres (public) default schemas
            var currentTables = current.Tables.ToDictionary(t => t.Name.ToUpperInvariant());

            // Find tables to create or update
            foreach (var desiredTable in desired.Tables)
            {
                var key = desiredTable.Name.ToUpperInvariant();

                if (!currentTables.TryGetValue(key, out var currentTable))
                {
                    // Table doesn't exist - create it
                    logger?.LogDebug(
                        "Table {Schema}.{Table} not found, will create",
                        desiredTable.Schema,
                        desiredTable.Name
                    );
                    operations.Add(new CreateTableOperation(desiredTable));

                    // Also create operations for any indexes on the new table
                    foreach (var index in desiredTable.Indexes)
                    {
                        logger?.LogDebug(
                            "Creating index {Index} on new table {Schema}.{Table}",
                            index.Name,
                            desiredTable.Schema,
                            desiredTable.Name
                        );
                        operations.Add(
                            new CreateIndexOperation(desiredTable.Schema, desiredTable.Name, index)
                        );
                    }
                }
                else
                {
                    // Table exists - check for column additions
                    var columnOps = CalculateColumnDiff(
                        currentTable,
                        desiredTable,
                        allowDestructive,
                        logger
                    );
                    operations.AddRange(columnOps);

                    // Check for index additions
                    var indexOps = CalculateIndexDiff(
                        currentTable,
                        desiredTable,
                        allowDestructive,
                        logger
                    );
                    operations.AddRange(indexOps);

                    // Check for foreign key additions
                    var fkOps = CalculateForeignKeyDiff(
                        currentTable,
                        desiredTable,
                        allowDestructive,
                        logger
                    );
                    operations.AddRange(fkOps);
                }
            }

            // Find tables to drop (only if destructive allowed)
            if (allowDestructive)
            {
                // Use table name only for matching (schema-agnostic)
                var desiredTableNames = desired
                    .Tables.Select(t => t.Name.ToUpperInvariant())
                    .ToHashSet();

                foreach (var currentTable in current.Tables)
                {
                    var exists = desiredTableNames.Contains(currentTable.Name.ToUpperInvariant());

                    if (!exists)
                    {
                        logger?.LogWarning(
                            "Table {Schema}.{Table} will be DROPPED",
                            currentTable.Schema,
                            currentTable.Name
                        );
                        operations.Add(
                            new DropTableOperation(currentTable.Schema, currentTable.Name)
                        );
                    }
                }
            }

            return new OperationsResult.Ok<IReadOnlyList<SchemaOperation>, MigrationError>(
                operations.AsReadOnly()
            );
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error calculating schema diff");
            return new OperationsResult.Error<IReadOnlyList<SchemaOperation>, MigrationError>(
                MigrationError.FromException(ex)
            );
        }
    }

    private static IEnumerable<SchemaOperation> CalculateColumnDiff(
        TableDefinition current,
        TableDefinition desired,
        bool allowDestructive,
        ILogger? logger
    )
    {
        var currentColumns = current.Columns.ToDictionary(
            c => c.Name,
            StringComparer.OrdinalIgnoreCase
        );

        // Add new columns
        foreach (var desiredColumn in desired.Columns)
        {
            if (!currentColumns.ContainsKey(desiredColumn.Name))
            {
                logger?.LogDebug(
                    "Column {Schema}.{Table}.{Column} not found, will add",
                    desired.Schema,
                    desired.Name,
                    desiredColumn.Name
                );
                yield return new AddColumnOperation(desired.Schema, desired.Name, desiredColumn);
            }
        }

        // Drop removed columns (only if destructive allowed)
        if (allowDestructive)
        {
            var desiredColumns = desired.Columns.ToDictionary(
                c => c.Name,
                StringComparer.OrdinalIgnoreCase
            );

            foreach (var currentColumn in current.Columns)
            {
                if (!desiredColumns.ContainsKey(currentColumn.Name))
                {
                    logger?.LogWarning(
                        "Column {Schema}.{Table}.{Column} will be DROPPED",
                        current.Schema,
                        current.Name,
                        currentColumn.Name
                    );
                    yield return new DropColumnOperation(
                        current.Schema,
                        current.Name,
                        currentColumn.Name
                    );
                }
            }
        }
    }

    private static IEnumerable<SchemaOperation> CalculateIndexDiff(
        TableDefinition current,
        TableDefinition desired,
        bool allowDestructive,
        ILogger? logger
    )
    {
        var currentIndexes = current.Indexes.ToDictionary(
            i => i.Name,
            StringComparer.OrdinalIgnoreCase
        );

        // Add new indexes
        foreach (var desiredIndex in desired.Indexes)
        {
            if (!currentIndexes.ContainsKey(desiredIndex.Name))
            {
                logger?.LogDebug(
                    "Index {IndexName} on {Schema}.{Table} not found, will create",
                    desiredIndex.Name,
                    desired.Schema,
                    desired.Name
                );
                yield return new CreateIndexOperation(desired.Schema, desired.Name, desiredIndex);
            }
        }

        // Drop removed indexes (only if destructive allowed)
        if (allowDestructive)
        {
            var desiredIndexes = desired.Indexes.ToDictionary(
                i => i.Name,
                StringComparer.OrdinalIgnoreCase
            );

            foreach (var currentIndex in current.Indexes)
            {
                if (!desiredIndexes.ContainsKey(currentIndex.Name))
                {
                    logger?.LogDebug("Index {IndexName} will be dropped", currentIndex.Name);
                    yield return new DropIndexOperation(
                        current.Schema,
                        current.Name,
                        currentIndex.Name
                    );
                }
            }
        }
    }

    private static IEnumerable<SchemaOperation> CalculateForeignKeyDiff(
        TableDefinition current,
        TableDefinition desired,
        bool allowDestructive,
        ILogger? logger
    )
    {
        var currentFks = current
            .ForeignKeys.Where(fk => fk.Name is not null)
            .ToDictionary(fk => fk.Name!, StringComparer.OrdinalIgnoreCase);

        // Add new foreign keys
        foreach (var desiredFk in desired.ForeignKeys)
        {
            if (desiredFk.Name is not null && !currentFks.ContainsKey(desiredFk.Name))
            {
                logger?.LogDebug(
                    "Foreign key {FkName} on {Schema}.{Table} not found, will add",
                    desiredFk.Name,
                    desired.Schema,
                    desired.Name
                );
                yield return new AddForeignKeyOperation(desired.Schema, desired.Name, desiredFk);
            }
        }

        // Drop removed foreign keys (only if destructive allowed)
        if (allowDestructive)
        {
            var desiredFks = desired
                .ForeignKeys.Where(fk => fk.Name is not null)
                .ToDictionary(fk => fk.Name!, StringComparer.OrdinalIgnoreCase);

            foreach (var currentFk in current.ForeignKeys)
            {
                if (currentFk.Name is not null && !desiredFks.ContainsKey(currentFk.Name))
                {
                    logger?.LogDebug("Foreign key {FkName} will be dropped", currentFk.Name);
                    yield return new DropForeignKeyOperation(
                        current.Schema,
                        current.Name,
                        currentFk.Name
                    );
                }
            }
        }
    }
}
