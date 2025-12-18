using System.Data;
using Outcome;
using Selecta;

namespace DataProvider;

/// <summary>
/// Static extension methods for IDbConnection following FP patterns
/// </summary>
public static class DbConnectionExtensions
{
    /// <summary>
    /// Execute a query and return results
    /// </summary>
    /// <typeparam name="T">The result type</typeparam>
    /// <param name="connection">The database connection</param>
    /// <param name="sql">The SQL query</param>
    /// <param name="parameters">Optional parameters</param>
    /// <param name="mapper">Function to map from IDataReader to T</param>
    /// <returns>Result with list of T or error</returns>
    public static Result<IReadOnlyList<T>, SqlError> Query<T>(
        this IDbConnection connection,
        string sql,
        IEnumerable<IDataParameter>? parameters = null,
        Func<IDataReader, T>? mapper = null
    )
    {
        if (connection == null)
            return new Result<IReadOnlyList<T>, SqlError>.Error<IReadOnlyList<T>, SqlError>(
                SqlError.Create("Connection is null")
            );

        if (string.IsNullOrWhiteSpace(sql))
            return new Result<IReadOnlyList<T>, SqlError>.Error<IReadOnlyList<T>, SqlError>(
                SqlError.Create("SQL is null or empty")
            );

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;

            if (parameters != null)
            {
                foreach (var parameter in parameters)
                {
                    command.Parameters.Add(parameter);
                }
            }

            var results = new List<T>();
            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                if (mapper != null)
                {
                    results.Add(mapper(reader));
                }
            }

            return new Result<IReadOnlyList<T>, SqlError>.Ok<IReadOnlyList<T>, SqlError>(results.AsReadOnly());
        }
        catch (Exception ex)
        {
            return new Result<IReadOnlyList<T>, SqlError>.Error<IReadOnlyList<T>, SqlError>(SqlError.FromException(ex));
        }
    }

    /// <summary>
    /// Execute a non-query command
    /// </summary>
    /// <param name="connection">The database connection</param>
    /// <param name="sql">The SQL command</param>
    /// <param name="parameters">Optional parameters</param>
    /// <returns>Result with rows affected or error</returns>
    public static Result<int, SqlError> Execute(
        this IDbConnection connection,
        string sql,
        IEnumerable<IDataParameter>? parameters = null
    )
    {
        if (connection == null)
            return new Result<int, SqlError>.Error<int, SqlError>(SqlError.Create("Connection is null"));

        if (string.IsNullOrWhiteSpace(sql))
            return new Result<int, SqlError>.Error<int, SqlError>(SqlError.Create("SQL is null or empty"));

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;

            if (parameters != null)
            {
                foreach (var parameter in parameters)
                {
                    command.Parameters.Add(parameter);
                }
            }

            var rowsAffected = command.ExecuteNonQuery();
            return new Result<int, SqlError>.Ok<int, SqlError>(rowsAffected);
        }
        catch (Exception ex)
        {
            return new Result<int, SqlError>.Error<int, SqlError>(SqlError.FromException(ex));
        }
    }

    /// <summary>
    /// Execute a scalar command
    /// </summary>
    /// <typeparam name="T">The result type</typeparam>
    /// <param name="connection">The database connection</param>
    /// <param name="sql">The SQL command</param>
    /// <param name="parameters">Optional parameters</param>
    /// <returns>Result with scalar value or error</returns>
    public static Result<T?, SqlError> Scalar<T>(
        this IDbConnection connection,
        string sql,
        IEnumerable<IDataParameter>? parameters = null
    )
    {
        if (connection == null)
            return new Result<T?, SqlError>.Error<T?, SqlError>(SqlError.Create("Connection is null"));

        if (string.IsNullOrWhiteSpace(sql))
            return new Result<T?, SqlError>.Error<T?, SqlError>(SqlError.Create("SQL is null or empty"));

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;

            if (parameters != null)
            {
                foreach (var parameter in parameters)
                {
                    command.Parameters.Add(parameter);
                }
            }

            var result = command.ExecuteScalar();
            return new Result<T?, SqlError>.Ok<T?, SqlError>(result is T value ? value : default);
        }
        catch (Exception ex)
        {
            return new Result<T?, SqlError>.Error<T?, SqlError>(SqlError.FromException(ex));
        }
    }

    /// <summary>
    /// Execute a SelectStatement by generating platform-specific SQL and mapping results.
    /// </summary>
    /// <typeparam name="T">Result row type</typeparam>
    /// <param name="connection">The database connection</param>
    /// <param name="statement">The abstract SQL statement</param>
    /// <param name="sqlGenerator">Function that converts the statement to platform-specific SQL (returns Result)</param>
    /// <param name="parameters">Optional parameters to pass to the command</param>
    /// <param name="mapper">Mapper from IDataReader to T (required)</param>
    /// <returns>Result with list of T or SqlError on failure</returns>
    public static Result<IReadOnlyList<T>, SqlError> GetRecords<T>(
        this IDbConnection connection,
        SelectStatement statement,
        Func<SelectStatement, Result<string, SqlError>> sqlGenerator,
        Func<IDataReader, T> mapper,
        IEnumerable<IDataParameter>? parameters = null
    )
    {
        if (connection == null)
            return new Result<IReadOnlyList<T>, SqlError>.Error<IReadOnlyList<T>, SqlError>(
                SqlError.Create("Connection is null")
            );
        if (statement == null)
            return new Result<IReadOnlyList<T>, SqlError>.Error<IReadOnlyList<T>, SqlError>(
                SqlError.Create("SelectStatement is null")
            );
        if (sqlGenerator == null)
            return new Result<IReadOnlyList<T>, SqlError>.Error<IReadOnlyList<T>, SqlError>(
                SqlError.Create("sqlGenerator is null")
            );
        if (mapper == null)
            return new Result<IReadOnlyList<T>, SqlError>.Error<IReadOnlyList<T>, SqlError>(
                SqlError.Create("Mapper is required for GetRecords<T>")
            );

        var sqlResult = sqlGenerator(statement);
        if (sqlResult is Result<string, SqlError>.Error<string, SqlError> sqlFail)
        {
            return new Result<IReadOnlyList<T>, SqlError>.Error<IReadOnlyList<T>, SqlError>(sqlFail.Value);
        }

        var sql = ((Result<string, SqlError>.Ok<string, SqlError>)sqlResult).Value;
        return connection.Query(sql, parameters, mapper);
    }
}
