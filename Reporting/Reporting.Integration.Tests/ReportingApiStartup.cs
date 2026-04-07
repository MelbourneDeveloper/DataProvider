using System.Collections.Immutable;
using System.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Routing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nimblesite.Lql.Core;
using Nimblesite.Lql.SQLite;
using Nimblesite.Sql.Model;
using Reporting.Engine;
using ConnError = Outcome.Result<
    System.Data.IDbConnection,
    Nimblesite.Sql.Model.SqlError
>.Error<System.Data.IDbConnection, Nimblesite.Sql.Model.SqlError>;
using ConnOk = Outcome.Result<System.Data.IDbConnection, Nimblesite.Sql.Model.SqlError>.Ok<
    System.Data.IDbConnection,
    Nimblesite.Sql.Model.SqlError
>;
using ConnResult = Outcome.Result<System.Data.IDbConnection, Nimblesite.Sql.Model.SqlError>;
using EngineError = Outcome.Result<
    Reporting.Engine.ReportExecutionResult,
    Nimblesite.Sql.Model.SqlError
>.Error<Reporting.Engine.ReportExecutionResult, Nimblesite.Sql.Model.SqlError>;
using EngineOk = Outcome.Result<
    Reporting.Engine.ReportExecutionResult,
    Nimblesite.Sql.Model.SqlError
>.Ok<Reporting.Engine.ReportExecutionResult, Nimblesite.Sql.Model.SqlError>;
using LoadDirError = Outcome.Result<
    System.Collections.Immutable.ImmutableArray<Reporting.Engine.ReportDefinition>,
    Nimblesite.Sql.Model.SqlError
>.Error<
    System.Collections.Immutable.ImmutableArray<Reporting.Engine.ReportDefinition>,
    Nimblesite.Sql.Model.SqlError
>;
using LoadDirOk = Outcome.Result<
    System.Collections.Immutable.ImmutableArray<Reporting.Engine.ReportDefinition>,
    Nimblesite.Sql.Model.SqlError
>.Ok<
    System.Collections.Immutable.ImmutableArray<Reporting.Engine.ReportDefinition>,
    Nimblesite.Sql.Model.SqlError
>;
using LqlParseError = Outcome.Result<
    Nimblesite.Lql.Core.LqlStatement,
    Nimblesite.Sql.Model.SqlError
>.Error<Nimblesite.Lql.Core.LqlStatement, Nimblesite.Sql.Model.SqlError>;
using LqlParseOk = Outcome.Result<
    Nimblesite.Lql.Core.LqlStatement,
    Nimblesite.Sql.Model.SqlError
>.Ok<Nimblesite.Lql.Core.LqlStatement, Nimblesite.Sql.Model.SqlError>;
using TranspileError = Outcome.Result<string, Nimblesite.Sql.Model.SqlError>.Error<
    string,
    Nimblesite.Sql.Model.SqlError
>;
using TranspileResult = Outcome.Result<string, Nimblesite.Sql.Model.SqlError>;

namespace Reporting.Integration.Tests;

/// <summary>
/// Real Reporting.Api startup used by the E2E fixture.
/// This is the actual API, not a mock. Same logic as Reporting.Api/Program.cs.
/// </summary>
public sealed class ReportingApiStartup
{
    private readonly IConfiguration _configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportingApiStartup"/> class.
    /// </summary>
    /// <param name="configuration">Application configuration.</param>
    public ReportingApiStartup(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// Configures services for the reporting API.
    /// </summary>
    /// <param name="services">Service collection to configure.</param>
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton(_configuration);

        services.Configure<JsonOptions>(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = System
                .Text
                .Json
                .JsonNamingPolicy
                .CamelCase;
        });

        services.AddCors(options =>
        {
            options.AddPolicy(
                "ReportViewer",
                policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()
            );
        });
    }

    /// <summary>
    /// Configures the application middleware and endpoints.
    /// </summary>
    /// <param name="app">Application builder.</param>
    public void Configure(IApplicationBuilder app)
    {
        var logger = app.ApplicationServices.GetRequiredService<ILogger<ReportingApiStartup>>();

        app.UseCors("ReportViewer");
        app.UseRouting();

        // Load report definitions
        var reportsDir =
            _configuration["ReportsDirectory"] ?? Path.Combine(AppContext.BaseDirectory, "Reports");

        var loadResult = ReportConfigLoader.LoadFromDirectory(
            directoryPath: reportsDir,
            logger: logger
        );

        var reports = loadResult switch
        {
            LoadDirOk ok => ok.Value.ToImmutableDictionary(r => r.Id),
            LoadDirError => ImmutableDictionary<string, ReportDefinition>.Empty,
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
                var connection = new SqliteConnection(connStr);
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
            return LqlStatementConverter.ToStatement(lqlCode) switch
            {
                LqlParseError stmtErr => new TranspileError(stmtErr.Value),
                LqlParseOk stmtOk => stmtOk.Value.ToSQLite(),
            };
        }

        app.UseEndpoints(endpoints =>
        {
            var reportGroup = endpoints.MapGroup("/api/reports");

            reportGroup.MapGet(
                "/",
                () =>
                    Results.Ok(
                        reports.Values.Select(ReportMetadataMapper.ToMetadata).ToImmutableArray()
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

                    return ReportEngine.Execute(
                        report: report,
                        parameters: request.Parameters,
                        connectionFactory: CreateConnection,
                        lqlTranspiler: TranspileLql,
                        logger: logger
                    ) switch
                    {
                        EngineOk ok => Results.Ok(ok.Value),
                        EngineError err => Results.Problem(err.Value.Message),
                    };
                }
            );
        });
    }
}
