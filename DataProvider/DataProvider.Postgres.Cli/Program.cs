using System.CommandLine;
using System.Text;
using System.Text.Json;
using DataProvider.CodeGeneration;
using Npgsql;
using Outcome;
using Selecta;
using SqlParser;
using SqlParser.Ast;

#pragma warning disable CA1849 // Call async methods when in an async method
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities

namespace DataProvider.Postgres.Cli;

/// <summary>
/// PostgreSQL code generation CLI for DataProvider.
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
            description: "Project directory containing sql files and DataProvider.json"
        )
        {
            IsRequired = true,
        };
        var config = new Option<FileInfo>("--config", description: "Path to DataProvider.json")
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
        var root = new RootCommand("DataProvider.Postgres codegen CLI")
        {
            projectDir,
            config,
            outDir,
        };
        root.SetHandler(
            async (DirectoryInfo proj, FileInfo cfg, DirectoryInfo output) =>
            {
                var exit = await RunAsync(proj, cfg, output).ConfigureAwait(false);
                Environment.Exit(exit);
            },
            projectDir,
            config,
            outDir
        );

        return await root.InvokeAsync(args).ConfigureAwait(false);
    }

    private static async Task<int> RunAsync(
        DirectoryInfo projectDir,
        FileInfo configFile,
        DirectoryInfo outDir
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
            if (cfg is null || string.IsNullOrWhiteSpace(cfg.ConnectionString))
            {
                Console.WriteLine("❌ DataProvider.json ConnectionString is required");
                return 1;
            }

            // Verify DB connection
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

                    // Get column metadata from PostgreSQL
                    var colsResult = await GetColumnMetadataAsync(
                            cfg.ConnectionString,
                            sql,
                            parameters
                        )
                        .ConfigureAwait(false);
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

            return hadErrors ? 1 : 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Unexpected error: {ex}");
            return 1;
        }
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

            // Wrap in a CTE to get metadata without executing
            var wrappedSql = $"SELECT * FROM ({metaSql}) AS _meta WHERE 1=0";

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

        // Header with all using statements (including type aliases) at the top
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System.Collections.Immutable;");
        sb.AppendLine("using Npgsql;");
        sb.AppendLine("using Outcome;");
        sb.AppendLine("using Selecta;");
        sb.AppendLine();
        // Result type aliases must come after standard usings but before any type definitions
        // Use fully qualified names since type aliases don't use namespace context
        sb.AppendLine(
            $"using {fileName}Result = Outcome.Result<System.Collections.Immutable.ImmutableList<{recordName}>, Selecta.SqlError>;"
        );
        sb.AppendLine(
            $"using {fileName}Ok = Outcome.Result<System.Collections.Immutable.ImmutableList<{recordName}>, Selecta.SqlError>.Ok<System.Collections.Immutable.ImmutableList<{recordName}>, Selecta.SqlError>;"
        );
        sb.AppendLine(
            $"using {fileName}Error = Outcome.Result<System.Collections.Immutable.ImmutableList<{recordName}>, Selecta.SqlError>.Error<System.Collections.Immutable.ImmutableList<{recordName}>, Selecta.SqlError>;"
        );
        sb.AppendLine();

        // Generate record type
        sb.AppendLine($"/// <summary>");
        sb.AppendLine($"/// Generated record for {fileName} query.");
        sb.AppendLine($"/// </summary>");
        sb.Append($"public sealed record {recordName}(");

        var first = true;
        foreach (var col in columns)
        {
            if (!first)
                sb.Append(", ");
            first = false;

            var propName = ToPascalCase(col.Name);
            sb.Append($"{col.CSharpType} {propName}");
        }
        sb.AppendLine(");");
        sb.AppendLine();

        // Generate extension method
        sb.AppendLine($"/// <summary>");
        sb.AppendLine($"/// Extension methods for {fileName} query.");
        sb.AppendLine($"/// </summary>");
        sb.AppendLine($"public static class {fileName}Extensions");
        sb.AppendLine("{");

        // SQL constant
        sb.AppendLine($"    private const string Sql = @\"");
        sb.AppendLine(sql.Replace("\"", "\"\""));
        sb.AppendLine("\";");
        sb.AppendLine();

        // Async method
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Executes the {fileName} query.");
        sb.AppendLine($"    /// </summary>");
        sb.Append(
            $"    public static async Task<{fileName}Result> {fileName}Async(this NpgsqlConnection conn"
        );

        foreach (var param in parameters)
        {
            var paramType = InferParameterType(param);
            sb.Append($", {paramType} {ToCamelCase(param)}");
        }
        sb.AppendLine(")");
        sb.AppendLine("    {");
        sb.AppendLine("        try");
        sb.AppendLine("        {");
        sb.AppendLine($"            var results = ImmutableList.CreateBuilder<{recordName}>();");
        sb.AppendLine("            await using var cmd = new NpgsqlCommand(Sql, conn);");

        foreach (var param in parameters)
        {
            sb.AppendLine(
                $"            cmd.Parameters.AddWithValue(\"{param}\", {ToCamelCase(param)});"
            );
        }

        sb.AppendLine();
        sb.AppendLine(
            "            await using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);"
        );
        sb.AppendLine("            while (await reader.ReadAsync().ConfigureAwait(false))");
        sb.AppendLine("            {");
        sb.AppendLine($"                results.Add(Read{recordName}(reader));");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine($"            return new {fileName}Ok(results.ToImmutable());");
        sb.AppendLine("        }");
        sb.AppendLine("        catch (Exception ex)");
        sb.AppendLine("        {");
        sb.AppendLine($"            return new {fileName}Error(SqlError.FromException(ex));");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();

        // Reader method
        sb.AppendLine(
            $"    private static {recordName} Read{recordName}(NpgsqlDataReader reader) =>"
        );
        sb.Append($"        new(");

        first = true;
        var ordinal = 0;
        foreach (var col in columns)
        {
            if (!first)
                sb.Append(", ");
            first = false;

            var propName = ToPascalCase(col.Name);
            var readExpr = GetReaderExpression(col, ordinal);
            sb.Append($"{propName}: {readExpr}");
            ordinal++;
        }
        sb.AppendLine(");");

        sb.AppendLine("}");

        return new Result<string, SqlError>.Ok<string, SqlError>(sb.ToString());
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

    private static string InferParameterType(string paramName)
    {
        var lower = paramName.ToLowerInvariant();
        if (lower.EndsWith("id", StringComparison.Ordinal))
            return "Guid";
        if (lower.Contains("limit") || lower.Contains("offset") || lower.Contains("count"))
            return "int";
        return "object";
    }

    private static string ToPascalCase(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        var parts = name.Split('_');
        var sb = new StringBuilder();
        foreach (var part in parts)
        {
            if (part.Length > 0)
            {
                sb.Append(char.ToUpperInvariant(part[0]));
                if (part.Length > 1)
                    sb.Append(part[1..].ToLowerInvariant());
            }
        }
        return sb.ToString();
    }

    private static string ToCamelCase(string name)
    {
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
    /// Columns to exclude from generation.
    /// </summary>
    public IReadOnlyList<string> ExcludeColumns { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Primary key columns.
    /// </summary>
    public IReadOnlyList<string> PrimaryKeyColumns { get; init; } = Array.Empty<string>();
}
