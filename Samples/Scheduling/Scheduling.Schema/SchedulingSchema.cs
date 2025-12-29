using Migration;
using static Migration.PortableTypes;

namespace Scheduling.Schema;

/// <summary>
/// Database-independent schema definition for Scheduling FHIR R4 resources.
/// See: https://build.fhir.org/resourcelist.html
/// </summary>
public static class SchedulingSchema
{
    /// <summary>
    /// Gets the complete Scheduling database schema definition.
    /// </summary>
    public static SchemaDefinition Definition { get; } = BuildSchema();

    private static SchemaDefinition BuildSchema() =>
        Schema
            .Define("scheduling")
            .Table(
                "fhir_Practitioner",
                t =>
                    t.Column("Id", Text, c => c.PrimaryKey())
                        .Column("Identifier", Text, c => c.NotNull())
                        .Column("Active", Int, c => c.NotNull().Default("1"))
                        .Column("NameFamily", Text, c => c.NotNull())
                        .Column("NameGiven", Text, c => c.NotNull())
                        .Column("Qualification", Text)
                        .Column("Specialty", Text)
                        .Column("TelecomEmail", Text)
                        .Column("TelecomPhone", Text)
                        .Index("idx_practitioner_identifier", "Identifier")
                        .Index("idx_practitioner_specialty", "Specialty")
            )
            .Table(
                "fhir_Schedule",
                t =>
                    t.Column("Id", Text, c => c.PrimaryKey())
                        .Column("Active", Int, c => c.NotNull().Default("1"))
                        .Column("PractitionerReference", Text, c => c.NotNull())
                        .Column("PlanningHorizon", Int, c => c.NotNull().Default("30"))
                        .Column("Comment", Text)
                        .ForeignKey("PractitionerReference", "fhir_Practitioner", "Id")
                        .Index("idx_schedule_practitioner", "PractitionerReference")
            )
            .Table(
                "fhir_Slot",
                t =>
                    t.Column("Id", Text, c => c.PrimaryKey())
                        .Column("ScheduleReference", Text, c => c.NotNull())
                        .Column(
                            "Status",
                            Text,
                            c =>
                                c.NotNull()
                                    .Check(
                                        "Status IN ('free', 'busy', 'busy-unavailable', 'busy-tentative')"
                                    )
                        )
                        .Column("StartTime", Text, c => c.NotNull())
                        .Column("EndTime", Text, c => c.NotNull())
                        .Column("Overbooked", Int, c => c.NotNull().Default("0"))
                        .Column("Comment", Text)
                        .ForeignKey("ScheduleReference", "fhir_Schedule", "Id")
                        .Index("idx_slot_schedule", "ScheduleReference")
                        .Index("idx_slot_status", "Status")
            )
            .Table(
                "fhir_Appointment",
                t =>
                    t.Column("Id", Text, c => c.PrimaryKey())
                        .Column(
                            "Status",
                            Text,
                            c =>
                                c.NotNull()
                                    .Check(
                                        "Status IN ('proposed', 'pending', 'booked', 'arrived', 'fulfilled', 'cancelled', 'noshow', 'entered-in-error', 'checked-in', 'waitlist')"
                                    )
                        )
                        .Column("ServiceCategory", Text)
                        .Column("ServiceType", Text)
                        .Column("ReasonCode", Text)
                        .Column(
                            "Priority",
                            Text,
                            c =>
                                c.NotNull()
                                    .Check("Priority IN ('routine', 'urgent', 'asap', 'stat')")
                        )
                        .Column("Description", Text)
                        .Column("StartTime", Text, c => c.NotNull())
                        .Column("EndTime", Text, c => c.NotNull())
                        .Column("MinutesDuration", Int, c => c.NotNull())
                        .Column("PatientReference", Text, c => c.NotNull())
                        .Column("PractitionerReference", Text, c => c.NotNull())
                        .Column("Created", Text, c => c.NotNull())
                        .Column("Comment", Text)
                        .Index("idx_appointment_status", "Status")
                        .Index("idx_appointment_patient", "PatientReference")
                        .Index("idx_appointment_practitioner", "PractitionerReference")
            )
            .Table(
                "sync_ScheduledPatient",
                t =>
                    t.Column("PatientId", Text, c => c.PrimaryKey())
                        .Column("DisplayName", Text, c => c.NotNull())
                        .Column("ContactPhone", Text)
                        .Column("ContactEmail", Text)
                        .Column("SyncedAt", Text, c => c.NotNull().Default("(datetime('now'))"))
            )
            .Build();
}
