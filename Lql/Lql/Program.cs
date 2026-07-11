using System.CommandLine;

namespace Lql;

/// <summary>
/// Unified LQL transpiler CLI tool. Wires one subcommand per supported
/// target platform (<c>postgres</c>, <c>sqlite</c>) onto a single
/// <see cref="RootCommand"/> and dispatches. Replaces the per-platform
/// tools formerly shipped as <c>Nimblesite.Lql.Cli.Postgres</c> and
/// <c>Nimblesite.Lql.Cli.SQLite</c>.
/// </summary>
internal static class Program
{
    /// <summary>
    /// Entry point.
    /// </summary>
    /// <param name="args">Command line arguments.</param>
    /// <returns>Exit code.</returns>
    public static Task<int> Main(string[] args)
    {
        var root = new RootCommand(
            "LQL transpiler CLI tool. Transpiles LQL to SQL for a target "
                + "database platform. Pick a platform subcommand: postgres | sqlite."
        )
        {
            PostgresCli.BuildCommand(),
            SqliteCli.BuildCommand(),
        };

        return root.InvokeAsync(args);
    }
}
