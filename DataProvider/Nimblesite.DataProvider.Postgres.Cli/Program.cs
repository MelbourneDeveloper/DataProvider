using System.CommandLine;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Nimblesite.DataProvider.Migration.Core;
using Nimblesite.Sql.Model;
using Npgsql;
using Outcome;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes - records are instantiated by JSON deserialization
#pragma warning disable CA1849 // Call async methods when in an async method
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities

namespace Nimblesite.DataProvider.Postgres.Cli;

/// <summary>
/// PostgreSQL code generation CLI for Nimblesite.DataProvider.Core.
/// </summary>
internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Entry point.
    /// </summary>
    public static async Task<int> Main(string[] args)
    {
        var projectDir = new Option<DirectoryInfo>(
            "--project-dir",
            description: "Project directory containing sql files and Nimblesite.DataProvider.Core.json"
        )
        {
            IsRequired = true,
        };
        var config = new Option<FileInfo>(
            "--config",
            description: "Path to Nimblesite.DataProvider.Core.json"
        )
        {
            IsRequired = true,
        };
        var outDir = new Option<DirectoryInfo>(
            "--out",
            description: "Output directory for generated .g.cs files"
        )
        {
            IsRequired = true,
        };
        var offline = new Option<bool>(
            "--offline",
            description: "Run in offline mode without database connection (uses schema.yaml for metadata)"
        )
        {
            IsRequired = false,
        };
        var schemaFile = new Option<FileInfo?>(
            "--schema",
            description: "Path to schema.yaml file (required for offline mode)"
        )
        {
            IsRequired = false,
        };
        var root = new RootCommand("Nimblesite.DataProvider.Core.Postgres codegen CLI")
        {
            projectDir,
            config,
            outDir,
            offline,
            schemaFile,
        };
        root.SetHandler(
            async (
                DirectoryInfo proj,
                FileInfo cfg,
                DirectoryInfo output,
                bool off,
                FileInfo? schema
            ) =>
            {
                var exit = await RunAsync(proj, cfg, output, off, schema).ConfigureAwait(false);
                Environment.Exit(exit);
            },
            projectDir,
            config,
            outDir,
            offline,
            schemaFile
        );

        return await root.InvokeAsync(args).ConfigureAwait(false);
    }

    private static async Task<int> RunAsync(
        DirectoryInfo projectDir,
        FileInfo configFile,
        DirectoryInfo outDir,
        bool offline,
        FileInfo? schemaFile
    )
    {
        try
        {
            if (!configFile.Exists)
            {
                Console.WriteLine($"❌ Config not found: {configFile.FullName}");
                return 1;
            }

            if (!outDir.Exists)
                outDir.Create();

            var cfgText = await File.ReadAllTextAsync(configFile.FullName).ConfigureAwait(false);
            var cfg = JsonSerializer.Deserialize<PostgresDataProviderConfig>(cfgText, JsonOptions);
            if (cfg is null)
            {
                Console.WriteLine("❌ Nimblesite.DataProvider.Core.json is invalid");
                return 1;
            }

            // Load schema if provided (required for offline mode)
            SchemaDefinition? schema = null;
            if (schemaFile?.Exists == true)
            {
                var schemaYaml = await File.ReadAllTextAsync(schemaFile.FullName)
                    .ConfigureAwait(false);
                schema = SchemaSerializer.FromYaml(schemaYaml);
                Console.WriteLine($"📋 Loaded schema from {schemaFile.FullName}");
            }
            else if (offline)
            {
                Console.WriteLine("❌ --schema is required when using --offline mode");
                return 1;
            }

            if (!offline)
            {
                // Verify DB connection
                if (string.IsNullOrWhiteSpace(cfg.ConnectionString))
                {
                    Console.WriteLine(
                        "❌ Nimblesite.DataProvider.Core.json ConnectionString is required for online mode"
                    );
                    return 1;
                }

                try
                {
                    await using var conn = new NpgsqlConnection(cfg.ConnectionString);
                    await conn.OpenAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Failed to connect to database: {ex.Message}");
                    return 1;
                }
            }
            else
            {
                Console.WriteLine("🔌 Running in OFFLINE mode (no database connection)");
            }

            // Gather SQL files
            var sqlFiles = Directory.GetFiles(
                projectDir.FullName,
                "*.sql",
                SearchOption.AllDirectories
            );

            var hadErrors = false;

            foreach (var sqlPath in sqlFiles)
            {
                try
                {
                    var sql = await File.ReadAllTextAsync(sqlPath).ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(sql))
                        continue;

                    var baseName = Path.GetFileNameWithoutExtension(sqlPath);
                    if (baseName.EndsWith(".generated", StringComparison.OrdinalIgnoreCase))
                    {
                        baseName = baseName[..^".generated".Length];
                    }
                    if (
                        string.Equals(baseName, "schema", StringComparison.OrdinalIgnoreCase)
                        || baseName.EndsWith("_schema", StringComparison.OrdinalIgnoreCase)
                    )
                    {
                        continue;
                    }

                    // Parse SQL to extract parameters
                    var parameters = ExtractParameters(sql);

                    // Get column metadata
                    Result<IReadOnlyList<DatabaseColumn>, SqlError> colsResult;
                    if (offline)
                    {
                        colsResult = InferColumnTypesFromSql(sql, schema);
                    }
                    else
                    {
                        colsResult = await GetColumnMetadataAsync(
                                cfg.ConnectionString!,
                                sql,
                                parameters
                            )
                            .ConfigureAwait(false);
                    }

                    if (
                        colsResult
                        is Result<IReadOnlyList<DatabaseColumn>, SqlError>.Error<
                            IReadOnlyList<DatabaseColumn>,
                            SqlError
                        > colsError
                    )
                    {
                        Console.WriteLine($"❌ {colsError.Value.Message}");
                        Console.Error.WriteLine(
                            $"{sqlPath}(1,1): error DP0001: {colsError.Value.Message}"
                        );
                        var errorFile = Path.Combine(outDir.FullName, baseName + ".g.cs");
                        var content =
                            $"// Auto-generated due to SQL error in {sqlPath}\n#error {EscapeForPreprocessor(colsError.Value.Message)}\n";
                        await File.WriteAllTextAsync(errorFile, content).ConfigureAwait(false);
                        hadErrors = true;
                        continue;
                    }

                    var cols = (
                        (Result<IReadOnlyList<DatabaseColumn>, SqlError>.Ok<
                            IReadOnlyList<DatabaseColumn>,
                            SqlError
                        >)colsResult
                    ).Value;

                    // Generate code
                    var genResult = GenerateCode(baseName, sql, cols, parameters);
                    if (genResult is Result<string, SqlError>.Ok<string, SqlError> success)
                    {
                        var target = Path.Combine(outDir.FullName, baseName + ".g.cs");
                        await File.WriteAllTextAsync(target, success.Value).ConfigureAwait(false);
                        Console.WriteLine($"✅ Generated {target}");
                    }
                    else if (genResult is Result<string, SqlError>.Error<string, SqlError> failure)
                    {
                        Console.WriteLine($"❌ {failure.Value.Message}");
                        Console.Error.WriteLine(
                            $"{sqlPath}(1,1): error DP0002: {failure.Value.Message}"
                        );
                        var errorFile = Path.Combine(outDir.FullName, baseName + ".g.cs");
                        var content =
                            $"// Auto-generated due to generation error in {sqlPath}\n#error {EscapeForPreprocessor(failure.Value.Message)}\n";
                        await File.WriteAllTextAsync(errorFile, content).ConfigureAwait(false);
                        hadErrors = true;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error processing {sqlPath}: {ex.Message}");
                    var baseName = Path.GetFileNameWithoutExtension(sqlPath);
                    if (baseName.EndsWith(".generated", StringComparison.OrdinalIgnoreCase))
                    {
                        baseName = baseName[..^".generated".Length];
                    }
                    var errorFile = Path.Combine(outDir.FullName, baseName + ".g.cs");
                    var content =
                        $"// Auto-generated due to unexpected error in {sqlPath}\n#error {EscapeForPreprocessor(ex.Message)}\n";
                    Console.Error.WriteLine($"{sqlPath}(1,1): error DP0003: {ex.Message}");
                    await File.WriteAllTextAsync(errorFile, content).ConfigureAwait(false);
                    hadErrors = true;
                }
            }

            // Process table configurations for INSERT/UPDATE generation
            if (cfg.Tables is { Count: > 0 })
            {
                foreach (var table in cfg.Tables)
                {
                    try
                    {
                        Result<string, SqlError> tableCode;
                        if (offline)
                        {
                            tableCode = await GenerateTableOperationsOfflineAsync(
                                    table,
                                    schema,
                                    outDir.FullName
                                )
                                .ConfigureAwait(false);
                        }
                        else
                        {
                            tableCode = await GenerateTableOperationsAsync(
                                    cfg.ConnectionString!,
                                    table,
                                    outDir.FullName
                                )
                                .ConfigureAwait(false);
                        }

                        if (tableCode is Result<string, SqlError>.Error<string, SqlError> err)
                        {
                            Console.WriteLine($"❌ Table {table.Name}: {err.Value.Message}");
                            hadErrors = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ Error processing table {table.Name}: {ex.Message}");
                        hadErrors = true;
                    }
                }
            }

            return hadErrors ? 1 : 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Unexpected error: {ex}");
            return 1;
        }
    }

    private static Result<IReadOnlyList<DatabaseColumn>, SqlError> InferColumnTypesFromSql(
        string sql,
        SchemaDefinition? schema
    )
    {
        try
        {
            var columns = new List<DatabaseColumn>();

            // Extract column aliases from SELECT clause
            // Pattern: SELECT col1, col2 AS alias, func(col) AS alias2, ...
            var selectMatch = Regex.Match(
                sql,
                @"SELECT\s+(.*?)\s+FROM",
                RegexOptions.IgnoreCase | RegexOptions.Singleline
            );

            if (!selectMatch.Success)
            {
                return new Result<IReadOnlyList<DatabaseColumn>, SqlError>.Error<
                    IReadOnlyList<DatabaseColumn>,
                    SqlError
                >(new SqlError("Could not parse SELECT clause from SQL"));
            }

            var selectClause = selectMatch.Groups[1].Value;

            // Split by comma, but be careful with nested parentheses
            var columnDefs = ParseSelectColumns(selectClause);

            foreach (var colDef in columnDefs)
            {
                var (name, sqlType) = ParseColumnDefinition(colDef, schema);
                var csharpType = MapPostgresTypeToCSharp(sqlType, true);
                var isNullable =
                    !sqlType.Contains("serial", StringComparison.OrdinalIgnoreCase)
                    && !sqlType.Contains("not null", StringComparison.OrdinalIgnoreCase);

                columns.Add(
                    new DatabaseColumn
                    {
                        Name = name,
                        SqlType = sqlType,
                        CSharpType = csharpType,
                        IsNullable = isNullable,
                        IsPrimaryKey = false,
                        IsIdentity = sqlType.Contains("serial", StringComparison.OrdinalIgnoreCase),
                        IsComputed = false,
                    }
                );
            }

            if (columns.Count == 0)
            {
                return new Result<IReadOnlyList<DatabaseColumn>, SqlError>.Error<
                    IReadOnlyList<DatabaseColumn>,
                    SqlError
                >(new SqlError("No columns found in SELECT clause"));
            }

            return new Result<IReadOnlyList<DatabaseColumn>, SqlError>.Ok<
                IReadOnlyList<DatabaseColumn>,
                SqlError
            >(columns.AsReadOnly());
        }
        catch (Exception ex)
        {
            return new Result<IReadOnlyList<DatabaseColumn>, SqlError>.Error<
                IReadOnlyList<DatabaseColumn>,
                SqlError
            >(SqlError.FromException(ex));
        }
    }

    private static List<string> ParseSelectColumns(string selectClause)
    {
        var columns = new List<string>();
        var depth = 0;
        var current = new StringBuilder();

        foreach (var c in selectClause)
        {
            if (c == '(')
            {
                depth++;
                current.Append(c);
            }
            else if (c == ')')
            {
                depth--;
                current.Append(c);
            }
            else if (c == ',' && depth == 0)
            {
                columns.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        if (current.Length > 0)
            columns.Add(current.ToString().Trim());

        return columns;
    }

    private static (string name, string sqlType) ParseColumnDefinition(
        string colDef,
        SchemaDefinition? schema
    )
    {
        // Check for AS alias
        var asMatch = Regex.Match(colDef, @"(.+?)\s+AS\s+(\w+)", RegexOptions.IgnoreCase);

        if (asMatch.Success)
        {
            var expr = asMatch.Groups[1].Value.Trim();
            var alias = asMatch.Groups[2].Value.Trim();
            var type = InferTypeFromExpression(expr, schema);
            return (alias, type);
        }

        // Simple column reference: table.column or just column
        var colRef = colDef.Trim();
        if (colRef.Contains('.'))
        {
            var parts = colRef.Split('.');
            colRef = parts[^1];
        }

        // Try to get type from schema
        if (schema != null)
        {
            foreach (var table in schema.Tables)
            {
                var col = table.Columns.FirstOrDefault(c =>
                    c.Name.Equals(colRef, StringComparison.OrdinalIgnoreCase)
                );
                if (col != null)
                {
                    return (colRef, col.Type.ToString());
                }
            }
        }

        // Default to text
        return (colRef, "text");
    }

    private static string InferTypeFromExpression(string expr, SchemaDefinition? schema)
    {
        var lower = expr.ToLowerInvariant().Trim();

        // Check for function calls
        if (lower.Contains("count(") || lower.Contains("sum(") || lower.Contains("avg("))
            return "bigint";
        if (lower.Contains("now(") || lower.Contains("current_timestamp"))
            return "timestamp with time zone";
        if (lower.Contains("uuid") || lower.Contains("gen_random_uuid"))
            return "uuid";
        if (lower.Contains("lower(") || lower.Contains("upper("))
            return "text";

        // Check for table.column reference in schema
        var dotIdx = lower.LastIndexOf('.');
        if (dotIdx > 0 && schema != null)
        {
            var colName = expr[(dotIdx + 1)..].Trim();
            foreach (var table in schema.Tables)
            {
                var col = table.Columns.FirstOrDefault(c =>
                    c.Name.Equals(colName, StringComparison.OrdinalIgnoreCase)
                );
                if (col != null)
                    return col.Type.ToString();
            }
        }

        return "text";
    }

    private static async Task<Result<string, SqlError>> GenerateTableOperationsOfflineAsync(
        TableConfigItem table,
        SchemaDefinition? schema,
        string outDir
    )
    {
        if (schema == null)
        {
            return new Result<string, SqlError>.Error<string, SqlError>(
                new SqlError("Schema is required for offline table operations")
            );
        }

        var tableDef = schema.Tables.FirstOrDefault(t =>
            t.Name.Equals(table.Name, StringComparison.OrdinalIgnoreCase)
            && t.Schema.Equals(table.Schema, StringComparison.OrdinalIgnoreCase)
        );

        if (tableDef == null)
        {
            return new Result<string, SqlError>.Error<string, SqlError>(
                new SqlError($"Table {table.Schema}.{table.Name} not found in schema")
            );
        }

        var columns = new List<DatabaseColumn>();

        foreach (var col in tableDef.Columns)
        {
            // Skip excluded columns
            if (table.ExcludeColumns.Contains(col.Name, StringComparer.OrdinalIgnoreCase))
                continue;

            var isPk =
                tableDef.PrimaryKey?.Columns.Contains(col.Name, StringComparer.OrdinalIgnoreCase)
                    == true
                || table.PrimaryKeyColumns.Contains(col.Name, StringComparer.OrdinalIgnoreCase);

            columns.Add(
                new DatabaseColumn
                {
                    Name = col.Name,
                    SqlType = col.Type.ToString(),
                    CSharpType = MapPortableTypeToCSharp(col.Type, col.IsNullable),
                    IsNullable = col.IsNullable,
                    IsPrimaryKey = isPk,
                    IsIdentity = col.IsIdentity,
                    IsComputed =
                        col.DefaultValue?.StartsWith("nextval", StringComparison.OrdinalIgnoreCase)
                        == true,
                }
            );
        }

        if (columns.Count == 0)
        {
            return new Result<string, SqlError>.Error<string, SqlError>(
                new SqlError($"No columns found for table {table.Schema}.{table.Name}")
            );
        }

        var sb = new StringBuilder();
        var pascalName = ToPascalCase(table.Name);

        // Header
        _ = sb.AppendLine("// <auto-generated />");
        _ = sb.AppendLine("#nullable enable");
        _ = sb.AppendLine();
        _ = sb.AppendLine("using Npgsql;");
        _ = sb.AppendLine("using Outcome;");
        _ = sb.AppendLine("using Nimblesite.Sql.Model;");
        _ = sb.AppendLine();
        _ = sb.AppendLine("namespace Generated;");
        _ = sb.AppendLine();

        // Extension class
        _ = sb.AppendLine("/// <summary>");
        _ = sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"/// Generated CRUD operations for {table.Name} table."
        );
        _ = sb.AppendLine("/// </summary>");
        _ = sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"public static class {pascalName}Extensions"
        );
        _ = sb.AppendLine("{");

        // Generate INSERT method
        if (table.GenerateInsert)
        {
            GenerateInsertMethod(sb, table, columns, pascalName);
        }

        // Generate UPDATE method
        if (table.GenerateUpdate)
        {
            GenerateUpdateMethod(sb, table, columns, pascalName);
        }

        // Generate DELETE method
        if (table.GenerateDelete)
        {
            GenerateDeleteMethod(sb, table, columns, pascalName);
        }

        // Generate bulk INSERT method
        if (table.GenerateBulkInsert)
        {
            GenerateBulkInsertMethod(sb, table, columns, pascalName);
        }

        // Generate bulk UPSERT method
        if (table.GenerateBulkUpsert)
        {
            GenerateBulkUpsertMethod(sb, table, columns, pascalName);
        }

        _ = sb.AppendLine("}");

        var target = Path.Combine(outDir, $"{pascalName}Operations.g.cs");
        await File.WriteAllTextAsync(target, sb.ToString()).ConfigureAwait(false);
        Console.WriteLine($"✅ Generated {target}");

        return new Result<string, SqlError>.Ok<string, SqlError>(sb.ToString());
    }

    private static async Task<Result<string, SqlError>> GenerateTableOperationsAsync(
        string connectionString,
        TableConfigItem table,
        string outDir
    )
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync().ConfigureAwait(false);

        // Get column metadata from information_schema
        var columns = new List<DatabaseColumn>();
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                SELECT c.column_name, c.data_type, c.is_nullable, c.column_default,
                       COALESCE(c.is_identity, 'NO') as is_identity,
                       CASE WHEN pk.column_name IS NOT NULL THEN 'YES' ELSE 'NO' END as is_pk
                FROM information_schema.columns c
                LEFT JOIN (
                    SELECT kcu.column_name
                    FROM information_schema.table_constraints tc
                    JOIN information_schema.key_column_usage kcu
                        ON tc.constraint_name = kcu.constraint_name
                    WHERE tc.table_schema = @schema
                        AND tc.table_name = @table
                        AND tc.constraint_type = 'PRIMARY KEY'
                ) pk ON pk.column_name = c.column_name
                WHERE c.table_schema = @schema AND c.table_name = @table
                ORDER BY c.ordinal_position
                """;
            _ = cmd.Parameters.AddWithValue("schema", table.Schema);
            _ = cmd.Parameters.AddWithValue("table", table.Name);

            await using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                var colName = reader.GetString(0);
                var dataType = reader.GetString(1);
                var isNullable = reader.GetString(2) == "YES";
                var colDefault = reader.IsDBNull(3) ? null : reader.GetString(3);
                var isIdentity = reader.GetString(4) == "YES";
                var isPk = reader.GetString(5) == "YES";

                // Skip excluded columns
                if (table.ExcludeColumns.Contains(colName, StringComparer.OrdinalIgnoreCase))
                    continue;

                columns.Add(
                    new DatabaseColumn
                    {
                        Name = colName,
                        SqlType = dataType,
                        CSharpType = MapPostgresTypeToCSharp(dataType, isNullable),
                        IsNullable = isNullable,
                        IsPrimaryKey = isPk,
                        IsIdentity = isIdentity,
                        IsComputed =
                            colDefault?.StartsWith("nextval", StringComparison.OrdinalIgnoreCase)
                            == true,
                    }
                );
            }
        }

        if (columns.Count == 0)
        {
            return new Result<string, SqlError>.Error<string, SqlError>(
                new SqlError($"No columns found for table {table.Schema}.{table.Name}")
            );
        }

        var sb = new StringBuilder();
        var pascalName = ToPascalCase(table.Name);

        // Header
        _ = sb.AppendLine("// <auto-generated />");
        _ = sb.AppendLine("#nullable enable");
        _ = sb.AppendLine();
        _ = sb.AppendLine("using Npgsql;");
        _ = sb.AppendLine("using Outcome;");
        _ = sb.AppendLine("using Nimblesite.Sql.Model;");
        _ = sb.AppendLine();
        _ = sb.AppendLine("namespace Generated;");
        _ = sb.AppendLine();

        // Extension class
        _ = sb.AppendLine("/// <summary>");
        _ = sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"/// Generated CRUD operations for {table.Name} table."
        );
        _ = sb.AppendLine("/// </summary>");
        _ = sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"public static class {pascalName}Extensions"
        );
        _ = sb.AppendLine("{");

        // Generate INSERT method
        if (table.GenerateInsert)
        {
            GenerateInsertMethod(sb, table, columns, pascalName);
        }

        // Generate UPDATE method
        if (table.GenerateUpdate)
        {
            GenerateUpdateMethod(sb, table, columns, pascalName);
        }

        // Generate DELETE method
        if (table.GenerateDelete)
        {
            GenerateDeleteMethod(sb, table, columns, pascalName);
        }

        // Generate bulk INSERT method
        if (table.GenerateBulkInsert)
        {
            GenerateBulkInsertMethod(sb, table, columns, pascalName);
        }

        // Generate bulk UPSERT method
        if (table.GenerateBulkUpsert)
        {
            GenerateBulkUpsertMethod(sb, table, columns, pascalName);
        }

        _ = sb.AppendLine("}");

        var target = Path.Combine(outDir, $"{pascalName}Operations.g.cs");
        await File.WriteAllTextAsync(target, sb.ToString()).ConfigureAwait(false);
        Console.WriteLine($"✅ Generated {target}");

        return new Result<string, SqlError>.Ok<string, SqlError>(sb.ToString());
    }

    private static void GenerateInsertMethod(
        StringBuilder sb,
        TableConfigItem table,
        List<DatabaseColumn> columns,
        string pascalName
    )
    {
        // Get insertable columns (exclude auto-generated ones)
        var insertable = columns.Where(c => !c.IsIdentity && !c.IsComputed).ToList();
        var parameters = string.Join(
            ", ",
            insertable.Select(c => $"{c.CSharpType} {ToCamelCase(c.Name)}")
        );

        _ = sb.AppendLine();
        _ = sb.AppendLine("    /// <summary>");
        _ = sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"    /// Inserts a row into {table.Name}. Returns inserted id or null on conflict."
        );
        _ = sb.AppendLine("    /// </summary>");
        _ = sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"    public static async Task<Result<Guid?, SqlError>> Insert{pascalName}Async("
        );
        _ = sb.AppendLine("        this NpgsqlConnection conn,");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"        {parameters})");
        _ = sb.AppendLine("    {");

        var colNames = string.Join(", ", insertable.Select(c => c.Name));
        var paramNames = string.Join(", ", insertable.Select(c => $"@{ToCamelCase(c.Name)}"));

        _ = sb.AppendLine("        const string sql = @\"");
        _ = sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"            INSERT INTO {table.Schema}.{table.Name} ({colNames})"
        );
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"            VALUES ({paramNames})");
        _ = sb.AppendLine("            ON CONFLICT DO NOTHING");
        _ = sb.AppendLine("            RETURNING id\";");
        _ = sb.AppendLine();
        _ = sb.AppendLine("        try");
        _ = sb.AppendLine("        {");
        _ = sb.AppendLine("            await using var cmd = new NpgsqlCommand(sql, conn);");

        foreach (var col in insertable)
        {
            var paramName = ToCamelCase(col.Name);
            if (col.IsNullable)
            {
                _ = sb.AppendLine(
                    CultureInfo.InvariantCulture,
                    $"            cmd.Parameters.AddWithValue(\"{paramName}\", {paramName} ?? (object)DBNull.Value);"
                );
            }
            else
            {
                _ = sb.AppendLine(
                    CultureInfo.InvariantCulture,
                    $"            cmd.Parameters.AddWithValue(\"{paramName}\", {paramName});"
                );
            }
        }

        _ = sb.AppendLine();
        _ = sb.AppendLine(
            "            var result = await cmd.ExecuteScalarAsync().ConfigureAwait(false);"
        );
        _ = sb.AppendLine(
            "            return new Result<Guid?, SqlError>.Ok<Guid?, SqlError>(result is Guid g ? g : null);"
        );
        _ = sb.AppendLine("        }");
        _ = sb.AppendLine("        catch (Exception ex)");
        _ = sb.AppendLine("        {");
        _ = sb.AppendLine(
            "            return new Result<Guid?, SqlError>.Error<Guid?, SqlError>(SqlError.FromException(ex));"
        );
        _ = sb.AppendLine("        }");
        _ = sb.AppendLine("    }");
    }

    private static void GenerateUpdateMethod(
        StringBuilder sb,
        TableConfigItem table,
        List<DatabaseColumn> columns,
        string pascalName
    )
    {
        var pkCols = columns.Where(c => c.IsPrimaryKey).ToList();
        var updateable = columns
            .Where(c => !c.IsPrimaryKey && !c.IsIdentity && !c.IsComputed)
            .ToList();

        if (pkCols.Count == 0 || updateable.Count == 0)
            return;

        var allParams = pkCols.Concat(updateable).ToList();
        var parameters = string.Join(
            ", ",
            allParams.Select(c => $"{c.CSharpType} {ToCamelCase(c.Name)}")
        );

        _ = sb.AppendLine();
        _ = sb.AppendLine("    /// <summary>");
        _ = sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"    /// Updates a row in {table.Name} by primary key."
        );
        _ = sb.AppendLine("    /// </summary>");
        _ = sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"    public static async Task<Result<int, SqlError>> Update{pascalName}Async("
        );
        _ = sb.AppendLine("        this NpgsqlConnection conn,");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"        {parameters})");
        _ = sb.AppendLine("    {");

        var setClauses = string.Join(
            ", ",
            updateable.Select(c => $"{c.Name} = @{ToCamelCase(c.Name)}")
        );
        var whereClauses = string.Join(
            " AND ",
            pkCols.Select(c => $"{c.Name} = @{ToCamelCase(c.Name)}")
        );

        _ = sb.AppendLine("        const string sql = @\"");
        _ = sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"            UPDATE {table.Schema}.{table.Name}"
        );
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"            SET {setClauses}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"            WHERE {whereClauses}\";");
        _ = sb.AppendLine();
        _ = sb.AppendLine("        try");
        _ = sb.AppendLine("        {");
        _ = sb.AppendLine("            await using var cmd = new NpgsqlCommand(sql, conn);");

        foreach (var col in allParams)
        {
            var paramName = ToCamelCase(col.Name);
            if (col.IsNullable)
            {
                _ = sb.AppendLine(
                    CultureInfo.InvariantCulture,
                    $"            cmd.Parameters.AddWithValue(\"{paramName}\", {paramName} ?? (object)DBNull.Value);"
                );
            }
            else
            {
                _ = sb.AppendLine(
                    CultureInfo.InvariantCulture,
                    $"            cmd.Parameters.AddWithValue(\"{paramName}\", {paramName});"
                );
            }
        }

        _ = sb.AppendLine();
        _ = sb.AppendLine(
            "            var rows = await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);"
        );
        _ = sb.AppendLine("            return new Result<int, SqlError>.Ok<int, SqlError>(rows);");
        _ = sb.AppendLine("        }");
        _ = sb.AppendLine("        catch (Exception ex)");
        _ = sb.AppendLine("        {");
        _ = sb.AppendLine(
            "            return new Result<int, SqlError>.Error<int, SqlError>(SqlError.FromException(ex));"
        );
        _ = sb.AppendLine("        }");
        _ = sb.AppendLine("    }");
    }

    private static void GenerateDeleteMethod(
        StringBuilder sb,
        TableConfigItem table,
        List<DatabaseColumn> columns,
        string pascalName
    )
    {
        var pkCols = columns.Where(c => c.IsPrimaryKey).ToList();
        if (pkCols.Count == 0)
            return;

        var parameters = string.Join(
            ", ",
            pkCols.Select(c => $"{c.CSharpType} {ToCamelCase(c.Name)}")
        );

        _ = sb.AppendLine();
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"    /// <summary>");
        _ = sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"    /// Deletes a row from {table.Name} by primary key."
        );
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"    /// </summary>");
        _ = sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"    public static async Task<Result<int, SqlError>> Delete{pascalName}Async("
        );
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"        this NpgsqlConnection conn,");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"        {parameters})");
        _ = sb.AppendLine("    {");

        var whereClauses = string.Join(
            " AND ",
            pkCols.Select(c => $"{c.Name} = @{ToCamelCase(c.Name)}")
        );

        _ = sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"        const string sql = @\"DELETE FROM {table.Schema}.{table.Name} WHERE {whereClauses}\";"
        );
        _ = sb.AppendLine();
        _ = sb.AppendLine("        try");
        _ = sb.AppendLine("        {");
        _ = sb.AppendLine("            await using var cmd = new NpgsqlCommand(sql, conn);");

        foreach (var col in pkCols)
        {
            _ = sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"            cmd.Parameters.AddWithValue(\"{ToCamelCase(col.Name)}\", {ToCamelCase(col.Name)});"
            );
        }

        _ = sb.AppendLine();
        _ = sb.AppendLine(
            "            var rows = await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);"
        );
        _ = sb.AppendLine("            return new Result<int, SqlError>.Ok<int, SqlError>(rows);");
        _ = sb.AppendLine("        }");
        _ = sb.AppendLine("        catch (Exception ex)");
        _ = sb.AppendLine("        {");
        _ = sb.AppendLine(
            "            return new Result<int, SqlError>.Error<int, SqlError>(SqlError.FromException(ex));"
        );
        _ = sb.AppendLine("        }");
        _ = sb.AppendLine("    }");
    }

    private static void GenerateBulkInsertMethod(
        StringBuilder sb,
        TableConfigItem table,
        List<DatabaseColumn> columns,
        string pascalName
    )
    {
        // Get insertable columns (exclude auto-generated ones)
        var insertable = columns.Where(c => !c.IsIdentity && !c.IsComputed).ToList();
        if (insertable.Count == 0)
            return;

        // Build tuple type for the IEnumerable parameter
        var tupleType = string.Join(
            ", ",
            insertable.Select(c => $"{c.CSharpType} {ToPascalCase(c.Name)}")
        );

        _ = sb.AppendLine();
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"    /// <summary>");
        _ = sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"    /// Bulk inserts rows into {table.Name} using batched multi-row VALUES."
        );
        _ = sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"    /// Uses ON CONFLICT DO NOTHING to skip duplicates."
        );
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"    /// </summary>");
        _ = sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"    /// <param name=\"conn\">Open database connection.</param>"
        );
        _ = sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"    /// <param name=\"records\">Records to insert as tuples.</param>"
        );
        _ = sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"    /// <param name=\"batchSize\">Max rows per batch (default 1000).</param>"
        );
        _ = sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"    /// <returns>Total rows inserted.</returns>"
        );
        _ = sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"    public static async Task<Result<int, SqlError>> BulkInsert{pascalName}Async("
        );
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"        this NpgsqlConnection conn,");
        _ = sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"        IEnumerable<({tupleType})> records,"
        );
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"        int batchSize = 1000)");
        _ = sb.AppendLine("    {");
        _ = sb.AppendLine("        var totalInserted = 0;");
        _ = sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"        var batch = new List<({tupleType})>(batchSize);"
        );
        _ = sb.AppendLine();
        _ = sb.AppendLine("        try");
        _ = sb.AppendLine("        {");
        _ = sb.AppendLine("            foreach (var record in records)");
        _ = sb.AppendLine("            {");
        _ = sb.AppendLine("                batch.Add(record);");
        _ = sb.AppendLine("                if (batch.Count >= batchSize)");
        _ = sb.AppendLine("                {");
        _ = sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"                    var result = await ExecuteBulkInsert{pascalName}BatchAsync(conn, batch).ConfigureAwait(false);"
        );
        _ = sb.AppendLine(
            "                    if (result is Result<int, SqlError>.Error<int, SqlError> err)"
        );
        _ = sb.AppendLine("                        return err;");
        _ = sb.AppendLine(
            "                    totalInserted += ((Result<int, SqlError>.Ok<int, SqlError>)result).Value;"
        );
        _ = sb.AppendLine("                    batch.Clear();");
        _ = sb.AppendLine("                }");
        _ = sb.AppendLine("            }");
        _ = sb.AppendLine();
        _ = sb.AppendLine("            if (batch.Count > 0)");
        _ = sb.AppendLine("            {");
        _ = sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"                var finalResult = await ExecuteBulkInsert{pascalName}BatchAsync(conn, batch).ConfigureAwait(false);"
        );
        _ = sb.AppendLine(
            "                if (finalResult is Result<int, SqlError>.Error<int, SqlError> finalErr)"
        );
        _ = sb.AppendLine("                    return finalErr;");
        _ = sb.AppendLine(
            "                    totalInserted += ((Result<int, SqlError>.Ok<int, SqlError>)finalResult).Value;"
        );
        _ = sb.AppendLine("            }");
        _ = sb.AppendLine();
        _ = sb.AppendLine(
            "            return new Result<int, SqlError>.Ok<int, SqlError>(totalInserted);"
        );
        _ = sb.AppendLine("        }");
        _ = sb.AppendLine("        catch (Exception ex)");
        _ = sb.AppendLine("        {");
        _ = sb.AppendLine(
            "            return new Result<int, SqlError>.Error<int, SqlError>(SqlError.FromException(ex));"
        );
        _ = sb.AppendLine("        }");
        _ = sb.AppendLine("    }");
        _ = sb.AppendLine();

        // Generate batch execution helper
        _ = sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"    private static async Task<Result<int, SqlError>> ExecuteBulkInsert{pascalName}BatchAsync("
        );
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"        NpgsqlConnection conn,");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"        List<({tupleType})> batch)");
        _ = sb.AppendLine("    {");
        _ = sb.AppendLine("        if (batch.Count == 0)");
        _ = sb.AppendLine("            return new Result<int, SqlError>.Ok<int, SqlError>(0);");
        _ = sb.AppendLine();

        var colNames = string.Join(", ", insertable.Select(c => c.Name));
        _ = sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"        var sql = new System.Text.StringBuilder(\"INSERT INTO {table.Schema}.{table.Name} ({colNames}) VALUES \");"
        );
        _ = sb.AppendLine();
        _ = sb.AppendLine("        for (int i = 0; i < batch.Count; i++)");
        _ = sb.AppendLine("        {");
        _ = sb.AppendLine("            if (i > 0) sql.Append(\", \");");

        // Build VALUES placeholders
        var placeholders = string.Join(
            ", ",
            insertable.Select((c, idx) => $"@p\" + (i * {insertable.Count} + {idx}) + \"")
        );
        _ = sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"            sql.Append(\"({placeholders})\");"
        );
        _ = sb.AppendLine("        }");
        _ = sb.AppendLine("        sql.Append(\" ON CONFLICT DO NOTHING\");");
        _ = sb.AppendLine();
        _ = sb.AppendLine("        await using var cmd = new NpgsqlCommand(sql.ToString(), conn);");
        _ = sb.AppendLine();
        _ = sb.AppendLine("        for (int i = 0; i < batch.Count; i++)");
        _ = sb.AppendLine("        {");
        _ = sb.AppendLine("            var rec = batch[i];");

        for (int i = 0; i < insertable.Count; i++)
        {
            var col = insertable[i];
            // Preserve the column name verbatim so generated record fields
            // match the SQLite CLI output (which kept snake_case literally),
            // and so consumers that reference `rec.user_id` etc. keep working.
            var propName = col.Name;
            if (col.IsNullable)
            {
                _ = sb.AppendLine(
                    CultureInfo.InvariantCulture,
                    $"            cmd.Parameters.AddWithValue(\"p\" + (i * {insertable.Count} + {i}), rec.{propName} ?? (object)DBNull.Value);"
                );
            }
            else
            {
                _ = sb.AppendLine(
                    CultureInfo.InvariantCulture,
                    $"            cmd.Parameters.AddWithValue(\"p\" + (i * {insertable.Count} + {i}), rec.{propName});"
                );
            }
        }

        _ = sb.AppendLine("        }");
        _ = sb.AppendLine();
        _ = sb.AppendLine(
            "        var rows = await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);"
        );
        _ = sb.AppendLine("        return new Result<int, SqlError>.Ok<int, SqlError>(rows);");
        _ = sb.AppendLine("    }");
    }

    private static void GenerateBulkUpsertMethod(
        StringBuilder sb,
        TableConfigItem table,
        List<DatabaseColumn> columns,
        string pascalName
    )
    {
        // Get insertable columns (exclude auto-generated ones)
        var insertable = columns.Where(c => !c.IsIdentity && !c.IsComputed).ToList();
        var pkCols = columns.Where(c => c.IsPrimaryKey).ToList();

        if (insertable.Count == 0 || pkCols.Count == 0)
            return;

        // Build tuple type for the IEnumerable parameter
        var tupleType = string.Join(
            ", ",
            insertable.Select(c => $"{c.CSharpType} {ToPascalCase(c.Name)}")
        );

        _ = sb.AppendLine();
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"    /// <summary>");
        _ = sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"    /// Bulk upserts rows into {table.Name} using batched multi-row VALUES."
        );
        _ = sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"    /// Uses ON CONFLICT DO UPDATE to insert or update existing rows."
        );
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"    /// </summary>");
        _ = sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"    /// <param name=\"conn\">Open database connection.</param>"
        );
        _ = sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"    /// <param name=\"records\">Records to upsert as tuples.</param>"
        );
        _ = sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"    /// <param name=\"batchSize\">Max rows per batch (default 1000).</param>"
        );
        _ = sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"    /// <returns>Total rows affected.</returns>"
        );
        _ = sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"    public static async Task<Result<int, SqlError>> BulkUpsert{pascalName}Async("
        );
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"        this NpgsqlConnection conn,");
        _ = sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"        IEnumerable<({tupleType})> records,"
        );
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"        int batchSize = 1000)");
        _ = sb.AppendLine("    {");
        _ = sb.AppendLine("        var totalAffected = 0;");
        _ = sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"        var batch = new List<({tupleType})>(batchSize);"
        );
        _ = sb.AppendLine();
        _ = sb.AppendLine("        try");
        _ = sb.AppendLine("        {");
        _ = sb.AppendLine("            foreach (var record in records)");
        _ = sb.AppendLine("            {");
        _ = sb.AppendLine("                batch.Add(record);");
        _ = sb.AppendLine("                if (batch.Count >= batchSize)");
        _ = sb.AppendLine("                {");
        _ = sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"                    var result = await ExecuteBulkUpsert{pascalName}BatchAsync(conn, batch).ConfigureAwait(false);"
        );
        _ = sb.AppendLine(
            "                    if (result is Result<int, SqlError>.Error<int, SqlError> err)"
        );
        _ = sb.AppendLine("                        return err;");
        _ = sb.AppendLine(
            "                    totalAffected += ((Result<int, SqlError>.Ok<int, SqlError>)result).Value;"
        );
        _ = sb.AppendLine("                    batch.Clear();");
        _ = sb.AppendLine("                }");
        _ = sb.AppendLine("            }");
        _ = sb.AppendLine();
        _ = sb.AppendLine("            if (batch.Count > 0)");
        _ = sb.AppendLine("            {");
        _ = sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"                var finalResult = await ExecuteBulkUpsert{pascalName}BatchAsync(conn, batch).ConfigureAwait(false);"
        );
        _ = sb.AppendLine(
            "                if (finalResult is Result<int, SqlError>.Error<int, SqlError> finalErr)"
        );
        _ = sb.AppendLine("                    return finalErr;");
        _ = sb.AppendLine(
            "                    totalAffected += ((Result<int, SqlError>.Ok<int, SqlError>)finalResult).Value;"
        );
        _ = sb.AppendLine("            }");
        _ = sb.AppendLine();
        _ = sb.AppendLine(
            "            return new Result<int, SqlError>.Ok<int, SqlError>(totalAffected);"
        );
        _ = sb.AppendLine("        }");
        _ = sb.AppendLine("        catch (Exception ex)");
        _ = sb.AppendLine("        {");
        _ = sb.AppendLine(
            "            return new Result<int, SqlError>.Error<int, SqlError>(SqlError.FromException(ex));"
        );
        _ = sb.AppendLine("        }");
        _ = sb.AppendLine("    }");
        _ = sb.AppendLine();

        // Generate batch execution helper
        _ = sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"    private static async Task<Result<int, SqlError>> ExecuteBulkUpsert{pascalName}BatchAsync("
        );
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"        NpgsqlConnection conn,");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"        List<({tupleType})> batch)");
        _ = sb.AppendLine("    {");
        _ = sb.AppendLine("        if (batch.Count == 0)");
        _ = sb.AppendLine("            return new Result<int, SqlError>.Ok<int, SqlError>(0);");
        _ = sb.AppendLine();

        var colNames = string.Join(", ", insertable.Select(c => c.Name));
        var pkColNames = string.Join(", ", pkCols.Select(c => c.Name));
        var updateCols = insertable.Where(c => !c.IsPrimaryKey).ToList();
        var updateSet = string.Join(", ", updateCols.Select(c => $"{c.Name} = EXCLUDED.{c.Name}"));

        _ = sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"        var sql = new System.Text.StringBuilder(\"INSERT INTO {table.Schema}.{table.Name} ({colNames}) VALUES \");"
        );
        _ = sb.AppendLine();
        _ = sb.AppendLine("        for (int i = 0; i < batch.Count; i++)");
        _ = sb.AppendLine("        {");
        _ = sb.AppendLine("            if (i > 0) sql.Append(\", \");");

        // Build VALUES placeholders
        var placeholders = string.Join(
            ", ",
            insertable.Select((c, idx) => $"@p\" + (i * {insertable.Count} + {idx}) + \"")
        );
        _ = sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"            sql.Append(\"({placeholders})\");"
        );
        _ = sb.AppendLine("        }");

        // Add ON CONFLICT DO UPDATE clause
        if (updateCols.Count > 0)
        {
            _ = sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"        sql.Append(\" ON CONFLICT ({pkColNames}) DO UPDATE SET {updateSet}\");"
            );
        }
        else
        {
            // If all columns are PKs, just do nothing on conflict
            _ = sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"        sql.Append(\" ON CONFLICT ({pkColNames}) DO NOTHING\");"
            );
        }

        _ = sb.AppendLine();
        _ = sb.AppendLine("        await using var cmd = new NpgsqlCommand(sql.ToString(), conn);");
        _ = sb.AppendLine();
        _ = sb.AppendLine("        for (int i = 0; i < batch.Count; i++)");
        _ = sb.AppendLine("        {");
        _ = sb.AppendLine("            var rec = batch[i];");

        for (int i = 0; i < insertable.Count; i++)
        {
            var col = insertable[i];
            // Preserve the column name verbatim so generated record fields
            // match the SQLite CLI output (which kept snake_case literally),
            // and so consumers that reference `rec.user_id` etc. keep working.
            var propName = col.Name;
            if (col.IsNullable)
            {
                _ = sb.AppendLine(
                    CultureInfo.InvariantCulture,
                    $"            cmd.Parameters.AddWithValue(\"p\" + (i * {insertable.Count} + {i}), rec.{propName} ?? (object)DBNull.Value);"
                );
            }
            else
            {
                _ = sb.AppendLine(
                    CultureInfo.InvariantCulture,
                    $"            cmd.Parameters.AddWithValue(\"p\" + (i * {insertable.Count} + {i}), rec.{propName});"
                );
            }
        }

        _ = sb.AppendLine("        }");
        _ = sb.AppendLine();
        _ = sb.AppendLine(
            "        var rows = await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);"
        );
        _ = sb.AppendLine("        return new Result<int, SqlError>.Ok<int, SqlError>(rows);");
        _ = sb.AppendLine("    }");
    }

    private static List<string> ExtractParameters(string sql)
    {
        var parameters = new List<string>();
        var i = 0;
        while (i < sql.Length)
        {
            if (sql[i] == '@')
            {
                var start = i + 1;
                while (
                    start < sql.Length && (char.IsLetterOrDigit(sql[start]) || sql[start] == '_')
                )
                {
                    start++;
                }
                if (start > i + 1)
                {
                    var paramName = sql[(i + 1)..start];
                    if (!parameters.Contains(paramName, StringComparer.OrdinalIgnoreCase))
                    {
                        parameters.Add(paramName);
                    }
                }
                i = start;
            }
            else
            {
                i++;
            }
        }
        return parameters;
    }

    private static async Task<
        Result<IReadOnlyList<DatabaseColumn>, SqlError>
    > GetColumnMetadataAsync(string connectionString, string sql, List<string> parameters)
    {
        try
        {
            await using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync().ConfigureAwait(false);

            // Replace @params with NULL for metadata query
            var metaSql = sql;
            foreach (var param in parameters)
            {
                metaSql = metaSql.Replace($"@{param}", "NULL", StringComparison.OrdinalIgnoreCase);
            }

            // Strip optional trailing semicolons + whitespace so the inner
            // statement parses cleanly inside a CTE wrapper. Postgres rejects
            // a `;` followed by `)` inside a subquery / CTE body.
            metaSql = metaSql.TrimEnd();
            while (metaSql.EndsWith(";", StringComparison.Ordinal))
            {
                metaSql = metaSql[..^1].TrimEnd();
            }

            // Wrap in a CTE to get metadata without executing. CTE form
            // (instead of `SELECT * FROM (<sql>) AS _meta`) is required so
            // that UPDATE/INSERT/DELETE ... RETURNING statements also work,
            // since Postgres only allows DML inside a `WITH` clause, not
            // inside a `FROM (...)` subquery.
            var wrappedSql = $"WITH _meta AS ({metaSql}) SELECT * FROM _meta WHERE 1=0";

            await using var cmd = new NpgsqlCommand(wrappedSql, conn);
            await using var reader = await cmd.ExecuteReaderAsync(
                    System.Data.CommandBehavior.SchemaOnly
                )
                .ConfigureAwait(false);

            var schema = reader.GetColumnSchema();
            var columns = new List<DatabaseColumn>();

            foreach (var col in schema)
            {
                var dbType = col.DataTypeName ?? "text";
                var csharpType = MapPostgresTypeToCSharp(dbType, col.AllowDBNull ?? true);

                columns.Add(
                    new DatabaseColumn
                    {
                        Name = col.ColumnName,
                        SqlType = dbType,
                        CSharpType = csharpType,
                        IsNullable = col.AllowDBNull ?? true,
                        IsPrimaryKey = col.IsKey ?? false,
                        IsIdentity = col.IsIdentity ?? false,
                        IsComputed = col.IsReadOnly ?? false,
                    }
                );
            }

            return new Result<IReadOnlyList<DatabaseColumn>, SqlError>.Ok<
                IReadOnlyList<DatabaseColumn>,
                SqlError
            >(columns.AsReadOnly());
        }
        catch (Exception ex)
        {
            return new Result<IReadOnlyList<DatabaseColumn>, SqlError>.Error<
                IReadOnlyList<DatabaseColumn>,
                SqlError
            >(SqlError.FromException(ex));
        }
    }

    private static string MapPostgresTypeToCSharp(string pgType, bool isNullable)
    {
        var baseType = pgType.ToLowerInvariant() switch
        {
            // PostgreSQL native types
            "uuid" => "Guid",
            "boolean" or "bool" => "bool",
            "smallint" or "int2" => "short",
            "integer" or "int4" or "int" => "int",
            "bigint" or "int8" => "long",
            "real" or "float4" => "float",
            "double precision" or "float8" => "double",
            "numeric" or "decimal" or "money" => "decimal",
            "date" => "DateOnly",
            "time" or "time without time zone" => "TimeOnly",
            "time with time zone" or "timetz" => "TimeOnly",
            "timestamp" or "timestamp without time zone" => "DateTime",
            "timestamp with time zone" or "timestamptz" => "DateTimeOffset",
            "interval" => "TimeSpan",
            "bytea" => "byte[]",
            "text" or "varchar" or "character varying" or "char" or "character" or "name" =>
                "string",
            "json" or "jsonb" => "string",
            var t when t.EndsWith("[]", StringComparison.Ordinal) => "string[]",

            // PortableType names (from schema.yaml)
            "uuidtype" => "Guid",
            "booleantype" => "bool",
            "smallinttype" => "short",
            "inttype" => "int",
            "biginttype" => "long",
            "floattype" => "float",
            "doubletype" => "double",
            "decimaltype" => "decimal",
            "moneytype" => "decimal",
            "smallmoneytype" => "decimal",
            "datetype" => "DateOnly",
            "timetype" => "TimeOnly",
            "datetimetype" => "DateTime",
            "datetimeoffsettype" => "DateTimeOffset",
            "texttype" => "string",
            "chartype" => "string",
            "varchartype" => "string",
            "nchartype" => "string",
            "nvarchartype" => "string",
            "jsontype" => "string",
            "xmltype" => "string",
            "binarytype" => "byte[]",
            "varbinarytype" => "byte[]",
            "blobtype" => "byte[]",
            "rowversiontype" => "byte[]",

            _ => "string",
        };

        // Add nullable suffix for nullable types (including strings but not arrays)
        if (isNullable && !baseType.EndsWith("[]", StringComparison.Ordinal))
        {
            return baseType + "?";
        }

        return baseType;
    }

    private static Result<string, SqlError> GenerateCode(
        string fileName,
        string sql,
        IReadOnlyList<DatabaseColumn> columns,
        List<string> parameters
    )
    {
        var sb = new StringBuilder();
        var recordName = fileName;

        // Fully qualified Result type strings used throughout the generated
        // file. Inlining these avoids per-file `using XxxOk = ...` aliases
        // that conflict (CS1537) with consumer-side global aliases.
        var resultType =
            $"Outcome.Result<System.Collections.Immutable.ImmutableList<{recordName}>, Nimblesite.Sql.Model.SqlError>";
        var okType =
            $"Outcome.Result<System.Collections.Immutable.ImmutableList<{recordName}>, Nimblesite.Sql.Model.SqlError>.Ok<System.Collections.Immutable.ImmutableList<{recordName}>, Nimblesite.Sql.Model.SqlError>";
        var errorType =
            $"Outcome.Result<System.Collections.Immutable.ImmutableList<{recordName}>, Nimblesite.Sql.Model.SqlError>.Error<System.Collections.Immutable.ImmutableList<{recordName}>, Nimblesite.Sql.Model.SqlError>";

        // Header with all using statements at the top, then a file-scoped
        // `namespace Generated;` so consumers can `using Generated;`.
        _ = sb.AppendLine("// <auto-generated />");
        _ = sb.AppendLine("#nullable enable");
        _ = sb.AppendLine();
        _ = sb.AppendLine("using System.Collections.Immutable;");
        _ = sb.AppendLine("using Npgsql;");
        _ = sb.AppendLine("using Outcome;");
        _ = sb.AppendLine("using Nimblesite.Sql.Model;");
        _ = sb.AppendLine();
        _ = sb.AppendLine("namespace Generated;");
        _ = sb.AppendLine();

        // Generate record type
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"/// <summary>");
        _ = sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"/// Generated record for {fileName} query."
        );
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"/// </summary>");
        _ = sb.Append(CultureInfo.InvariantCulture, $"public sealed record {recordName}(");

        var first = true;
        foreach (var col in columns)
        {
            if (!first)
                _ = sb.Append(", ");
            first = false;

            // Preserve the column name verbatim so generated record fields
            // match the SQLite CLI output (which kept snake_case literally),
            // and so consumers that reference `rec.user_id` etc. keep working.
            var propName = col.Name;
            _ = sb.Append(CultureInfo.InvariantCulture, $"{col.CSharpType} {propName}");
        }
        _ = sb.AppendLine(");");
        _ = sb.AppendLine();

        // Generate extension method
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"/// <summary>");
        _ = sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"/// Extension methods for {fileName} query."
        );
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"/// </summary>");
        _ = sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"public static class {fileName}Extensions"
        );
        _ = sb.AppendLine("{");

        // SQL constant
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"    private const string Sql = @\"");
        _ = sb.AppendLine(sql.Replace("\"", "\"\"", StringComparison.Ordinal));
        _ = sb.AppendLine("\";");
        _ = sb.AppendLine();

        // Async method
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"    /// <summary>");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"    /// Executes the {fileName} query.");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"    /// </summary>");
        _ = sb.Append(
            CultureInfo.InvariantCulture,
            $"    public static async Task<{resultType}> {fileName}Async(this NpgsqlConnection conn"
        );

        foreach (var param in parameters)
        {
            var paramType = InferParameterType(param, columns);
            _ = sb.Append(CultureInfo.InvariantCulture, $", {paramType} {ToCamelCase(param)}");
        }
        _ = sb.AppendLine(")");
        _ = sb.AppendLine("    {");
        _ = sb.AppendLine("        try");
        _ = sb.AppendLine("        {");
        _ = sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"            var results = ImmutableList.CreateBuilder<{recordName}>();"
        );
        _ = sb.AppendLine("            await using var cmd = new NpgsqlCommand(Sql, conn);");

        foreach (var param in parameters)
        {
            _ = sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"            cmd.Parameters.AddWithValue(\"{param}\", {ToCamelCase(param)});"
            );
        }

        _ = sb.AppendLine();
        _ = sb.AppendLine(
            "            await using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);"
        );
        _ = sb.AppendLine("            while (await reader.ReadAsync().ConfigureAwait(false))");
        _ = sb.AppendLine("            {");
        _ = sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"                results.Add(Read{recordName}(reader));"
        );
        _ = sb.AppendLine("            }");
        _ = sb.AppendLine();
        _ = sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"            return new {okType}(results.ToImmutable());"
        );
        _ = sb.AppendLine("        }");
        _ = sb.AppendLine("        catch (Exception ex)");
        _ = sb.AppendLine("        {");
        _ = sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"            return new {errorType}(SqlError.FromException(ex));"
        );
        _ = sb.AppendLine("        }");
        _ = sb.AppendLine("    }");
        _ = sb.AppendLine();

        // Reader method
        _ = sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"    private static {recordName} Read{recordName}(NpgsqlDataReader reader) =>"
        );
        _ = sb.Append(CultureInfo.InvariantCulture, $"        new(");

        first = true;
        var ordinal = 0;
        foreach (var col in columns)
        {
            if (!first)
                _ = sb.Append(", ");
            first = false;

            // Preserve the column name verbatim so generated record fields
            // match the SQLite CLI output (which kept snake_case literally),
            // and so consumers that reference `rec.user_id` etc. keep working.
            var propName = col.Name;
            var readExpr = GetReaderExpression(col, ordinal);
            _ = sb.Append(CultureInfo.InvariantCulture, $"{propName}: {readExpr}");
            ordinal++;
        }
        _ = sb.AppendLine(");");

        _ = sb.AppendLine("}");

        return new Result<string, SqlError>.Ok<string, SqlError>(sb.ToString());
    }

    private static string MapPortableTypeToCSharp(PortableType type, bool isNullable)
    {
        var baseType = type switch
        {
            UuidType => "Guid",
            BooleanType => "bool",
            SmallIntType => "short",
            IntType => "int",
            BigIntType => "long",
            FloatType => "float",
            DoubleType => "double",
            DecimalType => "decimal",
            MoneyType => "decimal",
            SmallMoneyType => "decimal",
            DateType => "DateOnly",
            TimeType => "TimeOnly",
            DateTimeType => "DateTime",
            DateTimeOffsetType => "DateTimeOffset",
            TextType => "string",
            CharType => "string",
            VarCharType => "string",
            NCharType => "string",
            NVarCharType => "string",
            JsonType => "string",
            XmlType => "string",
            BinaryType => "byte[]",
            VarBinaryType => "byte[]",
            BlobType => "byte[]",
            RowVersionType => "byte[]",
            _ => "string",
        };

        // Add nullable suffix for nullable types (including strings but not arrays)
        if (isNullable && !baseType.EndsWith("[]", StringComparison.Ordinal))
        {
            return baseType + "?";
        }

        return baseType;
    }

    private static string GetReaderExpression(DatabaseColumn col, int ordinal)
    {
        var nullCheck = col.IsNullable ? $"reader.IsDBNull({ordinal}) ? null : " : "";

        return col.CSharpType.TrimEnd('?') switch
        {
            "Guid" => $"{nullCheck}reader.GetGuid({ordinal})",
            "bool" => $"{nullCheck}reader.GetBoolean({ordinal})",
            "short" => $"{nullCheck}reader.GetInt16({ordinal})",
            "int" => $"{nullCheck}reader.GetInt32({ordinal})",
            "long" => $"{nullCheck}reader.GetInt64({ordinal})",
            "float" => $"{nullCheck}reader.GetFloat({ordinal})",
            "double" => $"{nullCheck}reader.GetDouble({ordinal})",
            "decimal" => $"{nullCheck}reader.GetDecimal({ordinal})",
            "DateOnly" => $"{nullCheck}DateOnly.FromDateTime(reader.GetDateTime({ordinal}))",
            "TimeOnly" => $"{nullCheck}TimeOnly.FromTimeSpan(reader.GetTimeSpan({ordinal}))",
            "DateTime" => $"{nullCheck}reader.GetDateTime({ordinal})",
            "DateTimeOffset" => $"{nullCheck}reader.GetFieldValue<DateTimeOffset>({ordinal})",
            "TimeSpan" => $"{nullCheck}reader.GetTimeSpan({ordinal})",
            "byte[]" => $"{nullCheck}reader.GetFieldValue<byte[]>({ordinal})",
            "string[]" => $"reader.GetFieldValue<string[]>({ordinal})",
            _ => $"{nullCheck}reader.GetString({ordinal})",
        };
    }

    private static string InferParameterType(
        string paramName,
        IReadOnlyList<DatabaseColumn>? columns = null
    )
    {
        // 1. If we have schema columns and one matches the param name
        // (case-insensitive), use the actual column C# type. Strip the
        // nullable suffix because parameters are non-nullable in method
        // signatures (callers pass concrete values).
        if (columns is not null)
        {
            foreach (var col in columns)
            {
                if (string.Equals(col.Name, paramName, StringComparison.OrdinalIgnoreCase))
                {
                    var t = col.CSharpType;
                    if (t.EndsWith("?", StringComparison.Ordinal))
                    {
                        t = t[..^1];
                    }
                    return t;
                }
            }
        }

        // 2. Fall back to name-based heuristics. Default `*id` -> string
        // because Postgres `text` ids are common and a `string` argument
        // round-trips correctly to both `text` and `uuid` columns (Npgsql
        // handles the cast for the latter when the column type is uuid).
        var lower = paramName.ToLowerInvariant();
        if (lower.EndsWith("id", StringComparison.Ordinal))
            return "string";
        if (
            lower.Contains("limit", StringComparison.Ordinal)
            || lower.Contains("offset", StringComparison.Ordinal)
            || lower.Contains("count", StringComparison.Ordinal)
        )
            return "int";
        return "object";
    }

    private static string ToPascalCase(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        // If the name is already mixed case (no underscores), preserve the
        // existing case and just uppercase the first letter. This keeps
        // identifiers like `givenName` -> `GivenName` and `GivenName` ->
        // `GivenName`, instead of destructively lowercasing the tail.
        if (!name.Contains('_', StringComparison.Ordinal))
        {
            return char.ToUpperInvariant(name[0]) + name[1..];
        }

        // snake_case input: split on underscore and Pascal-case each chunk.
        var parts = name.Split('_');
        var sb = new StringBuilder();
        foreach (var part in parts)
        {
            if (part.Length > 0)
            {
                _ = sb.Append(char.ToUpperInvariant(part[0]));
                if (part.Length > 1)
                    _ = sb.Append(part[1..]);
            }
        }
        return sb.ToString();
    }

    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        // Preserve existing camel/Pascal input, just lowercase the first
        // letter. For snake_case input, fall through to PascalCase then
        // lowercase the leading letter.
        if (!name.Contains('_', StringComparison.Ordinal))
        {
            return char.ToLowerInvariant(name[0]) + name[1..];
        }

        var pascal = ToPascalCase(name);
        if (string.IsNullOrEmpty(pascal))
            return pascal;
        return char.ToLowerInvariant(pascal[0]) + pascal[1..];
    }

    private static string EscapeForPreprocessor(string message)
    {
        if (string.IsNullOrEmpty(message))
            return string.Empty;
        var oneLine = message.Replace('\r', ' ').Replace('\n', ' ');
        return oneLine.Replace('"', '\'');
    }
}

/// <summary>
/// Configuration for PostgreSQL DataProvider code generation.
/// </summary>
internal sealed record PostgresDataProviderConfig
{
    /// <summary>
    /// The connection string to the PostgreSQL database.
    /// </summary>
    public string ConnectionString { get; init; } = string.Empty;

    /// <summary>
    /// List of query configurations.
    /// </summary>
    public List<QueryConfig>? Queries { get; init; }

    /// <summary>
    /// List of table configurations for CRUD generation.
    /// </summary>
    public List<TableConfigItem>? Tables { get; init; }
}

/// <summary>
/// Query configuration.
/// </summary>
internal sealed record QueryConfig
{
    /// <summary>
    /// Query name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Path to SQL file.
    /// </summary>
    public string SqlFile { get; init; } = string.Empty;
}

/// <summary>
/// Table configuration for CRUD generation.
/// </summary>
internal sealed record TableConfigItem
{
    /// <summary>
    /// Schema name.
    /// </summary>
    public string Schema { get; init; } = "public";

    /// <summary>
    /// Table name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Generate INSERT method.
    /// </summary>
    public bool GenerateInsert { get; init; }

    /// <summary>
    /// Generate UPDATE method.
    /// </summary>
    public bool GenerateUpdate { get; init; }

    /// <summary>
    /// Generate DELETE method.
    /// </summary>
    public bool GenerateDelete { get; init; }

    /// <summary>
    /// Generate bulk INSERT method for batch operations.
    /// </summary>
    public bool GenerateBulkInsert { get; init; }

    /// <summary>
    /// Generate bulk UPSERT method for batch insert-or-update operations.
    /// </summary>
    public bool GenerateBulkUpsert { get; init; }

    /// <summary>
    /// Columns to exclude from generation.
    /// </summary>
    public IReadOnlyList<string> ExcludeColumns { get; init; } = [];

    /// <summary>
    /// Primary key columns.
    /// </summary>
    public IReadOnlyList<string> PrimaryKeyColumns { get; init; } = [];
}
