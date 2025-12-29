using Microsoft.Data.Sqlite;
using Migration.Postgres;
using Migration.SQLite;
using Npgsql;

namespace Migration.Cli;

/// <summary>
/// CLI tool to create databases from YAML schema definitions.
/// This is the ONLY canonical tool for database creation - all projects MUST use this.
/// </summary>
public static class Program
{
    /// <summary>
    /// Entry point - creates database from YAML schema file.
    /// Usage: dotnet run -- --schema path/to/schema.yaml --output path/to/database.db --provider [sqlite|postgres]
    /// </summary>
    public static int Main(string[] args)
    {
        var parseResult = ParseArguments(args);

        return parseResult switch
        {
            ParseResult.Success success => ExecuteMigration(success),
            ParseResult.Failure failure => ShowError(failure),
            ParseResult.HelpRequested => ShowUsage(),
        };
    }

    private static int ExecuteMigration(ParseResult.Success args)
    {
        Console.WriteLine("Migration.Cli - Database Schema Tool");
        Console.WriteLine($"  Schema:   {args.SchemaPath}");
        Console.WriteLine($"  Output:   {args.OutputPath}");
        Console.WriteLine($"  Provider: {args.Provider}");
        Console.WriteLine();

        if (!File.Exists(args.SchemaPath))
        {
            Console.WriteLine($"Error: Schema file not found: {args.SchemaPath}");
            return 1;
        }

        SchemaDefinition schema;
        try
        {
            var yamlContent = File.ReadAllText(args.SchemaPath);
            schema = SchemaYamlSerializer.FromYaml(yamlContent);
            Console.WriteLine($"Loaded schema '{schema.Name}' with {schema.Tables.Count} tables");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: Failed to parse YAML schema: {ex}");
            return 1;
        }

        return args.Provider.ToLowerInvariant() switch
        {
            "sqlite" => CreateSqliteDatabase(schema, args.OutputPath),
            "postgres" => CreatePostgresDatabase(schema, args.OutputPath),
            _ => ShowProviderError(args.Provider),
        };
    }

