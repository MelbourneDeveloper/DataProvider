using System.Globalization;

namespace Healthcare.PatientService;

/// <summary>
/// Patient data access using DataProvider SQL extension methods.
/// All queries use raw SQL via DbConnectionExtensions.Query/Execute.
/// </summary>
public static class PatientRepository
{
    private const string PatientProjection =
        """
        SELECT
            p.Id,
            p.Active,
            p.GivenName,
            p.FamilyName,
            p.BirthDate,
            p.Gender,
            p.Phone,
            p.Email,
            p.AddressLine,
            p.City,
            p.State,
            p.PostalCode,
            p.Country,
            p.LastUpdated,
            p.VersionId
        FROM Patient p
        """;

    /// <summary>
    /// Gets patients filtered by optional FHIR search parameters.
    /// </summary>
    public static Result<IReadOnlyList<Patient>, SqlError> GetPatients(
        IDbConnection connection,
        bool? active,
        string? familyName,
        string? givenName,
        string? gender
    )
    {
        const string sql =
            PatientProjection
            + """
            WHERE (@active IS NULL OR p.Active = @active)
              AND (@familyName IS NULL OR p.FamilyName LIKE '%' || @familyName || '%')
              AND (@givenName IS NULL OR p.GivenName LIKE '%' || @givenName || '%')
              AND (@gender IS NULL OR p.Gender = @gender)
            ORDER BY p.FamilyName, p.GivenName
            """;

        var parameters = new IDataParameter[]
        {
            CreateParameter(connection, "@active", active.HasValue ? (active.Value ? 1 : 0) : DBNull.Value),
            CreateParameter(connection, "@familyName", familyName ?? (object)DBNull.Value),
            CreateParameter(connection, "@givenName", givenName ?? (object)DBNull.Value),
            CreateParameter(connection, "@gender", gender ?? (object)DBNull.Value),
        };

        return connection.Query(sql, parameters, MapPatient);
    }

    /// <summary>
    /// Gets a patient by ID.
    /// </summary>
    public static Result<Patient?, SqlError> GetById(IDbConnection connection, string id)
    {
        var result = connection.Query(
            "SELECT * FROM Patient WHERE Id = @id",
            [CreateParameter(connection, "@id", id)],
            MapPatient
        );

        return result switch
        {
            Result<IReadOnlyList<Patient>, SqlError>.Ok<IReadOnlyList<Patient>, SqlError> ok =>
                new Result<Patient?, SqlError>.Ok<Patient?, SqlError>(ok.Value.FirstOrDefault()),
            Result<IReadOnlyList<Patient>, SqlError>.Error<IReadOnlyList<Patient>, SqlError> err =>
                new Result<Patient?, SqlError>.Error<Patient?, SqlError>(err.Value),
            _ => new Result<Patient?, SqlError>.Error<Patient?, SqlError>(
                SqlError.Create("Unknown result type")
            ),
        };
    }

    /// <summary>
    /// Creates a new patient.
    /// </summary>
    public static Result<Patient, SqlError> Create(
        IDbConnection connection,
        CreatePatientRequest request
    )
    {
        var id = Guid.NewGuid().ToString();
        var now = DateTime
            .UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);

        var sql = """
            INSERT INTO Patient (
                Id,
                Active,
                GivenName,
                FamilyName,
                BirthDate,
                Gender,
                Phone,
                Email,
                AddressLine,
                City,
                State,
                PostalCode,
                Country,
                LastUpdated,
                VersionId
            )
            VALUES (
                @id,
                @active,
                @givenName,
                @familyName,
                @birthDate,
                @gender,
                @phone,
                @email,
                @addressLine,
                @city,
                @state,
                @postalCode,
                @country,
                @lastUpdated,
                @versionId
            )
            """;

