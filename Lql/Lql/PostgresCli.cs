using System.CommandLine;
using Nimblesite.Lql.Postgres;

namespace Lql;

/// <summary>
/// <c>postgres</c> subcommand of the unified <c>Lql</c> tool. Transpiles
/// LQL to PostgreSQL SQL. Built via <see cref="BuildCommand"/> and
/// attached to the root command by <see cref="Program"/>.
/// </summary>
internal static class PostgresCli
{
    /// <summary>
    /// Builds the <c>postgres</c> subcommand. Exposed so <see cref="Program"/>
    /// can wire it onto the root command.
    /// </summary>
    /// <returns>Configured <see cref="Command"/>.</returns>
    public static Command BuildCommand()
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

        var command = new Command("postgres", "LQL to PostgreSQL SQL transpiler")
        {
            inputOption,
            outputOption,
            validateOption,
        };

        command.SetHandler(
            async (inputFile, outputFile, validate) =>
            {
                var exit = await TranspileRunner
                    .RunAsync(
                        "PostgreSQL",
                        inputFile!,
                        outputFile,
                        validate,
                        statement => statement.ToPostgreSql()
                    )
                    .ConfigureAwait(false);
                Environment.ExitCode = exit;
            },
            inputOption,
            outputOption,
            validateOption
        );

        return command;
    }
}