    private static int CreateSqliteDatabase(SchemaDefinition schema, string outputPath)
    {
        try
        {
            // Delete existing file to start fresh
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
                Console.WriteLine($"Deleted existing database: {outputPath}");
            }

            // Ensure directory exists
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var connectionString = $"Data Source={outputPath}";
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var tablesCreated = 0;
            foreach (var table in schema.Tables)
            {
                var ddl = SqliteDdlGenerator.Generate(new CreateTableOperation(table));
                using var cmd = connection.CreateCommand();
                cmd.CommandText = ddl;
                cmd.ExecuteNonQuery();
                Console.WriteLine($"  Created table: {table.Name}");
                tablesCreated++;
            }

            Console.WriteLine();
            Console.WriteLine($"Successfully created SQLite database with {tablesCreated} tables");
            Console.WriteLine($"  Output: {outputPath}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: SQLite migration failed: {ex}");
            return 1;
        }
    }

    private static int CreatePostgresDatabase(SchemaDefinition schema, string connectionString)
    {
        try
        {
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();

            Console.WriteLine("Connected to PostgreSQL database");

            var result = PostgresDdlGenerator.MigrateSchema(
                connection: connection,
                schema: schema,
                onTableCreated: table => Console.WriteLine($"  Created table: {table}"),
                onTableFailed: (table, ex) => Console.WriteLine($"  Failed table: {table} - {ex}")
            );

            Console.WriteLine();

            if (result.Success)
            {
                Console.WriteLine($"Successfully created PostgreSQL database with {result.TablesCreated} tables");
                return 0;
            }
            else
            {
                Console.WriteLine("PostgreSQL migration completed with errors:");
                foreach (var error in result.Errors)
                {
                    Console.WriteLine($"  {error}");
                }

                return result.TablesCreated > 0 ? 0 : 1;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: PostgreSQL connection/migration failed: {ex}");
            return 1;
        }
    }

    private static int ShowProviderError(string provider)
    {
        Console.WriteLine($"Error: Unknown provider '{provider}'");
        Console.WriteLine("Valid providers: sqlite, postgres");
        return 1;
    }

    private static int ShowError(ParseResult.Failure failure)
    {
        Console.WriteLine($"Error: {failure.Message}");
        Console.WriteLine();
        return ShowUsage();
    }

    private static int ShowUsage()
    {
        Console.WriteLine("Migration.Cli - Database Schema Tool");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run --project Migration/Migration.Cli/Migration.Cli.csproj -- \\");
        Console.WriteLine("      --schema path/to/schema.yaml \\");
        Console.WriteLine("      --output path/to/database.db \\");
        Console.WriteLine("      --provider [sqlite|postgres]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --schema    Path to YAML schema definition file (required)");
        Console.WriteLine("  --output    Path to output database file (SQLite) or connection string (Postgres)");
        Console.WriteLine("  --provider  Database provider: sqlite or postgres (default: sqlite)");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  # SQLite (file path)");
        Console.WriteLine("  dotnet run -- --schema my-schema.yaml --output ./build.db --provider sqlite");
        Console.WriteLine();
        Console.WriteLine("  # PostgreSQL (connection string)");
        Console.WriteLine("  dotnet run -- --schema my-schema.yaml \\");
        Console.WriteLine("      --output \"Host=localhost;Database=mydb;Username=user;Password=pass\" \\");
        Console.WriteLine("      --provider postgres");
        Console.WriteLine();
        Console.WriteLine("YAML Schema Format:");
        Console.WriteLine("  name: my_schema");
        Console.WriteLine("  tables:");
        Console.WriteLine("    - name: Users");
        Console.WriteLine("      columns:");
        Console.WriteLine("        - name: Id");
        Console.WriteLine("          type: Uuid");
        Console.WriteLine("          isNullable: false");
        Console.WriteLine("        - name: Email");
        Console.WriteLine("          type: VarChar(255)");
        Console.WriteLine("          isNullable: false");
        Console.WriteLine("      primaryKey:");
        Console.WriteLine("        columns: [Id]");
        return 1;
    }

    private static ParseResult ParseArguments(string[] args)
    {
        string? schemaPath = null;
        string? outputPath = null;
        var provider = "sqlite";

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            switch (arg)
            {
                case "--schema" or "-s":
                    if (i + 1 >= args.Length)
                    {
                        return new ParseResult.Failure("--schema requires a path argument");
                    }

                    schemaPath = args[++i];
                    break;

                case "--output" or "-o":
                    if (i + 1 >= args.Length)
                    {
                        return new ParseResult.Failure("--output requires a path argument");
                    }

                    outputPath = args[++i];
                    break;

                case "--provider" or "-p":
                    if (i + 1 >= args.Length)
                    {
                        return new ParseResult.Failure("--provider requires an argument (sqlite or postgres)");
                    }

                    provider = args[++i];
                    break;

                case "--help" or "-h":
                    return new ParseResult.HelpRequested();

                default:
                    if (arg.StartsWith('-'))
                    {
                        return new ParseResult.Failure($"Unknown option: {arg}");
                    }

                    break;
            }
        }

        if (string.IsNullOrEmpty(schemaPath))
        {
            return new ParseResult.Failure("--schema is required");
        }

        if (string.IsNullOrEmpty(outputPath))
        {
            return new ParseResult.Failure("--output is required");
        }

        return new ParseResult.Success(schemaPath, outputPath, provider);
    }
}

/// <summary>
/// Argument parsing result - closed type hierarchy.
/// </summary>
public abstract record ParseResult
{
    private ParseResult()
    {
    }

    /// <summary>Successfully parsed arguments.</summary>
    public sealed record Success(string SchemaPath, string OutputPath, string Provider) : ParseResult;

    /// <summary>Parse error.</summary>
    public sealed record Failure(string Message) : ParseResult;

    /// <summary>Help requested.</summary>
    public sealed record HelpRequested : ParseResult;
}
