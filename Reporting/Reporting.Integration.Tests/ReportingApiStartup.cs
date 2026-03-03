using System.Collections.Immutable;
using System.Data;
using Lql;
using Lql.SQLite;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Routing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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

namespace Reporting.Integration.Tests;

/// <summary>
/// Real Reporting.Api startup used by the E2E fixture.
/// This is the actual API, not a mock. Same logic as Reporting.Api/Program.cs.
/// </summary>
public sealed class ReportingApiStartup
{
    private readonly IConfiguration _configuration;

    public ReportingApiStartup(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public void ConfigureServices(IServiceCollection services)
    {
        services.Configure<JsonOptions>(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy =
                System.Text.Json.JsonNamingPolicy.CamelCase;
        });

        services.AddCors(options =>
        {
            options.AddPolicy(
                "ReportViewer",
                policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()
            );
        });
    }

    public void Configure(IApplicationBuilder app)
    {
        var logger = app.ApplicationServices.GetRequiredService<ILogger<ReportingApiStartup>>();

        app.UseCors("ReportViewer");
        app.UseRouting();

        // Load report definitions
        var reportsDir =
            _configuration["ReportsDirectory"]
            ?? Path.Combine(AppContext.BaseDirectory, "Reports");

        var loadResult = ReportConfigLoader.LoadFromDirectory(
            directoryPath: reportsDir,
            logger: logger
        );

        var reports = loadResult switch
        {
            Result<ImmutableArray<ReportDefinition>, SqlError>.Ok<
                ImmutableArray<ReportDefinition>,
                SqlError
            > ok => ok.Value.ToImmutableDictionary(r => r.Id),
            _ => ImmutableDictionary<string, ReportDefinition>.Empty,
        };

        logger.LogInformation("E2E API loaded {Count} reports", reports.Count);

        // Connection strings from config
        var connectionStrings = _configuration
            .GetSection("ConnectionStrings")
            .GetChildren()
            .ToImmutableDictionary(c => c.Key, c => c.Value ?? "");

        ConnResult CreateConnection(string connectionRef)
        {
            if (!connectionStrings.TryGetValue(connectionRef, out var connStr))
                return new ConnError(SqlError.Create($"Connection '{connectionRef}' not found"));

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

        static TranspileResult TranspileLql(string lqlCode)
        {
            var statementResult = LqlStatementConverter.ToStatement(lqlCode);
            if (
                statementResult
                is Result<LqlStatement, SqlError>.Error<LqlStatement, SqlError> stmtErr
            )
                return new TranspileError(stmtErr.Value);

            var statement =
                ((Result<LqlStatement, SqlError>.Ok<LqlStatement, SqlError>)statementResult).Value;
            return statement.ToSQLite();
        }

        app.UseEndpoints(endpoints =>
        {
            var reportGroup = endpoints.MapGroup("/api/reports");

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
                        return Results.NotFound(new { Error = $"Report '{id}' not found" });

                    var result = ReportEngine.Execute(
                        report: report,
                        parameters: request.Parameters,
                        connectionFactory: CreateConnection,
                        lqlTranspiler: TranspileLql,
                        logger: logger
                    );

                    return result switch
                    {
                        Result<ReportExecutionResult, SqlError>.Ok<
                            ReportExecutionResult,
                            SqlError
                        > ok => Results.Ok(ok.Value),
                        Result<ReportExecutionResult, SqlError>.Error<
                            ReportExecutionResult,
                            SqlError
                        > err => Results.Problem(err.Value.Message),
                        _ => Results.Problem("Unexpected result type"),
                    };
                }
            );
        });
    }
}
