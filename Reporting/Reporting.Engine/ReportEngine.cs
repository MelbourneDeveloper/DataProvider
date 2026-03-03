using System.Collections.Immutable;
using System.Data;
using Microsoft.Extensions.Logging;
using Outcome;
using Selecta;

namespace Reporting.Engine;

using EngineResult = Result<ReportExecutionResult, SqlError>;
using EngineError = Result<ReportExecutionResult, SqlError>.Error<ReportExecutionResult, SqlError>;
using EngineOk = Result<ReportExecutionResult, SqlError>.Ok<ReportExecutionResult, SqlError>;
using DsResult = Result<DataSourceResult, SqlError>;
using DsError = Result<DataSourceResult, SqlError>.Error<DataSourceResult, SqlError>;
using DsOk = Result<DataSourceResult, SqlError>.Ok<DataSourceResult, SqlError>;

/// <summary>
/// Executes report data sources and assembles results.
/// </summary>
public static class ReportEngine
{
    /// <summary>
    /// Executes all data sources in a report definition and returns assembled results.
    /// </summary>
    /// <param name="report">The report definition to execute.</param>
    /// <param name="parameters">Parameter values provided by the user.</param>
    /// <param name="connectionFactory">Factory that creates open IDbConnection from a connection ref name.</param>
    /// <param name="lqlTranspiler">Function that transpiles LQL to SQL for the target database.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <returns>Result containing all data source results or an error.</returns>
    public static EngineResult Execute(
        ReportDefinition report,
        ImmutableDictionary<string, string> parameters,
        Func<string, Result<IDbConnection, SqlError>> connectionFactory,
        Func<string, Result<string, SqlError>> lqlTranspiler,
        ILogger logger
    )
    {
        logger.LogInformation(
            "Executing report {ReportId} with {ParamCount} parameters",
            report.Id,
            parameters.Count
        );

        var results = ImmutableDictionary.CreateBuilder<string, DataSourceResult>();

        foreach (var ds in report.DataSources)
        {
            logger.LogInformation(
                "Executing data source {DataSourceId} (type: {Type})",
                ds.Id,
                ds.Type
            );

            var dsResult = ExecuteDataSource(
                dataSource: ds,
                parameters: parameters,
                connectionFactory: connectionFactory,
                lqlTranspiler: lqlTranspiler,
                logger: logger
            );

            if (dsResult is DsError dsErr)
            {
                logger.LogError(
                    "Data source {DataSourceId} failed: {Error}",
                    ds.Id,
                    dsErr.Value.Message
                );
                return new EngineError(dsErr.Value);
            }

            var ok = (DsOk)dsResult;
            results.Add(ds.Id, ok.Value);

            logger.LogInformation(
                "Data source {DataSourceId} returned {RowCount} rows",
                ds.Id,
                ok.Value.TotalRows
            );
        }

        return new EngineOk(
            new ReportExecutionResult(
                ReportId: report.Id,
                ExecutedAt: DateTimeOffset.UtcNow,
                DataSources: results.ToImmutable()
            )
        );
    }

    /// <summary>
    /// Executes a single data source and returns its result.
    /// </summary>
    internal static DsResult ExecuteDataSource(
        DataSourceDefinition dataSource,
        ImmutableDictionary<string, string> parameters,
        Func<string, Result<IDbConnection, SqlError>> connectionFactory,
        Func<string, Result<string, SqlError>> lqlTranspiler,
        ILogger logger
    )
    {
        return dataSource.Type switch
        {
            DataSourceType.Sql => ExecuteSql(
                dataSource: dataSource,
                parameters: parameters,
                connectionFactory: connectionFactory,
                logger: logger
            ),
            DataSourceType.Lql => ExecuteLql(
                dataSource: dataSource,
                parameters: parameters,
                connectionFactory: connectionFactory,
                lqlTranspiler: lqlTranspiler,
                logger: logger
            ),
            DataSourceType.Api => new DsError(
                SqlError.Create("API data sources are not yet supported")
            ),
            _ => new DsError(
                SqlError.Create($"Unknown data source type: {dataSource.Type}")
            ),
        };
    }

