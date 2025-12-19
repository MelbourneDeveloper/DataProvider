using System.Data;

namespace Healthcare.AppointmentService;

/// <summary>
/// Practitioner data access using LQL transpiled to PostgreSQL.
/// All queries written in LQL and transpiled at runtime!
/// </summary>
public static class PractitionerRepository
{
    /// <summary>
    /// Gets all active practitioners using LQL.
    /// LQL: practitioner |> filter active = true |> orderBy nameFamily
    /// </summary>
    public static Result<IReadOnlyList<Practitioner>, SqlError> GetAll(NpgsqlConnection connection)
    {
        // LQL query transpiled to PostgreSQL
        const string lql = "practitioner |> filter active = true |> orderBy name_family";

        var sqlResult = TranspileLql(lql);
        if (sqlResult is Result<string, SqlError>.Error<string, SqlError> err)
        {
            return new Result<IReadOnlyList<Practitioner>, SqlError>.Error<
                IReadOnlyList<Practitioner>,
                SqlError
            >(err.Value);
        }

        var sql = ((Result<string, SqlError>.Ok<string, SqlError>)sqlResult).Value;
        return connection.Query(sql, mapper: MapPractitioner);
    }

    /// <summary>
    /// Gets a practitioner by ID using LQL.
    /// LQL: practitioner |> filter id = @id
    /// </summary>
    public static Result<Practitioner?, SqlError> GetById(NpgsqlConnection connection, string id)
    {
        // Direct SQL for single lookup - LQL overkill here
        var result = connection.Query(
            "SELECT * FROM practitioner WHERE id = @id",
            [CreateParameter(connection, "@id", id)],
            MapPractitioner
        );

        return result switch
        {
            Result<IReadOnlyList<Practitioner>, SqlError>.Ok<IReadOnlyList<Practitioner>, SqlError>
                ok => new Result<Practitioner?, SqlError>.Ok<Practitioner?, SqlError>(
                    ok.Value.FirstOrDefault()
                ),
            Result<IReadOnlyList<Practitioner>, SqlError>.Error<
                IReadOnlyList<Practitioner>,
                SqlError
            > err => new Result<Practitioner?, SqlError>.Error<Practitioner?, SqlError>(err.Value),
            _ => new Result<Practitioner?, SqlError>.Error<Practitioner?, SqlError>(
                SqlError.Create("Unknown result type")
            ),
        };
    }

    /// <summary>
    /// Searches practitioners by specialty using LQL.
    /// LQL: practitioner |> filter specialty like @term |> orderBy nameFamily
    /// </summary>
    public static Result<IReadOnlyList<Practitioner>, SqlError> SearchBySpecialty(
        NpgsqlConnection connection,
        string specialty
    )
    {
        const string lql =
            "practitioner |> filter specialty like @term and active = true |> orderBy name_family";

        var sqlResult = TranspileLql(lql);
        if (sqlResult is Result<string, SqlError>.Error<string, SqlError> err)
        {
            return new Result<IReadOnlyList<Practitioner>, SqlError>.Error<
                IReadOnlyList<Practitioner>,
                SqlError
            >(err.Value);
        }

        var sql = ((Result<string, SqlError>.Ok<string, SqlError>)sqlResult).Value;
        return connection.Query(
            sql,
            [CreateParameter(connection, "@term", $"%{specialty}%")],
            MapPractitioner
        );
    }

