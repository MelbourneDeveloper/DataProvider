namespace Healthcare.PatientService;

using System.IO;

/// <summary>
/// Database initialization for PatientService.
/// </summary>
public static class DatabaseSetup
{
    /// <summary>
    /// Creates the database schema and sync infrastructure.
    /// </summary>
    public static void Initialize(SqliteConnection connection, ILogger logger)
    {
        // Create sync infrastructure
        SyncSchema.CreateSchema(connection);
        SyncSchema.SetOriginId(connection, Guid.NewGuid().ToString());

        var schemaPath = Path.Combine(AppContext.BaseDirectory, "schema.sql");
        if (File.Exists(schemaPath))
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = File.ReadAllText(schemaPath);
            cmd.ExecuteNonQuery();
            logger.LogInformation("Executed schema.sql for PatientService setup");
        }
        else
        {
            logger.LogWarning("schema.sql not found, falling back to inline schema creation");
            using var fallbackCmd = connection.CreateCommand();
            fallbackCmd.CommandText = """
                CREATE TABLE IF NOT EXISTS Patient (
                    Id TEXT PRIMARY KEY,
                    Active INTEGER NOT NULL DEFAULT 1,
                    GivenName TEXT NOT NULL,
                    FamilyName TEXT NOT NULL,
                    BirthDate TEXT,
                    Gender TEXT,
                    Phone TEXT,
                    Email TEXT,
                    AddressLine TEXT,
                    City TEXT,
                    State TEXT,
                    PostalCode TEXT,
                    Country TEXT,
                    LastUpdated TEXT NOT NULL,
                    VersionId INTEGER NOT NULL DEFAULT 1
                );

                CREATE TABLE IF NOT EXISTS MedicalRecord (
                    Id TEXT PRIMARY KEY,
                    PatientId TEXT NOT NULL,
                    VisitDate TEXT NOT NULL,
                    ChiefComplaint TEXT NOT NULL,
                    Diagnosis TEXT,
                    Treatment TEXT,
                    Prescriptions TEXT,
                    Notes TEXT,
                    ProviderId TEXT,
                    CreatedAt TEXT NOT NULL,
                    FOREIGN KEY (PatientId) REFERENCES Patient(Id) ON DELETE CASCADE
                );

                CREATE INDEX IF NOT EXISTS idx_patient_family_name ON Patient(FamilyName);
                CREATE INDEX IF NOT EXISTS idx_medical_record_patient ON MedicalRecord(PatientId);
                CREATE INDEX IF NOT EXISTS idx_medical_record_visit_date ON MedicalRecord(VisitDate);
                """;
            fallbackCmd.ExecuteNonQuery();
        }

        // Create sync triggers for both tables
        TriggerGenerator.CreateTriggers(connection, "Patient", logger);
        TriggerGenerator.CreateTriggers(connection, "MedicalRecord", logger);

        logger.LogInformation("PatientService database initialized with sync triggers");
    }
}
