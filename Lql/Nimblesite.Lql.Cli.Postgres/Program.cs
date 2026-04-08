using System.CommandLine;
using Nimblesite.Lql.Core;
using Nimblesite.Lql.Postgres;
using Nimblesite.Sql.Model;
using LqlStatementError = Outcome.Result<
    Nimblesite.Lql.Core.LqlStatement,
    Nimblesite.Sql.Model.SqlError
>.Error<Nimblesite.Lql.Core.LqlStatement, Nimblesite.Sql.Model.SqlError>;
using LqlStatementOk = Outcome.Result<
    Nimblesite.Lql.Core.LqlStatement,
    Nimblesite.Sql.Model.SqlError
>.Ok<Nimblesite.Lql.Core.LqlStatement, Nimblesite.Sql.Model.SqlError>;
using StringSqlError = Outcome.Result<string, Nimblesite.Sql.Model.SqlError>.Error<
    string,
    Nimblesite.Sql.Model.SqlError
>;
using StringSqlOk = Outcome.Result<string, Nimblesite.Sql.Model.SqlError>.Ok<
    string,
    Nimblesite.Sql.Model.SqlError
>;

namespace Nimblesite.Lql.Cli.Postgres;

/// <summary>
/// LQL to PostgreSQL CLI transpiler
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
            description: "Output PostgreSQL SQL file (optional - prints to console if not specified)"
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

        var rootCommand = new RootCommand("LQL to PostgreSQL SQL transpiler")
        {
            inputOption,
            outputOption,
            validateOption,
        };

        rootCommand.SetHandler(
            async (inputFile, outputFile, validate) =>
            {
                var result = await TranspileLqlToPostgres(inputFile!, outputFile, validate)
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
    /// Transpiles LQL file to PostgreSQL SQL
    /// </summary>
    /// <param name="inputFile">Input LQL file</param>
    /// <param name="outputFile">Optional output file</param>
    /// <param name="validate">Whether to only validate syntax</param>
    /// <returns>Exit code (0 = success, 1 = error)</returns>
    private static async Task<int> TranspileLqlToPostgres(
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

            // Parse the LQL using Nimblesite.Lql.Core
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

        // Convert to PostgreSQL
        var pgResult = statement.ToPostgreSql();

        return pgResult switch
        {
            StringSqlOk success => await OutputSql(success.Value, outputFile).ConfigureAwait(false),
            StringSqlError failure => HandleTranspilationError(failure.Value),
        };
    }

    /// <summary>
    /// Outputs the generated SQL
    /// </summary>
    private static async Task<int> OutputSql(string sql, FileInfo? outputFile)
    {
        var finalSql = sql;

        if (outputFile != null)
        {
            var directory = outputFile.Directory;
            if (directory != null && !directory.Exists)
            {
                directory.Create();
            }

            await File.WriteAllTextAsync(outputFile.FullName, finalSql).ConfigureAwait(false);
            Console.WriteLine($"✅ PostgreSQL SQL written to: {outputFile.FullName}");
        }
        else
        {
            Console.WriteLine("\n🔄 Generated PostgreSQL SQL:");
            Console.WriteLine("─".PadRight(50, '─'));
            Console.WriteLine(finalSql);
            Console.WriteLine("─".PadRight(50, '─'));
        }

        return 0;
    }

    /// <summary>
    /// Handles parse errors
    /// </summary>
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
    private static int HandleTranspilationError(SqlError error)
    {
        Console.WriteLine($"❌ PostgreSQL Transpilation Error: {error.FormattedMessage}");
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
