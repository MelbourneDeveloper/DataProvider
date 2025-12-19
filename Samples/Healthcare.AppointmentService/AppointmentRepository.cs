using System.Data;
using System.Globalization;

namespace Healthcare.AppointmentService;

/// <summary>
/// FHIR Appointment resource data access using LQL transpiled to PostgreSQL.
/// </summary>
public static class AppointmentRepository
{
    /// <summary>
    /// Gets upcoming appointments using LQL.
    /// LQL: appointment |> filter status = 'booked' and start > @now |> orderBy start |> limit 50
    /// </summary>
    public static Result<IReadOnlyList<Appointment>, SqlError> GetUpcoming(
        NpgsqlConnection connection
    )
    {
        const string lql =
            "appointment |> filter status = 'booked' |> orderBy start |> limit 50";

        var sqlResult = TranspileLql(lql);
        if (sqlResult is Result<string, SqlError>.Error<string, SqlError> err)
        {
            return new Result<IReadOnlyList<Appointment>, SqlError>.Error<
                IReadOnlyList<Appointment>,
                SqlError
            >(err.Value);
        }

        var sql = ((Result<string, SqlError>.Ok<string, SqlError>)sqlResult).Value;
        return connection.Query(sql, mapper: MapAppointment);
    }

    /// <summary>
    /// Gets appointments for a patient using LQL.
    /// LQL: appointment |> filter patientReference = @ref |> orderBy start desc
    /// </summary>
    public static Result<IReadOnlyList<Appointment>, SqlError> GetByPatient(
        NpgsqlConnection connection,
        string patientId
    )
    {
        const string lql =
            "appointment |> filter patient_reference = @ref |> orderBy start desc";

        var sqlResult = TranspileLql(lql);
        if (sqlResult is Result<string, SqlError>.Error<string, SqlError> err)
        {
            return new Result<IReadOnlyList<Appointment>, SqlError>.Error<
                IReadOnlyList<Appointment>,
                SqlError
            >(err.Value);
        }

        var sql = ((Result<string, SqlError>.Ok<string, SqlError>)sqlResult).Value;
        return connection.Query(
            sql,
            [CreateParameter(connection, "@ref", $"Patient/{patientId}")],
            MapAppointment
        );
    }

    /// <summary>
    /// Gets appointments for a practitioner using LQL.
    /// LQL: appointment |> filter practitionerReference = @ref and status = 'booked' |> orderBy start
    /// </summary>
    public static Result<IReadOnlyList<Appointment>, SqlError> GetByPractitioner(
        NpgsqlConnection connection,
        string practitionerId
    )
    {
        const string lql =
            "appointment |> filter practitioner_reference = @ref and status = 'booked' |> orderBy start";

        var sqlResult = TranspileLql(lql);
        if (sqlResult is Result<string, SqlError>.Error<string, SqlError> err)
        {
            return new Result<IReadOnlyList<Appointment>, SqlError>.Error<
                IReadOnlyList<Appointment>,
                SqlError
            >(err.Value);
        }

        var sql = ((Result<string, SqlError>.Ok<string, SqlError>)sqlResult).Value;
        return connection.Query(
            sql,
            [CreateParameter(connection, "@ref", $"Practitioner/{practitionerId}")],
            MapAppointment
        );
    }

    /// <summary>
    /// Gets an appointment by ID.
    /// </summary>
    public static Result<Appointment?, SqlError> GetById(NpgsqlConnection connection, string id)
    {
        var result = connection.Query(
            "SELECT * FROM appointment WHERE id = @id",
            [CreateParameter(connection, "@id", id)],
            MapAppointment
        );

        return result switch
        {
            Result<IReadOnlyList<Appointment>, SqlError>.Ok<IReadOnlyList<Appointment>, SqlError>
                ok => new Result<Appointment?, SqlError>.Ok<Appointment?, SqlError>(
                    ok.Value.FirstOrDefault()
                ),
            Result<IReadOnlyList<Appointment>, SqlError>.Error<
                IReadOnlyList<Appointment>,
                SqlError
            > err => new Result<Appointment?, SqlError>.Error<Appointment?, SqlError>(err.Value),
            _ => new Result<Appointment?, SqlError>.Error<Appointment?, SqlError>(
                SqlError.Create("Unknown result type")
            ),
        };
    }

    /// <summary>
    /// Creates a new FHIR Appointment.
    /// </summary>
    public static Result<Appointment, SqlError> Create(
        NpgsqlConnection connection,
        CreateAppointmentRequest request
    )
    {
        var id = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);

