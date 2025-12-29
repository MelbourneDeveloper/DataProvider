using Microsoft.Data.Sqlite;
using Migration;
using Migration.SQLite;

namespace Migration.Cli;

/// <summary>
/// CLI tool to create build databases using the Migration system.
/// This runs BEFORE code generation to ensure tables exist for the DataProvider generator.
/// </summary>
public static class Program
{
    /// <summary>
    /// Entry point - creates build database from schema definition.
    /// Usage: dotnet run -- --schema [gatekeeper|clinical|scheduling|example] --output path/to/db.db
    /// </summary>
    public static int Main(string[] args)
    {
        if (args.Length < 4)
        {
            Console.WriteLine("Usage: BuildDb.Cli --schema <schema-name> --output <db-path>");
            Console.WriteLine("Schemas: gatekeeper, clinical, scheduling, example");
            return 1;
        }

        string? schemaName = null;
        string? outputPath = null;

        for (var i = 0; i < args.Length - 1; i++)
        {
            switch (args[i])
            {
                case "--schema":
                    schemaName = args[i + 1];
                    break;
                case "--output":
                    outputPath = args[i + 1];
                    break;
            }
        }

        if (string.IsNullOrEmpty(schemaName) || string.IsNullOrEmpty(outputPath))
        {
            Console.WriteLine("Error: Both --schema and --output are required");
            return 1;
        }

        var schema = GetSchema(schemaName);
        if (schema is null)
        {
            Console.WriteLine($"Error: Unknown schema '{schemaName}'");
            Console.WriteLine("Valid schemas: gatekeeper, clinical, scheduling, example");
            return 1;
        }

        Console.WriteLine($"Creating {schemaName} build database at {outputPath}");

        // Delete existing file to start fresh
        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }

        var connectionString = $"Data Source={outputPath}";
        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        foreach (var table in schema.Tables)
        {
            var ddl = SqliteDdlGenerator.Generate(new CreateTableOperation(table));
            using var cmd = connection.CreateCommand();
            cmd.CommandText = ddl;
            cmd.ExecuteNonQuery();
            Console.WriteLine($"  Created table: {table.Name}");
        }

        Console.WriteLine(
            $"Successfully created {schemaName} build database with {schema.Tables.Count} tables"
        );
        return 0;
    }
}
