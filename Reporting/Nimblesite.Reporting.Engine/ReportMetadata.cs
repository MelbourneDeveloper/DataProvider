using System.Collections.Immutable;

namespace Nimblesite.Reporting.Engine;

/// <summary>
/// Client-safe report metadata. No connection strings or secrets.
/// </summary>
public sealed record ReportMetadata(
    string Id,
    string Title,
    ImmutableArray<ReportParameter> Parameters,
    ImmutableArray<string> DataSourceIds,
    LayoutDefinition Layout,
    string? CustomCss = null
);

/// <summary>
/// Converts full report definitions to client-safe metadata.
/// </summary>
public static class ReportMetadataMapper
{
    /// <summary>
    /// Strips sensitive information from a report definition.
    /// </summary>
    /// <param name="report">Full report definition with connection details.</param>
    /// <returns>Client-safe report metadata.</returns>
    public static ReportMetadata ToMetadata(ReportDefinition report) =>
        new(
            Id: report.Id,
            Title: report.Title,
            Parameters: report.Parameters,
            DataSourceIds: [.. report.DataSources.Select(ds => ds.Id)],
            Layout: report.Layout,
            CustomCss: report.CustomCss
        );
}
