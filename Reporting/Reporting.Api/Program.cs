using System.Collections.Immutable;
using System.Data;
using Lql;
using Lql.SQLite;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Data.Sqlite;
using Outcome;
using Reporting.Engine;
using Selecta;

using ConnResult = Outcome.Result<System.Data.IDbConnection, Selecta.SqlError>;
using ConnError = Outcome.Result<System.Data.IDbConnection, Selecta.SqlError>.Error<
    System.Data.IDbConnection,
    Selecta.SqlError
>;
using ConnOk = Outcome.Result<System.Data.IDbConnection, Selecta.SqlError>.Ok<
    System.Data.IDbConnection,
    Selecta.SqlError
>;
using TranspileResult = Outcome.Result<string, Selecta.SqlError>;
using TranspileError = Outcome.Result<string, Selecta.SqlError>.Error<string, Selecta.SqlError>;
using TranspileOk = Outcome.Result<string, Selecta.SqlError>.Ok<string, Selecta.SqlError>;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.PropertyNamingPolicy =
        System.Text.Json.JsonNamingPolicy.CamelCase;
});

builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "ReportViewer",
        policy =>
        {
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        }
    );
});

var app = builder.Build();

app.UseCors("ReportViewer");

// Serve static files for the React renderer
app.UseDefaultFiles();
app.UseStaticFiles();

// Load report definitions from the Reports directory
var reportsDir =
    app.Configuration["ReportsDirectory"]
    ?? Path.Combine(AppContext.BaseDirectory, "Reports");

var loadResult = ReportConfigLoader.LoadFromDirectory(
    directoryPath: reportsDir,
    logger: app.Logger
);

var reports = loadResult switch
{
    Result<ImmutableArray<ReportDefinition>, SqlError>.Ok<
        ImmutableArray<ReportDefinition>,
        SqlError
    > ok => ok.Value.ToImmutableDictionary(r => r.Id),
    _ => ImmutableDictionary<string, ReportDefinition>.Empty,
};

app.Logger.LogInformation("Loaded {Count} report definitions", reports.Count);

// Connection registry from config
var connectionStrings = app.Configuration
    .GetSection("ConnectionStrings")
    .GetChildren()
    .ToImmutableDictionary(c => c.Key, c => c.Value ?? "");

ConnResult CreateConnection(string connectionRef)
{
    if (!connectionStrings.TryGetValue(connectionRef, out var connStr))
    {
        return new ConnError(SqlError.Create($"Connection '{connectionRef}' not found"));
    }

    try
    {
        var connection = (IDbConnection)new SqliteConnection(connStr);
        connection.Open();
        return new ConnOk(connection);
    }
    catch (Exception ex)
    {
        return new ConnError(SqlError.FromException(ex));
    }
}

TranspileResult TranspileLql(string lqlCode)
{
    var statementResult = LqlStatementConverter.ToStatement(lqlCode);
    if (
        statementResult
        is Result<LqlStatement, SqlError>.Error<LqlStatement, SqlError> stmtErr
    )
    {
        return new TranspileError(stmtErr.Value);
    }

    var statement =
        (
            (Result<LqlStatement, SqlError>.Ok<LqlStatement, SqlError>)statementResult
        ).Value;
    return statement.ToSQLite();
}

// --- API Endpoints ---

var reportGroup = app.MapGroup("/api/reports").WithTags("Reports");

reportGroup.MapGet(
    "/",
    () =>
        Results.Ok(
            reports.Values
                .Select(ReportMetadataMapper.ToMetadata)
                .ToImmutableArray()
        )
);

reportGroup.MapGet(
    "/{id}",
    (string id) =>
        reports.TryGetValue(id, out var report)
            ? Results.Ok(ReportMetadataMapper.ToMetadata(report))
            : Results.NotFound(new { Error = $"Report '{id}' not found" })
);

reportGroup.MapPost(
    "/{id}/execute",
    (string id, ReportExecuteRequest request) =>
    {
        if (!reports.TryGetValue(id, out var report))
        {
            return Results.NotFound(new { Error = $"Report '{id}' not found" });
        }

        var result = ReportEngine.Execute(
            report: report,
            parameters: request.Parameters,
            connectionFactory: CreateConnection,
            lqlTranspiler: TranspileLql,
            logger: app.Logger
        );

        return result switch
        {
            Result<ReportExecutionResult, SqlError>.Ok<ReportExecutionResult, SqlError> ok
                => Results.Ok(ok.Value),
            Result<ReportExecutionResult, SqlError>.Error<ReportExecutionResult, SqlError> err
                => Results.Problem(err.Value.Message),
            _ => Results.Problem("Unexpected result type"),
        };
    }
);

reportGroup.MapGet(
    "/{id}/export",
    (string id, string? datasource, string? format) =>
    {
        if (!reports.TryGetValue(id, out var report))
        {
            return Results.NotFound(new { Error = $"Report '{id}' not found" });
        }

        var parameters = ImmutableDictionary<string, string>.Empty;

        var result = ReportEngine.Execute(
            report: report,
            parameters: parameters,
            connectionFactory: CreateConnection,
            lqlTranspiler: TranspileLql,
            logger: app.Logger
        );

        if (
            result
            is not Result<ReportExecutionResult, SqlError>.Ok<
                ReportExecutionResult,
                SqlError
            > ok
        )
        {
            var err =
                (
                    Result<ReportExecutionResult, SqlError>.Error<
                        ReportExecutionResult,
                        SqlError
                    >
                )result;
            return Results.Problem(err.Value.Message);
        }

        var targetDs = datasource ?? report.DataSources.FirstOrDefault()?.Id;
        if (
            targetDs is null
            || !ok.Value.DataSources.TryGetValue(targetDs, out var dsResult)
        )
        {
            return Results.NotFound(
                new { Error = $"Data source '{targetDs}' not found" }
            );
        }

        if (format == "csv")
        {
            var csv = FormatAdapter.ToCsv(dsResult);
            return Results.Text(csv, contentType: "text/csv");
        }

        return Results.Ok(dsResult);
    }
);

app.Run();

/// <summary>
/// Partial class to allow test access via WebApplicationFactory.
/// </summary>
public partial class Program { }
