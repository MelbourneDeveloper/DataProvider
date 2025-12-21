using System.CommandLine;
using System.Text.Json;
using DataProvider.CodeGeneration;
using DataProvider.SQLite.Parsing;
using Outcome;
using Selecta;

#pragma warning disable CA1849 // Call async methods when in an async method

namespace DataProvider.SQLite.Cli;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static async Task<int> Main(string[] args)
    {
        var projectDir = new Option<DirectoryInfo>(
            "--project-dir",
            description: "Project directory containing sql, grouping, and DataProvider.json"
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
        var root = new RootCommand("DataProvider.SQLite codegen CLI")
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
            var cfg = JsonSerializer.Deserialize<SourceGeneratorDataProviderConfiguration>(
                cfgText,
                JsonOptions
            );
            if (cfg is null || string.IsNullOrWhiteSpace(cfg.ConnectionString))
            {
                Console.WriteLine("❌ DataProvider.json ConnectionString is required");
                return 1;
            }

            // Verify DB exists and is accessible
            try
            {
                using var conn = new Microsoft.Data.Sqlite.SqliteConnection(cfg.ConnectionString);
                await conn.OpenAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to open database: {ex.Message}");
                return 1;
            }

            // Gather files
            var sqlFiles = Directory.GetFiles(
                projectDir.FullName,
                "*.sql",
                SearchOption.AllDirectories
            );
            var groupingFiles = Directory
                .GetFiles(projectDir.FullName, "*.grouping.json", SearchOption.AllDirectories)
                .Select(p =>
                {
                    var fileName = Path.GetFileName(p);
                    var baseName = fileName.EndsWith(
                        ".grouping.json",
                        StringComparison.OrdinalIgnoreCase
                    )
                        ? fileName[..^".grouping.json".Length]
                        : Path.GetFileNameWithoutExtension(fileName);
                    return new { Path = p, Base = baseName };
                })
                .ToLookup(x => x.Base, x => x.Path);

            var parser = new SqliteAntlrParser();

            var hadErrors = false;

            foreach (var sqlPath in sqlFiles)
            {
                try
                {
                    var sql = await File.ReadAllTextAsync(sqlPath).ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(sql))
                        continue;

                    var baseName = Path.GetFileNameWithoutExtension(sqlPath);
                    // Normalize base name to ignore optional ".generated" suffix from intermediate files
                    if (baseName.EndsWith(".generated", StringComparison.OrdinalIgnoreCase))
                    {
                        baseName = baseName[..^".generated".Length];
                    }
                    if (
                        string.Equals(baseName, "schema", StringComparison.OrdinalIgnoreCase)
                        || baseName.EndsWith("_schema", StringComparison.OrdinalIgnoreCase)
                    )
                    {
                        // Skip schema files; they're only for DB initialization
                        continue;
                    }
                    var parseResult = parser.ParseSql(sql);
                    if (
                        parseResult
                        is Result<SelectStatement, string>.Error<
                            SelectStatement,
                            string
                        > parseFailure
                    )
                    {
                        Console.WriteLine(
                            $"Error parsing SQL file '{sqlPath}': {parseFailure.Value}"
                        );
                        continue;
                    }
                    var stmt = (
                        (Result<SelectStatement, string>.Ok<SelectStatement, string>)parseResult
                    ).Value;

                    var colsResult = await SqliteCodeGenerator
                        .GetColumnMetadataFromSqlAsync(cfg.ConnectionString, sql, stmt.Parameters)
                        .ConfigureAwait(false);
                    if (
                        colsResult
                        is not Result<IReadOnlyList<DatabaseColumn>, SqlError>.Ok<
                            IReadOnlyList<DatabaseColumn>,
                            SqlError
                        > cols
                    )
                    {
                        var err = (
                            colsResult
                            as Result<IReadOnlyList<DatabaseColumn>, SqlError>.Error<
                                IReadOnlyList<DatabaseColumn>,
                                SqlError
                            >
                        )!.Value;
                        var prettyMeta = FormatSqliteMetadataMessage(err.DetailedMessage);
                        Console.WriteLine($"❌ {prettyMeta}");
                        // Also emit an MSBuild-formatted error so IDE Problem Matchers pick it up
                        Console.Error.WriteLine($"{sqlPath}(1,1): error DP0001: {prettyMeta}");
                        // Emit a compile-time error file so MSBuild surfaces the exact problem
                        var errorFile = Path.Combine(outDir.FullName, baseName + ".g.cs");
                        var content =
                            $"// Auto-generated due to SQL error in {sqlPath}\n#error {EscapeForPreprocessor(prettyMeta)}\n";
                        await File.WriteAllTextAsync(errorFile, content).ConfigureAwait(false);
                        hadErrors = true;
                        continue;
                    }

                    GroupingConfig? grouping = null;
                    if (groupingFiles.Contains(baseName))
                    {
                        var gpath = groupingFiles[baseName].First();
                        var gtext = await File.ReadAllTextAsync(gpath).ConfigureAwait(false);
                        grouping = JsonSerializer.Deserialize<GroupingConfig>(gtext, JsonOptions);
                    }

                    var gen = SqliteCodeGenerator.GenerateCodeWithMetadata(
                        baseName,
                        sql,
                        stmt,
                        cfg.ConnectionString,
                        cols.Value,
                        hasCustomImplementation: false,
                        grouping
                    );
                    if (gen is Result<string, SqlError>.Ok<string, SqlError> success)
                    {
                        var target = Path.Combine(outDir.FullName, baseName + ".g.cs");
                        await File.WriteAllTextAsync(target, success.Value).ConfigureAwait(false);
                        Console.WriteLine($"✅ Generated {target}");
                    }
                    else if (gen is Result<string, SqlError>.Error<string, SqlError> failure)
                    {
                        var prettyGen = FormatSqliteMetadataMessage(failure.Value.DetailedMessage);
                        Console.WriteLine($"❌ {prettyGen}");
                        // Also emit an MSBuild-formatted error so IDE Problem Matchers pick it up
                        Console.Error.WriteLine($"{sqlPath}(1,1): error DL0002: {prettyGen}");
                        // Emit a compile-time error file so MSBuild surfaces the exact problem
                        var errorFile = Path.Combine(outDir.FullName, baseName + ".g.cs");
                        var content =
                            $"// Auto-generated due to SQL generation error in {sqlPath}\n#error {EscapeForPreprocessor(prettyGen)}\n";
                        await File.WriteAllTextAsync(errorFile, content).ConfigureAwait(false);
                        hadErrors = true;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error processing {sqlPath}: {ex.Message}");
                    // Emit a compile-time error file so MSBuild surfaces the exact problem
                    var baseName = Path.GetFileNameWithoutExtension(sqlPath);
                    if (baseName.EndsWith(".generated", StringComparison.OrdinalIgnoreCase))
                    {
                        baseName = baseName[..^".generated".Length];
                    }
                    var errorFile = Path.Combine(outDir.FullName, baseName + ".g.cs");
                    var content =
                        $"// Auto-generated due to unexpected error in {sqlPath}\n#error {EscapeForPreprocessor(ex.Message)}\n";
                    Console.Error.WriteLine($"{sqlPath}(1,1): error DL0003: {ex.Message}");
                    await File.WriteAllTextAsync(errorFile, content).ConfigureAwait(false);
                    hadErrors = true;
                }
            }

            // Generate table operations if configured
            if (cfg.Tables?.Count > 0)
            {
                Console.WriteLine($"🔧 Processing {cfg.Tables.Count} table configurations...");

                foreach (var tableConfigItem in cfg.Tables)
                {
                    try
                    {
                        Console.WriteLine(
                            $"🔧 Generating operations for table {tableConfigItem.Name}..."
                        );

                        // Use SQLite's native schema inspection to get table metadata
                        using var conn = new Microsoft.Data.Sqlite.SqliteConnection(
                            cfg.ConnectionString
                        );
                        await conn.OpenAsync().ConfigureAwait(false);

                        var columns = new List<DatabaseColumn>();
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = $"PRAGMA table_info({tableConfigItem.Name})";
                            using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);

                            while (await reader.ReadAsync().ConfigureAwait(false))
                            {
                                var columnName = reader.GetString(1); // name column
                                var sqliteType = reader.GetString(2); // type column
                                var notNull = reader.GetInt32(3) == 1; // notnull column
                                var isPrimaryKey = reader.GetInt32(5) > 0; // pk column

                                var csharpType = MapSqliteTypeToCSharpType(sqliteType, !notNull);

                                columns.Add(
                                    new DatabaseColumn
                                    {
                                        Name = columnName,
                                        SqlType = sqliteType,
                                        CSharpType = csharpType,
                                        IsNullable = !notNull,
                                        IsPrimaryKey = isPrimaryKey,
                                        IsIdentity =
                                            isPrimaryKey
                                            && sqliteType.Contains(
                                                "INTEGER",
                                                StringComparison.OrdinalIgnoreCase
                                            ),
                                        IsComputed = false,
                                    }
                                );
                            }
                        }

                        if (columns.Count == 0)
                        {
                            Console.WriteLine(
                                $"❌ Table {tableConfigItem.Name} not found or has no columns"
                            );
                            hadErrors = true;
                            continue;
                        }

                        var table = new DatabaseTable
                        {
                            Schema = "main",
                            Name = tableConfigItem.Name,
                            Columns = columns.AsReadOnly(),
                        };

                        // Convert TableConfigItem to TableConfig
                        var tableConfig = new TableConfig
                        {
                            Schema = tableConfigItem.Schema,
                            Name = tableConfigItem.Name,
                            GenerateInsert = tableConfigItem.GenerateInsert,
                            GenerateUpdate = tableConfigItem.GenerateUpdate,
                            GenerateDelete = tableConfigItem.GenerateDelete,
                            ExcludeColumns = tableConfigItem.ExcludeColumns.ToList().AsReadOnly(),
                            PrimaryKeyColumns = tableConfigItem
                                .PrimaryKeyColumns.ToList()
                                .AsReadOnly(),
                        };

                        // Generate table operations
                        var tableOperationGenerator = new DefaultTableOperationGenerator(
                            "SqliteConnection"
                        );
                        var operationsResult = tableOperationGenerator.GenerateTableOperations(
                            table,
                            tableConfig
                        );
                        if (
                            operationsResult
                            is Result<string, SqlError>.Ok<string, SqlError> operationsSuccess
                        )
                        {
                            var target = Path.Combine(
                                outDir.FullName,
                                tableConfigItem.Name + "Operations.g.cs"
                            );
                            await File.WriteAllTextAsync(target, operationsSuccess.Value)
                                .ConfigureAwait(false);
                            Console.WriteLine($"✅ Generated {target}");
                        }
                        else if (
                            operationsResult
                            is Result<string, SqlError>.Error<string, SqlError> operationsFailure
                        )
                        {
                            Console.WriteLine(
                                $"❌ Failed to generate table operations for {tableConfigItem.Name}: {operationsFailure.Value.Message}"
                            );
                            hadErrors = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(
                            $"❌ Error generating table operations for {tableConfigItem.Name}: {ex.Message}"
                        );
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

    private static string EscapeForPreprocessor(string message)
    {
        if (string.IsNullOrEmpty(message))
            return string.Empty;
        // Preprocessor directives cannot span multiple lines; replace newlines and quotes
        var oneLine = message.Replace('\r', ' ').Replace('\n', ' ');
        return oneLine.Replace('"', '\'');
    }

    private static string FormatSqliteMetadataMessage(string detailed)
    {
        if (string.IsNullOrWhiteSpace(detailed))
            return detailed;

        var msg = detailed.Trim();

        const string sqlitePrefix = "SQLite Error ";
        var idx = msg.IndexOf(sqlitePrefix, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
            return msg; // Not a known SQLite pattern; return as-is

        // Extract code
        var codeStart = idx + sqlitePrefix.Length;
        var colonAfterCode = msg.IndexOf(':', codeStart);
        if (colonAfterCode < 0)
            return msg;
        var codeText = msg[codeStart..colonAfterCode].Trim();

        // Extract inner quoted detail after colon, between single quotes if present
        var firstQuote = msg.IndexOf('\'', colonAfterCode + 1);
        var lastQuote = firstQuote >= 0 ? msg.IndexOf('\'', firstQuote + 1) : -1;
        string detail =
            lastQuote > firstQuote && firstQuote >= 0
                ? msg.Substring(firstQuote + 1, lastQuote - firstQuote - 1)
                : msg[(colonAfterCode + 1)..].Trim().Trim('.');

        // If detail starts with "no such column: ", rephrase
        const string noSuch = "no such column: ";
        string head;
        if (detail.StartsWith(noSuch, StringComparison.OrdinalIgnoreCase))
        {
            var col = detail[noSuch.Length..].Trim();
            head = $"No such column {col}.";
        }
        else
        {
            // Capitalize first letter and ensure trailing period
            if (detail.Length > 0)
            {
                var first = char.ToUpperInvariant(detail[0]);
                var rest = detail.Length > 1 ? detail[1..] : string.Empty;
                detail = first + rest;
            }
            head = detail.EndsWith('.') ? detail : detail + ".";
        }

        // Keep the leading context up to the sqlite prefix (e.g., "Failed to get column metadata: ")
        var contextPart = msg[..idx].Trim();
        if (contextPart.EndsWith(':'))
            contextPart = contextPart[..^1].TrimEnd();

        var tail = string.IsNullOrEmpty(contextPart) ? string.Empty : $" {contextPart}";
        var final = string.IsNullOrEmpty(tail)
            ? head
            : $"{head}{tail} (SQLite generation Error {codeText})";
        return final;
    }

    /// <summary>
    /// Maps SQLite types to C# types
    /// </summary>
    private static string MapSqliteTypeToCSharpType(string sqliteType, bool isNullable)
    {
        var baseType = sqliteType.ToUpperInvariant() switch
        {
            var t when t.Contains("INT", StringComparison.OrdinalIgnoreCase) => "long",
            var t
                when t.Contains("REAL", StringComparison.OrdinalIgnoreCase)
                    || t.Contains("FLOAT", StringComparison.OrdinalIgnoreCase)
                    || t.Contains("DOUBLE", StringComparison.OrdinalIgnoreCase) => "double",
            var t
                when t.Contains("DECIMAL", StringComparison.OrdinalIgnoreCase)
                    || t.Contains("NUMERIC", StringComparison.OrdinalIgnoreCase) => "double",
            var t when t.Contains("BOOL", StringComparison.OrdinalIgnoreCase) => "bool",
            var t
                when t.Contains("DATE", StringComparison.OrdinalIgnoreCase)
                    || t.Contains("TIME", StringComparison.OrdinalIgnoreCase) => "string", // SQLite stores dates as text
            var t when t.Contains("BLOB", StringComparison.OrdinalIgnoreCase) => "byte[]",
            _ => "string",
        };

        if (isNullable && baseType != "string" && baseType != "byte[]")
        {
            return baseType + "?";
        }

        return baseType;
    }
}