        // Calculate duration from start/end
        var start = DateTime.Parse(request.Start, CultureInfo.InvariantCulture);
        var end = DateTime.Parse(request.End, CultureInfo.InvariantCulture);
        var durationMinutes = (int)(end - start).TotalMinutes;

        var sql = """
            INSERT INTO appointment (id, status, service_category, service_type, reason_code, priority, description, start_time, end_time, minutes_duration, patient_reference, practitioner_reference, created, comment)
            VALUES (@id, 'booked', @serviceCategory, @serviceType, @reasonCode, @priority, @description, @start, @end, @duration, @patientRef, @practitionerRef, @created, @comment)
            """;

        var parameters = new IDataParameter[]
        {
            CreateParameter(connection, "@id", id),
            CreateParameter(connection, "@serviceCategory", request.ServiceCategory),
            CreateParameter(connection, "@serviceType", request.ServiceType),
            CreateParameter(connection, "@reasonCode", request.ReasonCode ?? (object)DBNull.Value),
            CreateParameter(connection, "@priority", request.Priority),
            CreateParameter(connection, "@description", request.Description ?? (object)DBNull.Value),
            CreateParameter(connection, "@start", request.Start),
            CreateParameter(connection, "@end", request.End),
            CreateParameter(connection, "@duration", durationMinutes),
            CreateParameter(connection, "@patientRef", request.PatientReference),
            CreateParameter(connection, "@practitionerRef", request.PractitionerReference),
            CreateParameter(connection, "@created", now),
            CreateParameter(connection, "@comment", request.Comment ?? (object)DBNull.Value),
        };

        var result = connection.Execute(sql, parameters);

        return result switch
        {
            Result<int, SqlError>.Ok<int, SqlError> =>
                new Result<Appointment, SqlError>.Ok<Appointment, SqlError>(
                    new Appointment(
                        id,
                        "booked",
                        request.ServiceCategory,
                        request.ServiceType,
                        request.ReasonCode,
                        request.Priority,
                        request.Description,
                        request.Start,
                        request.End,
                        durationMinutes,
                        request.PatientReference,
                        request.PractitionerReference,
                        now,
                        request.Comment
                    )
                ),
            Result<int, SqlError>.Error<int, SqlError> err =>
                new Result<Appointment, SqlError>.Error<Appointment, SqlError>(err.Value),
            _ => new Result<Appointment, SqlError>.Error<Appointment, SqlError>(
                SqlError.Create("Unknown result type")
            ),
        };
    }

    /// <summary>
    /// Updates appointment status (FHIR workflow).
    /// </summary>
    public static Result<int, SqlError> UpdateStatus(
        NpgsqlConnection connection,
        string id,
        string status
    ) =>
        connection.Execute(
            "UPDATE appointment SET status = @status WHERE id = @id",
            [
                CreateParameter(connection, "@id", id),
                CreateParameter(connection, "@status", status),
            ]
        );

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

    private static Appointment MapAppointment(IDataReader reader) =>
        new(
            Id: reader.GetString(reader.GetOrdinal("id")),
            Status: reader.GetString(reader.GetOrdinal("status")),
            ServiceCategory: reader.IsDBNull(reader.GetOrdinal("service_category"))
                ? null
                : reader.GetString(reader.GetOrdinal("service_category")),
            ServiceType: reader.IsDBNull(reader.GetOrdinal("service_type"))
                ? null
                : reader.GetString(reader.GetOrdinal("service_type")),
            ReasonCode: reader.IsDBNull(reader.GetOrdinal("reason_code"))
                ? null
                : reader.GetString(reader.GetOrdinal("reason_code")),
            Priority: reader.GetString(reader.GetOrdinal("priority")),
            Description: reader.IsDBNull(reader.GetOrdinal("description"))
                ? null
                : reader.GetString(reader.GetOrdinal("description")),
            Start: reader.GetString(reader.GetOrdinal("start_time")),
            End: reader.GetString(reader.GetOrdinal("end_time")),
            MinutesDuration: reader.GetInt32(reader.GetOrdinal("minutes_duration")),
            PatientReference: reader.GetString(reader.GetOrdinal("patient_reference")),
            PractitionerReference: reader.GetString(reader.GetOrdinal("practitioner_reference")),
            Created: reader.GetString(reader.GetOrdinal("created")),
            Comment: reader.IsDBNull(reader.GetOrdinal("comment"))
                ? null
                : reader.GetString(reader.GetOrdinal("comment"))
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
