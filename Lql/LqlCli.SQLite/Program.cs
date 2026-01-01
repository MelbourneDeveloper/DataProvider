using System.CommandLine;
using Lql;
using Lql.SQLite;
using Selecta;
using LqlStatementError = Outcome.Result<Lql.LqlStatement, Selecta.SqlError>.Error<
    Lql.LqlStatement,
    Selecta.SqlError
>;
using LqlStatementOk = Outcome.Result<Lql.LqlStatement, Selecta.SqlError>.Ok<
    Lql.LqlStatement,
    Selecta.SqlError
>;
using StringSqlError = Outcome.Result<string, Selecta.SqlError>.Error<string, Selecta.SqlError>;
using StringSqlOk = Outcome.Result<string, Selecta.SqlError>.Ok<string, Selecta.SqlError>;

namespace LqlCli.SQLite;

/// <summary>
/// LQL to SQLite CLI transpiler
/// </summary>
internal static class Program
{
    /// <summary>
    /// Main entry point for the CLI application
    /// </summary>
    /// <param name="args">Command line arguments</param>
    /// <returns>Exit code</returns>
    public static async Task<int> Main(string[] args)
    {
        var inputOption = new Option<FileInfo?>(
            name: "--input",
            description: "Input LQL file to transpile"
        )
        {
            IsRequired = true,
        };
        inputOption.AddAlias("-i");

        var outputOption = new Option<FileInfo?>(
            name: "--output",
            description: "Output SQLite SQL file (optional - prints to console if not specified)"
        )
        {
            IsRequired = false,
        };
        outputOption.AddAlias("-o");

        var validateOption = new Option<bool>(
            name: "--validate",
            description: "Validate the LQL syntax without generating output",
            getDefaultValue: () => false
        );
        validateOption.AddAlias("-v");

        var rootCommand = new RootCommand("LQL to SQLite SQL transpiler")
        {
            inputOption,
            outputOption,
            validateOption,
        };

        rootCommand.SetHandler(
            async (inputFile, outputFile, validate) =>
            {
                var result = await TranspileLqlToSqlite(inputFile!, outputFile, validate)
                    .ConfigureAwait(false);
                Environment.Exit(result);
            },
            inputOption,
            outputOption,
            validateOption
        );

        return await rootCommand.InvokeAsync(args).ConfigureAwait(false);
    }

    /// <summary>
    /// Transpiles LQL file to SQLite SQL
    /// </summary>
    /// <param name="inputFile">Input LQL file</param>
    /// <param name="outputFile">Optional output file</param>
    /// <param name="validate">Whether to only validate syntax</param>
    /// <returns>Exit code (0 = success, 1 = error)</returns>
    private static async Task<int> TranspileLqlToSqlite(
        FileInfo inputFile,
        FileInfo? outputFile,
        bool validate
    )
    {
        try
        {
            if (!inputFile.Exists)
            {
                Console.WriteLine($"❌ Error: Input file '{inputFile.FullName}' does not exist.");
                return 1;
            }

            var lqlContent = await File.ReadAllTextAsync(inputFile.FullName).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(lqlContent))
            {
                Console.WriteLine($"❌ Error: Input file '{inputFile.FullName}' is empty.");
                return 1;
            }

            Console.WriteLine($"📖 Reading LQL from: {inputFile.FullName}");

            // Parse the LQL using Lql
            var parseResult = LqlStatementConverter.ToStatement(lqlContent);

            return parseResult switch
            {
                LqlStatementOk success => await ProcessSuccessfulParse(
                        success.Value,
                        outputFile,
                        validate,
                        inputFile.FullName
                    )
                    .ConfigureAwait(false),
                LqlStatementError failure => HandleParseError(failure.Value),
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Unexpected error: {ex}");
            return 1;
        }
    }

    /// <summary>
    /// Processes a successfully parsed LQL statement
    /// </summary>
    /// <param name="statement">The parsed Lql statement</param>
    /// <param name="outputFile">Optional output file</param>
    /// <param name="validate">Whether to only validate</param>
    /// <param name="inputFileName">Input file name for logging</param>
    /// <returns>Exit code</returns>
    private static async Task<int> ProcessSuccessfulParse(
        LqlStatement statement,
        FileInfo? outputFile,
        bool validate,
        string inputFileName
    )
    {
        if (validate)
        {
            Console.WriteLine($"✅ LQL syntax is valid in: {inputFileName}");
            return 0;
        }

        // Convert to SQLite
        var sqliteResult = statement.ToSQLite();

        return sqliteResult switch
        {
            StringSqlOk success => await OutputSql(success.Value, outputFile).ConfigureAwait(false),
            StringSqlError failure => HandleTranspilationError(failure.Value),
        };
    }

    /// <summary>
    /// Outputs the generated SQL
    /// </summary>
    /// <param name="sql">Generated SQL</param>
    /// <param name="outputFile">Optional output file</param>
    /// <returns>Exit code</returns>
    private static async Task<int> OutputSql(string sql, FileInfo? outputFile)
    {
        var finalSql = sql;

        if (outputFile != null)
        {
            // Ensure the output directory exists
            var directory = outputFile.Directory;
            if (directory != null && !directory.Exists)
            {
                directory.Create();
            }

            await File.WriteAllTextAsync(outputFile.FullName, finalSql).ConfigureAwait(false);
            Console.WriteLine($"✅ SQLite SQL written to: {outputFile.FullName}");
        }
        else
        {
            Console.WriteLine("\n🔄 Generated SQLite SQL:");
            Console.WriteLine("─".PadRight(50, '─'));
            Console.WriteLine(finalSql);
            Console.WriteLine("─".PadRight(50, '─'));
        }

        return 0;
    }

    /// <summary>
    /// Handles parse errors
    /// </summary>
    /// <param name="error">The SQL error</param>
    /// <returns>Exit code</returns>
    private static int HandleParseError(SqlError error)
    {
        Console.WriteLine($"❌ LQL Parse Error: {error.FormattedMessage}");
        if (
            !string.IsNullOrEmpty(error.DetailedMessage)
            && error.DetailedMessage != error.FormattedMessage
        )
        {
            Console.WriteLine($"   Details: {error.DetailedMessage}");
        }
        return 1;
    }

    /// <summary>
    /// Handles transpilation errors
    /// </summary>
    /// <param name="error">The SQL error</param>
    /// <returns>Exit code</returns>
    private static int HandleTranspilationError(SqlError error)
    {
        Console.WriteLine($"❌ SQLite Transpilation Error: {error.FormattedMessage}");
        if (
            !string.IsNullOrEmpty(error.DetailedMessage)
            && error.DetailedMessage != error.FormattedMessage
        )
        {
            Console.WriteLine($"   Details: {error.DetailedMessage}");
        }
        return 1;
    }
}