    private static DsResult ExecuteSql(
        DataSourceDefinition dataSource,
        ImmutableDictionary<string, string> parameters,
        Func<string, Result<IDbConnection, SqlError>> connectionFactory,
        ILogger logger
    )
    {
        if (string.IsNullOrWhiteSpace(dataSource.Query))
        {
            return new DsError(SqlError.Create("SQL data source has no query"));
        }

        if (string.IsNullOrWhiteSpace(dataSource.ConnectionRef))
        {
            return new DsError(SqlError.Create("SQL data source has no connection reference"));
        }

        var connResult = connectionFactory(dataSource.ConnectionRef);
        if (connResult is Result<IDbConnection, SqlError>.Error<IDbConnection, SqlError> connErr)
        {
            return new DsError(connErr.Value);
        }

        var connection = ((Result<IDbConnection, SqlError>.Ok<IDbConnection, SqlError>)connResult).Value;

        return ExecuteQueryOnConnection(
            connection: connection,
            sql: dataSource.Query,
            parameterNames: dataSource.Parameters,
            parameterValues: parameters,
            logger: logger
        );
    }

    private static DsResult ExecuteLql(
        DataSourceDefinition dataSource,
        ImmutableDictionary<string, string> parameters,
        Func<string, Result<IDbConnection, SqlError>> connectionFactory,
        Func<string, Result<string, SqlError>> lqlTranspiler,
        ILogger logger
    )
    {
        if (string.IsNullOrWhiteSpace(dataSource.Query))
        {
            return new DsError(SqlError.Create("LQL data source has no query"));
        }

        if (string.IsNullOrWhiteSpace(dataSource.ConnectionRef))
        {
            return new DsError(SqlError.Create("LQL data source has no connection reference"));
        }

        var transpileResult = lqlTranspiler(dataSource.Query);
        if (transpileResult is Result<string, SqlError>.Error<string, SqlError> transpileErr)
        {
            return new DsError(transpileErr.Value);
        }

        var sql = ((Result<string, SqlError>.Ok<string, SqlError>)transpileResult).Value;
        logger.LogInformation("LQL transpiled to SQL: {Sql}", sql);

        var connResult = connectionFactory(dataSource.ConnectionRef);
        if (connResult is Result<IDbConnection, SqlError>.Error<IDbConnection, SqlError> connErr)
        {
            return new DsError(connErr.Value);
        }

        var connection = ((Result<IDbConnection, SqlError>.Ok<IDbConnection, SqlError>)connResult).Value;

        return ExecuteQueryOnConnection(
            connection: connection,
            sql: sql,
            parameterNames: dataSource.Parameters,
            parameterValues: parameters,
            logger: logger
        );
    }

    private static DsResult ExecuteQueryOnConnection(
        IDbConnection connection,
        string sql,
        ImmutableArray<string> parameterNames,
        ImmutableDictionary<string, string> parameterValues,
        ILogger logger
    )
    {
        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;

            foreach (var paramName in parameterNames)
            {
                if (parameterValues.TryGetValue(paramName, out var value))
                {
                    var param = command.CreateParameter();
                    param.ParameterName = $"@{paramName}";
                    param.Value = value;
                    command.Parameters.Add(param);
                }
            }

            using var reader = command.ExecuteReader();

            var columnNames = ImmutableArray.CreateBuilder<string>();
            for (var i = 0; i < reader.FieldCount; i++)
            {
                columnNames.Add(reader.GetName(i));
            }

            var rows = ImmutableArray.CreateBuilder<ImmutableArray<object?>>();
            while (reader.Read())
            {
                var row = ImmutableArray.CreateBuilder<object?>();
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    row.Add(reader.IsDBNull(i) ? null : reader.GetValue(i));
                }
                rows.Add(row.ToImmutable());
            }

            return new DsOk(
                new DataSourceResult(
                    ColumnNames: columnNames.ToImmutable(),
                    Rows: rows.ToImmutable(),
                    TotalRows: rows.Count
                )
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to execute query");
            return new DsError(SqlError.FromException(ex));
        }
    }
}
