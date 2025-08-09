using System.Collections.Immutable;
using System.Text.Json;
using Microsoft.CodeAnalysis;

namespace DataProvider.SqlServer;

/// <summary>
/// Incremental source generator for SQL Server that scans AdditionalFiles for .sql files and configuration
/// and produces strongly-typed data access extension methods.
/// </summary>
[Generator]
public class DataProviderIncrementalSourceGenerator : IIncrementalGenerator
{
    /// <summary>
    /// Initializes the incremental generator pipeline, wiring up inputs for SQL files and configuration
    /// and registering the output step.
    /// </summary>
    /// <param name="context">The initialization context provided by Roslyn.</param>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Collect all SQL files and configuration
        var sqlFiles = context
            .AdditionalTextsProvider.Where(file =>
                file.Path.EndsWith(".sql", StringComparison.OrdinalIgnoreCase)
            )
            .Collect();

        var configFiles = context
            .AdditionalTextsProvider.Where(file =>
                file.Path.EndsWith("DataProvider.json", StringComparison.OrdinalIgnoreCase)
            )
            .Collect();

        var groupingFiles = context
            .AdditionalTextsProvider.Where(file =>
                file.Path.EndsWith(".grouping.json", StringComparison.OrdinalIgnoreCase)
            )
            .Collect();

        // Combine SQL files and config
        var combined = sqlFiles.Combine(configFiles).Combine(groupingFiles);

        // Generate code for all SQL files together
        context.RegisterSourceOutput(combined, GenerateCodeForAllSqlFiles);
    }

    private void GenerateCodeForAllSqlFiles(
        SourceProductionContext context,
        (
            (
                ImmutableArray<AdditionalText> SqlFiles,
                ImmutableArray<AdditionalText> ConfigFiles
            ) Left,
            ImmutableArray<AdditionalText> GroupingFiles
        ) data
    )
    {
        var (left, groupingFiles) = data;
        var (sqlFiles, configFiles) = left;

        // Read configuration and create appropriate parser
        SourceGeneratorDataProviderConfiguration? config = null;
        if (configFiles.Length > 0)
        {
            var configContent = configFiles[0].GetText()?.ToString();
            if (!string.IsNullOrEmpty(configContent))
            {
                try
                {
                    config = JsonSerializer.Deserialize<SourceGeneratorDataProviderConfiguration>(
                        configContent!
                    );
                }
                catch (JsonException ex)
                {
                    context.ReportDiagnostic(
                        Diagnostic.Create(
                            new DiagnosticDescriptor(
                                "DataProvider002",
                                "Configuration parsing failed",
                                "Failed to parse DataProvider.json: {0}",
                                "DataProvider",
                                DiagnosticSeverity.Error,
                                true
                            ),
                            Location.None,
                            ex.Message
                        )
                    );
                    return;
                }
            }
        }

        if (config == null)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "DataProvider003",
                        "Configuration missing",
                        "DataProvider.json configuration file is required for code generation",
                        "DataProvider",
                        DiagnosticSeverity.Error,
                        true
                    ),
                    Location.None
                )
            );
            return;
        }

        // Delegate the actual generation work - call the existing method directly
        GenerateCodeForAllSqlFiles(context, ((sqlFiles, configFiles), groupingFiles));
    }
}
