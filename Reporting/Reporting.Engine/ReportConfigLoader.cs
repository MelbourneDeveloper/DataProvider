using System.Collections.Immutable;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Outcome;
using Selecta;

namespace Reporting.Engine;

using LoadResult = Result<ReportDefinition, SqlError>;
using LoadError = Result<ReportDefinition, SqlError>.Error<ReportDefinition, SqlError>;
using LoadOk = Result<ReportDefinition, SqlError>.Ok<ReportDefinition, SqlError>;

/// <summary>
/// Loads report definitions from JSON files.
/// </summary>
public static class ReportConfigLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>
    /// Loads a report definition from a JSON file path.
    /// </summary>
    /// <param name="filePath">Absolute path to the report JSON file.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <returns>Result containing the parsed report definition or an error.</returns>
    public static LoadResult LoadFromFile(string filePath, ILogger logger)
    {
        logger.LogInformation("Loading report from {FilePath}", filePath);

        try
        {
            var json = File.ReadAllText(filePath);
            return LoadFromJson(json: json, logger: logger);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to read report file {FilePath}", filePath);
            return new LoadError(SqlError.FromException(ex));
        }
    }

    /// <summary>
    /// Loads a report definition from a JSON string.
    /// </summary>
    /// <param name="json">JSON string containing the report definition.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <returns>Result containing the parsed report definition or an error.</returns>
    public static LoadResult LoadFromJson(string json, ILogger logger)
    {
        try
        {
            var report = JsonSerializer.Deserialize<ReportDefinition>(json, JsonOptions);
            if (report is null)
            {
                return new LoadError(SqlError.Create("Failed to deserialize report: result was null"));
            }

            logger.LogInformation(
                "Loaded report {ReportId} with {DsCount} data sources",
                report.Id,
                report.DataSources.Length
            );

            return new LoadOk(report);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Failed to parse report JSON");
            return new LoadError(SqlError.FromException(ex));
        }
    }

    /// <summary>
    /// Loads all report definitions from a directory.
    /// </summary>
    /// <param name="directoryPath">Path to the directory containing report JSON files.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <returns>Result containing all loaded reports or an error.</returns>
    public static Result<ImmutableArray<ReportDefinition>, SqlError> LoadFromDirectory(
        string directoryPath,
        ILogger logger
    )
    {
        logger.LogInformation("Loading reports from directory {DirectoryPath}", directoryPath);

        try
        {
            if (!Directory.Exists(directoryPath))
            {
                return new Result<ImmutableArray<ReportDefinition>, SqlError>
                    .Error<ImmutableArray<ReportDefinition>, SqlError>(
                        SqlError.Create($"Report directory not found: {directoryPath}")
                    );
            }

            var reports = ImmutableArray.CreateBuilder<ReportDefinition>();

            foreach (var file in Directory.GetFiles(directoryPath, "*.report.json"))
            {
                var result = LoadFromFile(filePath: file, logger: logger);
                if (result is LoadOk ok)
                {
                    reports.Add(ok.Value);
                }
                else if (result is LoadError err)
                {
                    logger.LogWarning(
                        "Skipping invalid report file {FilePath}: {Error}",
                        file,
                        err.Value.Message
                    );
                }
            }

            logger.LogInformation("Loaded {Count} reports from directory", reports.Count);

            return new Result<ImmutableArray<ReportDefinition>, SqlError>
                .Ok<ImmutableArray<ReportDefinition>, SqlError>(reports.ToImmutable());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load reports from directory");
            return new Result<ImmutableArray<ReportDefinition>, SqlError>
                .Error<ImmutableArray<ReportDefinition>, SqlError>(SqlError.FromException(ex));
        }
    }
}