        var parameters = new IDataParameter[]
        {
            CreateParameter(connection, "@id", id),
            CreateParameter(connection, "@active", request.Active ? 1 : 0),
            CreateParameter(connection, "@givenName", request.GivenName),
            CreateParameter(connection, "@familyName", request.FamilyName),
            CreateParameter(connection, "@birthDate", request.BirthDate ?? (object)DBNull.Value),
            CreateParameter(connection, "@gender", request.Gender ?? (object)DBNull.Value),
            CreateParameter(connection, "@phone", request.Phone ?? (object)DBNull.Value),
            CreateParameter(connection, "@email", request.Email ?? (object)DBNull.Value),
            CreateParameter(connection, "@addressLine", request.AddressLine ?? (object)DBNull.Value),
            CreateParameter(connection, "@city", request.City ?? (object)DBNull.Value),
            CreateParameter(connection, "@state", request.State ?? (object)DBNull.Value),
            CreateParameter(connection, "@postalCode", request.PostalCode ?? (object)DBNull.Value),
            CreateParameter(connection, "@country", request.Country ?? (object)DBNull.Value),
            CreateParameter(connection, "@lastUpdated", now),
            CreateParameter(connection, "@versionId", 1L),
        };

        var result = connection.Execute(sql, parameters);

        return result switch
        {
            Result<int, SqlError>.Ok<int, SqlError> =>
                new Result<Patient, SqlError>.Ok<Patient, SqlError>(
                    new Patient(
                        id,
                        request.Active,
                        request.GivenName,
                        request.FamilyName,
                        request.BirthDate,
                        request.Gender,
                        request.Phone,
                        request.Email,
                        request.AddressLine,
                        request.City,
                        request.State,
                        request.PostalCode,
                        request.Country,
                        now,
                        1
                    )
                ),
            Result<int, SqlError>.Error<int, SqlError> err =>
                new Result<Patient, SqlError>.Error<Patient, SqlError>(err.Value),
            _ => new Result<Patient, SqlError>.Error<Patient, SqlError>(
                SqlError.Create("Unknown result type")
            ),
        };
    }

    /// <summary>
    /// Searches patients by name or email.
    /// </summary>
    public static Result<IReadOnlyList<Patient>, SqlError> Search(
        IDbConnection connection,
        string searchTerm
    ) =>
        connection.Query(
            """
            SELECT *
            FROM Patient
            WHERE GivenName LIKE @term OR FamilyName LIKE @term OR Email LIKE @term
            ORDER BY FamilyName, GivenName
            """,
            [CreateParameter(connection, "@term", $"%{searchTerm}%")],
            MapPatient
        );

    private static Patient MapPatient(IDataReader reader) =>
        new(
            Id: reader.GetString(reader.GetOrdinal("Id")),
            Active: reader.GetInt64(reader.GetOrdinal("Active")) == 1,
            GivenName: reader.GetString(reader.GetOrdinal("GivenName")),
            FamilyName: reader.GetString(reader.GetOrdinal("FamilyName")),
            BirthDate: reader.IsDBNull(reader.GetOrdinal("BirthDate"))
                ? null
                : reader.GetString(reader.GetOrdinal("BirthDate")),
            Gender: reader.IsDBNull(reader.GetOrdinal("Gender"))
                ? null
                : reader.GetString(reader.GetOrdinal("Gender")),
            Phone: reader.IsDBNull(reader.GetOrdinal("Phone"))
                ? null
                : reader.GetString(reader.GetOrdinal("Phone")),
            Email: reader.IsDBNull(reader.GetOrdinal("Email"))
                ? null
                : reader.GetString(reader.GetOrdinal("Email")),
            AddressLine: reader.IsDBNull(reader.GetOrdinal("AddressLine"))
                ? null
                : reader.GetString(reader.GetOrdinal("AddressLine")),
            City: reader.IsDBNull(reader.GetOrdinal("City"))
                ? null
                : reader.GetString(reader.GetOrdinal("City")),
            State: reader.IsDBNull(reader.GetOrdinal("State"))
                ? null
                : reader.GetString(reader.GetOrdinal("State")),
            PostalCode: reader.IsDBNull(reader.GetOrdinal("PostalCode"))
                ? null
                : reader.GetString(reader.GetOrdinal("PostalCode")),
            Country: reader.IsDBNull(reader.GetOrdinal("Country"))
                ? null
                : reader.GetString(reader.GetOrdinal("Country")),
            LastUpdated: reader.GetString(reader.GetOrdinal("LastUpdated")),
            VersionId: reader.GetInt64(reader.GetOrdinal("VersionId"))
        );

    private static IDataParameter CreateParameter(IDbConnection connection, string name, object value)
    {
        var cmd = connection.CreateCommand();
        var param = cmd.CreateParameter();
        param.ParameterName = name;
        param.Value = value;
        return param;
    }
}