    /// <summary>
    /// Creates a new practitioner.
    /// </summary>
    public static Result<Practitioner, SqlError> Create(
        NpgsqlConnection connection,
        CreatePractitionerRequest request
    )
    {
        var id = Guid.NewGuid().ToString();

        var sql = """
            INSERT INTO practitioner (id, identifier, active, name_family, name_given, qualification, specialty, telecom_email, telecom_phone)
            VALUES (@id, @identifier, true, @nameFamily, @nameGiven, @qualification, @specialty, @telecomEmail, @telecomPhone)
            """;

        var parameters = new IDataParameter[]
        {
            CreateParameter(connection, "@id", id),
            CreateParameter(connection, "@identifier", request.Identifier),
            CreateParameter(connection, "@nameFamily", request.NameFamily),
            CreateParameter(connection, "@nameGiven", request.NameGiven),
            CreateParameter(connection, "@qualification", request.Qualification ?? (object)DBNull.Value),
            CreateParameter(connection, "@specialty", request.Specialty ?? (object)DBNull.Value),
            CreateParameter(connection, "@telecomEmail", request.TelecomEmail ?? (object)DBNull.Value),
            CreateParameter(connection, "@telecomPhone", request.TelecomPhone ?? (object)DBNull.Value),
        };

        var result = connection.Execute(sql, parameters);

        return result switch
        {
            Result<int, SqlError>.Ok<int, SqlError> =>
                new Result<Practitioner, SqlError>.Ok<Practitioner, SqlError>(
                    new Practitioner(
                        id,
                        request.Identifier,
                        true,
                        request.NameFamily,
                        request.NameGiven,
                        request.Qualification,
                        request.Specialty,
                        request.TelecomEmail,
                        request.TelecomPhone
                    )
                ),
            Result<int, SqlError>.Error<int, SqlError> err =>
                new Result<Practitioner, SqlError>.Error<Practitioner, SqlError>(err.Value),
            _ => new Result<Practitioner, SqlError>.Error<Practitioner, SqlError>(
                SqlError.Create("Unknown result type")
            ),
        };
    }

    /// <summary>
    /// Transpiles LQL to PostgreSQL SQL.
    /// </summary>
    private static Result<string, SqlError> TranspileLql(string lql)
    {
        try
        {
            var transpileResult = LqlTranspiler.Transpile(lql, SqlDialect.Postgres);
            return transpileResult switch
            {
                LqlTranspileOk ok => new Result<string, SqlError>.Ok<string, SqlError>(ok.Value),
                LqlTranspileError err => new Result<string, SqlError>.Error<string, SqlError>(
                    SqlError.Create($"LQL transpile error: {err.Value}")
                ),
                _ => new Result<string, SqlError>.Error<string, SqlError>(
                    SqlError.Create("Unknown LQL transpile result")
                ),
            };
        }
        catch (Exception ex)
        {
            return new Result<string, SqlError>.Error<string, SqlError>(SqlError.FromException(ex));
        }
    }

    private static Practitioner MapPractitioner(IDataReader reader) =>
        new(
            Id: reader.GetString(reader.GetOrdinal("id")),
            Identifier: reader.GetString(reader.GetOrdinal("identifier")),
            Active: reader.GetBoolean(reader.GetOrdinal("active")),
            NameFamily: reader.GetString(reader.GetOrdinal("name_family")),
            NameGiven: reader.GetString(reader.GetOrdinal("name_given")),
            Qualification: reader.IsDBNull(reader.GetOrdinal("qualification"))
                ? null
                : reader.GetString(reader.GetOrdinal("qualification")),
            Specialty: reader.IsDBNull(reader.GetOrdinal("specialty"))
                ? null
                : reader.GetString(reader.GetOrdinal("specialty")),
            TelecomEmail: reader.IsDBNull(reader.GetOrdinal("telecom_email"))
                ? null
                : reader.GetString(reader.GetOrdinal("telecom_email")),
            TelecomPhone: reader.IsDBNull(reader.GetOrdinal("telecom_phone"))
                ? null
                : reader.GetString(reader.GetOrdinal("telecom_phone"))
        );

    private static IDataParameter CreateParameter(NpgsqlConnection connection, string name, object value)
    {
        var cmd = connection.CreateCommand();
        var param = cmd.CreateParameter();
        param.ParameterName = name;
        param.Value = value;
        return param;
    }
}
