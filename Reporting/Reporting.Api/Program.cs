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
using LqlParseOk = Outcome.Result<Lql.LqlStatement, Selecta.SqlError>.Ok<
    Lql.LqlStatement,
    Selecta.SqlError
>;
using LqlParseError = Outcome.Result<Lql.LqlStatement, Selecta.SqlError>.Error<
    Lql.LqlStatement,
    Selecta.SqlError
>;
using EngineOk = Outcome.Result<Reporting.Engine.ReportExecutionResult, Selecta.SqlError>.Ok<
    Reporting.Engine.ReportExecutionResult,
    Selecta.SqlError
>;
using EngineError = Outcome.Result<Reporting.Engine.ReportExecutionResult, Selecta.SqlError>.Error<
    Reporting.Engine.ReportExecutionResult,
    Selecta.SqlError
>;
using LoadDirOk = Outcome.Result<
    System.Collections.Immutable.ImmutableArray<Reporting.Engine.ReportDefinition>,
    Selecta.SqlError
>.Ok<
    System.Collections.Immutable.ImmutableArray<Reporting.Engine.ReportDefinition>,
    Selecta.SqlError
>;
using LoadDirError = Outcome.Result<
    System.Collections.Immutable.ImmutableArray<Reporting.Engine.ReportDefinition>,
    Selecta.SqlError
>.Error<
    System.Collections.Immutable.ImmutableArray<Reporting.Engine.ReportDefinition>,
    Selecta.SqlError
>;

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
    LoadDirOk ok => ok.Value.ToImmutableDictionary(r => r.Id),
    LoadDirError => ImmutableDictionary<string, ReportDefinition>.Empty,
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
        IDbConnection connection = new SqliteConnection(connStr);
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
    return LqlStatementConverter.ToStatement(lqlCode) switch
    {
        LqlParseError stmtErr => new TranspileError(stmtErr.Value),
        LqlParseOk stmtOk => stmtOk.Value.ToSQLite(),
    };
}

static IResult FormatExportResult(
    ReportExecutionResult executionResult,
    ReportDefinition report,
    string? datasource,
    string? format
)
{
    var targetDs = datasource ?? report.DataSources.FirstOrDefault()?.Id;
    if (
        targetDs is null
        || !executionResult.DataSources.TryGetValue(targetDs, out var dsResult)
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

        return ReportEngine.Execute(
            report: report,
            parameters: request.Parameters,
            connectionFactory: CreateConnection,
            lqlTranspiler: TranspileLql,
            logger: app.Logger
        ) switch
        {
            EngineOk ok => Results.Ok(ok.Value),
            EngineError err => Results.Problem(err.Value.Message),
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

        return ReportEngine.Execute(
            report: report,
            parameters: ImmutableDictionary<string, string>.Empty,
            connectionFactory: CreateConnection,
            lqlTranspiler: TranspileLql,
            logger: app.Logger
        ) switch
        {
            EngineError err => Results.Problem(err.Value.Message),
            EngineOk ok => FormatExportResult(
                executionResult: ok.Value,
                report: report,
                datasource: datasource,
                format: format
            ),
        };
    }
);

app.Run();

/// <summary>
/// Partial class to allow test access via WebApplicationFactory.
/// </summary>
public partial class Program { }
