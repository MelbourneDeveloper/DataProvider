namespace Healthcare.AppointmentService;

/// <summary>
/// Database initialization for AppointmentService.
/// All tables follow FHIR R4 resource structure.
/// See: https://build.fhir.org/resourcelist.html
/// </summary>
public static class DatabaseSetup
{
    /// <summary>
    /// Creates the database schema and sync infrastructure.
    /// Tables conform to FHIR R4 resources.
    /// </summary>
    public static void Initialize(NpgsqlConnection connection, ILogger logger)
    {
        // Create sync infrastructure
        var schemaResult = PostgresSyncSchema.CreateSchema(connection);
        if (schemaResult is BoolSyncError err)
        {
            logger.LogError("Failed to create sync schema: {Error}", err.Value.Message);
            return;
        }

        PostgresSyncSchema.SetOriginId(connection, Guid.NewGuid().ToString());

        // Create FHIR Practitioner table
        // See: https://build.fhir.org/practitioner.html
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS practitioner (
                id TEXT PRIMARY KEY,
                identifier TEXT NOT NULL,
                active BOOLEAN NOT NULL DEFAULT true,
                name_family TEXT NOT NULL,
                name_given TEXT NOT NULL,
                qualification TEXT,
                specialty TEXT,
                telecom_email TEXT,
                telecom_phone TEXT
            );

            CREATE TABLE IF NOT EXISTS schedule (
                id TEXT PRIMARY KEY,
                active BOOLEAN NOT NULL DEFAULT true,
                practitioner_reference TEXT NOT NULL,
                planning_horizon INTEGER NOT NULL DEFAULT 30,
                comment TEXT,
                FOREIGN KEY (practitioner_reference) REFERENCES practitioner(id)
            );

            CREATE TABLE IF NOT EXISTS slot (
                id TEXT PRIMARY KEY,
                schedule_reference TEXT NOT NULL,
                status TEXT NOT NULL CHECK (status IN ('free', 'busy', 'busy-unavailable', 'busy-tentative')),
                start_time TEXT NOT NULL,
                end_time TEXT NOT NULL,
                overbooked BOOLEAN NOT NULL DEFAULT false,
                comment TEXT,
                FOREIGN KEY (schedule_reference) REFERENCES schedule(id)
            );

            CREATE TABLE IF NOT EXISTS appointment (
                id TEXT PRIMARY KEY,
                status TEXT NOT NULL CHECK (status IN ('proposed', 'pending', 'booked', 'arrived', 'fulfilled', 'cancelled', 'noshow', 'entered-in-error', 'checked-in', 'waitlist')),
                service_category TEXT,
                service_type TEXT,
                reason_code TEXT,
                priority TEXT NOT NULL CHECK (priority IN ('routine', 'urgent', 'asap', 'stat')),
                description TEXT,
                start_time TEXT NOT NULL,
                end_time TEXT NOT NULL,
                minutes_duration INTEGER NOT NULL,
                patient_reference TEXT NOT NULL,
                practitioner_reference TEXT NOT NULL,
                created TEXT NOT NULL,
                comment TEXT,
                FOREIGN KEY (practitioner_reference) REFERENCES practitioner(id)
            );

            CREATE INDEX IF NOT EXISTS idx_practitioner_identifier ON practitioner(identifier);
            CREATE INDEX IF NOT EXISTS idx_practitioner_specialty ON practitioner(specialty);
            CREATE INDEX IF NOT EXISTS idx_appointment_status ON appointment(status);
            CREATE INDEX IF NOT EXISTS idx_appointment_start ON appointment(start_time);
            CREATE INDEX IF NOT EXISTS idx_appointment_patient ON appointment(patient_reference);
            CREATE INDEX IF NOT EXISTS idx_appointment_practitioner ON appointment(practitioner_reference);
            CREATE INDEX IF NOT EXISTS idx_slot_schedule ON slot(schedule_reference);
            CREATE INDEX IF NOT EXISTS idx_slot_status ON slot(status);
            """;
        cmd.ExecuteNonQuery();

        // Create sync triggers for FHIR resources
        PostgresTriggerGenerator.CreateTriggers(connection, "practitioner", logger);
        PostgresTriggerGenerator.CreateTriggers(connection, "appointment", logger);
        PostgresTriggerGenerator.CreateTriggers(connection, "schedule", logger);
        PostgresTriggerGenerator.CreateTriggers(connection, "slot", logger);

        logger.LogInformation("AppointmentService database initialized with FHIR tables and sync triggers");
    }
}
