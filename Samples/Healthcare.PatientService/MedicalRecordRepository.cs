using System.Globalization;

namespace Healthcare.PatientService;

/// <summary>
/// Medical record data access using DataProvider SQL extension methods.
/// </summary>
public static class MedicalRecordRepository
{
    /// <summary>
    /// Gets all medical records for a patient.
    /// </summary>
    public static Result<IReadOnlyList<MedicalRecord>, SqlError> GetByPatientId(
        IDbConnection connection,
        string patientId
    ) =>
        connection.Query(
            "SELECT * FROM MedicalRecord WHERE PatientId = @patientId ORDER BY VisitDate DESC",
            [CreateParameter(connection, "@patientId", patientId)],
            MapRecord
        );

    /// <summary>
    /// Gets a medical record by ID.
    /// </summary>
    public static Result<MedicalRecord?, SqlError> GetById(IDbConnection connection, string id)
    {
        var result = connection.Query(
            "SELECT * FROM MedicalRecord WHERE Id = @id",
            [CreateParameter(connection, "@id", id)],
            MapRecord
        );

        return result switch
        {
            Result<IReadOnlyList<MedicalRecord>, SqlError>.Ok<IReadOnlyList<MedicalRecord>, SqlError> ok =>
                new Result<MedicalRecord?, SqlError>.Ok<MedicalRecord?, SqlError>(ok.Value.FirstOrDefault()),
            Result<IReadOnlyList<MedicalRecord>, SqlError>.Error<IReadOnlyList<MedicalRecord>, SqlError> err =>
                new Result<MedicalRecord?, SqlError>.Error<MedicalRecord?, SqlError>(err.Value),
            _ => new Result<MedicalRecord?, SqlError>.Error<MedicalRecord?, SqlError>(
                SqlError.Create("Unknown result type")
            ),
        };
    }

    /// <summary>
    /// Creates a new medical record.
    /// </summary>
    public static Result<MedicalRecord, SqlError> Create(
        IDbConnection connection,
        CreateMedicalRecordRequest request
    )
    {
        var id = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);

        var sql = """
            INSERT INTO MedicalRecord (Id, PatientId, VisitDate, ChiefComplaint, Diagnosis, Treatment, Prescriptions, Notes, ProviderId, CreatedAt)
            VALUES (@id, @patientId, @visitDate, @complaint, @diagnosis, @treatment, @prescriptions, @notes, @providerId, @createdAt)
            """;

        var parameters = new IDataParameter[]
        {
            CreateParameter(connection, "@id", id),
            CreateParameter(connection, "@patientId", request.PatientId),
            CreateParameter(connection, "@visitDate", request.VisitDate),
            CreateParameter(connection, "@complaint", request.ChiefComplaint),
            CreateParameter(connection, "@diagnosis", request.Diagnosis ?? (object)DBNull.Value),
            CreateParameter(connection, "@treatment", request.Treatment ?? (object)DBNull.Value),
            CreateParameter(connection, "@prescriptions", request.Prescriptions ?? (object)DBNull.Value),
            CreateParameter(connection, "@notes", request.Notes ?? (object)DBNull.Value),
            CreateParameter(connection, "@providerId", request.ProviderId ?? (object)DBNull.Value),
            CreateParameter(connection, "@createdAt", now),
        };

        var result = connection.Execute(sql, parameters);

        return result switch
        {
            Result<int, SqlError>.Ok<int, SqlError> => new Result<MedicalRecord, SqlError>.Ok<MedicalRecord, SqlError>(
                new MedicalRecord(
                    id,
                    request.PatientId,
                    request.VisitDate,
                    request.ChiefComplaint,
                    request.Diagnosis,
                    request.Treatment,
                    request.Prescriptions,
                    request.Notes,
                    request.ProviderId,
                    now
                )
            ),
            Result<int, SqlError>.Error<int, SqlError> err =>
                new Result<MedicalRecord, SqlError>.Error<MedicalRecord, SqlError>(err.Value),
            _ => new Result<MedicalRecord, SqlError>.Error<MedicalRecord, SqlError>(
                SqlError.Create("Unknown result type")
            ),
        };
    }

    private static MedicalRecord MapRecord(IDataReader reader) =>
        new(
            Id: reader.GetString(reader.GetOrdinal("Id")),
            PatientId: reader.GetString(reader.GetOrdinal("PatientId")),
            VisitDate: reader.GetString(reader.GetOrdinal("VisitDate")),
            ChiefComplaint: reader.GetString(reader.GetOrdinal("ChiefComplaint")),
            Diagnosis: reader.IsDBNull(reader.GetOrdinal("Diagnosis"))
                ? null
                : reader.GetString(reader.GetOrdinal("Diagnosis")),
            Treatment: reader.IsDBNull(reader.GetOrdinal("Treatment"))
                ? null
                : reader.GetString(reader.GetOrdinal("Treatment")),
            Prescriptions: reader.IsDBNull(reader.GetOrdinal("Prescriptions"))
                ? null
                : reader.GetString(reader.GetOrdinal("Prescriptions")),
            Notes: reader.IsDBNull(reader.GetOrdinal("Notes"))
                ? null
                : reader.GetString(reader.GetOrdinal("Notes")),
            ProviderId: reader.IsDBNull(reader.GetOrdinal("ProviderId"))
                ? null
                : reader.GetString(reader.GetOrdinal("ProviderId")),
            CreatedAt: reader.GetString(reader.GetOrdinal("CreatedAt"))
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
