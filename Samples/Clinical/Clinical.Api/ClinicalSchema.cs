using Migration;
using static Migration.PortableTypes;

namespace Clinical.Api;

/// <summary>
/// Database-independent schema definition for Clinical FHIR R4 resources.
/// See: https://hl7.org/fhir/R4/
/// </summary>
public static class ClinicalSchema
{
    /// <summary>
    /// Gets the complete Clinical database schema definition.
    /// </summary>
    public static SchemaDefinition Definition { get; } = BuildSchema();

    private static SchemaDefinition BuildSchema() =>
        Schema
            .Define("clinical")
            .Table(
                "fhir_Patient",
                t =>
                    t.Column("Id", Text, c => c.PrimaryKey())
                        .Column("Active", Int, c => c.NotNull().Default("1"))
                        .Column("GivenName", Text, c => c.NotNull())
                        .Column("FamilyName", Text, c => c.NotNull())
                        .Column("BirthDate", Text)
                        .Column(
                            "Gender",
                            Text,
                            c => c.Check("Gender IN ('male', 'female', 'other', 'unknown')")
                        )
                        .Column("Phone", Text)
                        .Column("Email", Text)
                        .Column("AddressLine", Text)
                        .Column("City", Text)
                        .Column("State", Text)
                        .Column("PostalCode", Text)
                        .Column("Country", Text)
                        .Column("LastUpdated", Text, c => c.NotNull().Default("(datetime('now'))"))
                        .Column("VersionId", Int, c => c.NotNull().Default("1"))
                        .Index("idx_fhir_patient_family", "FamilyName")
                        .Index("idx_fhir_patient_given", "GivenName")
            )
            .Table(
                "fhir_Encounter",
                t =>
                    t.Column("Id", Text, c => c.PrimaryKey())
                        .Column(
                            "Status",
                            Text,
                            c =>
                                c.NotNull()
                                    .Check(
                                        "Status IN ('planned', 'arrived', 'triaged', 'in-progress', 'onleave', 'finished', 'cancelled', 'entered-in-error')"
                                    )
                        )
                        .Column(
                            "Class",
                            Text,
                            c =>
                                c.NotNull()
                                    .Check(
                                        "Class IN ('ambulatory', 'emergency', 'inpatient', 'observation', 'virtual')"
                                    )
                        )
                        .Column("PatientId", Text, c => c.NotNull())
                        .Column("PractitionerId", Text)
                        .Column("ServiceType", Text)
                        .Column("ReasonCode", Text)
                        .Column("PeriodStart", Text, c => c.NotNull())
                        .Column("PeriodEnd", Text)
                        .Column("Notes", Text)
                        .Column("LastUpdated", Text, c => c.NotNull().Default("(datetime('now'))"))
                        .Column("VersionId", Int, c => c.NotNull().Default("1"))
                        .ForeignKey("PatientId", "fhir_Patient", "Id")
                        .Index("idx_fhir_encounter_patient", "PatientId")
            )
            .Table(
                "fhir_Condition",
                t =>
                    t.Column("Id", Text, c => c.PrimaryKey())
                        .Column(
                            "ClinicalStatus",
                            Text,
                            c =>
                                c.NotNull()
                                    .Check(
                                        "ClinicalStatus IN ('active', 'recurrence', 'relapse', 'inactive', 'remission', 'resolved')"
                                    )
                        )
                        .Column(
                            "VerificationStatus",
                            Text,
                            c =>
                                c.Check(
                                    "VerificationStatus IN ('unconfirmed', 'provisional', 'differential', 'confirmed', 'refuted', 'entered-in-error')"
                                )
                        )
                        .Column("Category", Text, c => c.Default("'problem-list-item'"))
                        .Column(
                            "Severity",
                            Text,
                            c => c.Check("Severity IN ('mild', 'moderate', 'severe')")
                        )
                        .Column(
                            "CodeSystem",
                            Text,
                            c => c.NotNull().Default("'http://hl7.org/fhir/sid/icd-10-cm'")
                        )
                        .Column("CodeValue", Text, c => c.NotNull())
                        .Column("CodeDisplay", Text, c => c.NotNull())
                        .Column("SubjectReference", Text, c => c.NotNull())
                        .Column("EncounterReference", Text)
                        .Column("OnsetDateTime", Text)
                        .Column("RecordedDate", Text, c => c.NotNull().Default("(date('now'))"))
                        .Column("RecorderReference", Text)
                        .Column("NoteText", Text)
                        .Column("LastUpdated", Text, c => c.NotNull().Default("(datetime('now'))"))
                        .Column("VersionId", Int, c => c.NotNull().Default("1"))
                        .ForeignKey("SubjectReference", "fhir_Patient", "Id")
                        .Index("idx_fhir_condition_patient", "SubjectReference")
            )
            .Table(
                "fhir_MedicationRequest",
                t =>
                    t.Column("Id", Text, c => c.PrimaryKey())
                        .Column(
                            "Status",
                            Text,
                            c =>
                                c.NotNull()
                                    .Check(
                                        "Status IN ('active', 'on-hold', 'cancelled', 'completed', 'entered-in-error', 'stopped', 'draft')"
                                    )
                        )
                        .Column(
                            "Intent",
                            Text,
                            c =>
                                c.NotNull()
                                    .Check(
                                        "Intent IN ('proposal', 'plan', 'order', 'original-order', 'reflex-order', 'filler-order', 'instance-order', 'option')"
                                    )
                        )
                        .Column("PatientId", Text, c => c.NotNull())
                        .Column("PractitionerId", Text, c => c.NotNull())
                        .Column("EncounterId", Text)
                        .Column("MedicationCode", Text, c => c.NotNull())
                        .Column("MedicationDisplay", Text, c => c.NotNull())
                        .Column("DosageInstruction", Text)
                        .Column("Quantity", Float64)
                        .Column("Unit", Text)
                        .Column("Refills", Int, c => c.NotNull().Default("0"))
                        .Column("AuthoredOn", Text, c => c.NotNull().Default("(datetime('now'))"))
                        .Column("LastUpdated", Text, c => c.NotNull().Default("(datetime('now'))"))
                        .Column("VersionId", Int, c => c.NotNull().Default("1"))
                        .ForeignKey("PatientId", "fhir_Patient", "Id")
                        .ForeignKey("EncounterId", "fhir_Encounter", "Id")
                        .Index("idx_fhir_medication_patient", "PatientId")
            )
            .Table(
                "sync_Provider",
                t =>
                    t.Column("ProviderId", Text, c => c.PrimaryKey())
                        .Column("FirstName", Text, c => c.NotNull())
                        .Column("LastName", Text, c => c.NotNull())
                        .Column("Specialty", Text)
                        .Column("SyncedAt", Text, c => c.NotNull().Default("(datetime('now'))"))
            )
            .Build();
}
