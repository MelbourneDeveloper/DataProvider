namespace Dashboard.Models;

using H5;

/// <summary>
/// FHIR Patient resource model.
/// </summary>
[External]
[Name("Object")]
public record Patient
{
    /// <summary>Patient unique identifier.</summary>
    public extern string Id { get; init; }

    /// <summary>Whether patient record is active.</summary>
    public extern bool Active { get; init; }

    /// <summary>Patient's given name.</summary>
    public extern string GivenName { get; init; }

    /// <summary>Patient's family name.</summary>
    public extern string FamilyName { get; init; }

    /// <summary>Patient's birth date.</summary>
    public extern string? BirthDate { get; init; }

    /// <summary>Patient's gender.</summary>
    public extern string? Gender { get; init; }

    /// <summary>Patient's phone number.</summary>
    public extern string? Phone { get; init; }

    /// <summary>Patient's email address.</summary>
    public extern string? Email { get; init; }

    /// <summary>Patient's address line.</summary>
    public extern string? AddressLine { get; init; }

    /// <summary>Patient's city.</summary>
    public extern string? City { get; init; }

    /// <summary>Patient's state.</summary>
    public extern string? State { get; init; }

    /// <summary>Patient's postal code.</summary>
    public extern string? PostalCode { get; init; }

    /// <summary>Patient's country.</summary>
    public extern string? Country { get; init; }

    /// <summary>Last updated timestamp.</summary>
    public extern string LastUpdated { get; init; }

    /// <summary>Version identifier.</summary>
    public extern long VersionId { get; init; }
}

/// <summary>
/// FHIR Encounter resource model.
/// </summary>
[External]
[Name("Object")]
public record Encounter
{
    /// <summary>Encounter unique identifier.</summary>
    public extern string Id { get; init; }

    /// <summary>Encounter status.</summary>
    public extern string Status { get; init; }

    /// <summary>Encounter class.</summary>
    public extern string Class { get; init; }

    /// <summary>Patient reference.</summary>
    public extern string PatientId { get; init; }

    /// <summary>Practitioner reference.</summary>
    public extern string? PractitionerId { get; init; }

    /// <summary>Service type.</summary>
    public extern string? ServiceType { get; init; }

    /// <summary>Reason code.</summary>
    public extern string? ReasonCode { get; init; }

    /// <summary>Period start.</summary>
    public extern string PeriodStart { get; init; }

    /// <summary>Period end.</summary>
    public extern string? PeriodEnd { get; init; }

    /// <summary>Notes.</summary>
    public extern string? Notes { get; init; }

    /// <summary>Last updated timestamp.</summary>
    public extern string LastUpdated { get; init; }

    /// <summary>Version identifier.</summary>
    public extern long VersionId { get; init; }
}

/// <summary>
/// FHIR Condition resource model.
/// </summary>
[External]
[Name("Object")]
public record Condition
{
    /// <summary>Condition unique identifier.</summary>
    public extern string Id { get; init; }

    /// <summary>Clinical status.</summary>
    public extern string ClinicalStatus { get; init; }

    /// <summary>Verification status.</summary>
    public extern string? VerificationStatus { get; init; }

    /// <summary>Condition category.</summary>
    public extern string Category { get; init; }

    /// <summary>Severity.</summary>
    public extern string? Severity { get; init; }

    /// <summary>ICD-10 code value.</summary>
    public extern string CodeValue { get; init; }

    /// <summary>Code display name.</summary>
    public extern string CodeDisplay { get; init; }

    /// <summary>Subject reference (patient).</summary>
    public extern string SubjectReference { get; init; }

    /// <summary>Onset date/time.</summary>
    public extern string? OnsetDateTime { get; init; }

    /// <summary>Recorded date.</summary>
    public extern string RecordedDate { get; init; }

    /// <summary>Note text.</summary>
    public extern string? NoteText { get; init; }
}

/// <summary>
/// FHIR MedicationRequest resource model.
/// </summary>
[External]
[Name("Object")]
public record MedicationRequest
{
    /// <summary>MedicationRequest unique identifier.</summary>
    public extern string Id { get; init; }

    /// <summary>Status.</summary>
    public extern string Status { get; init; }

    /// <summary>Intent.</summary>
    public extern string Intent { get; init; }

    /// <summary>Patient reference.</summary>
    public extern string PatientId { get; init; }

    /// <summary>Practitioner reference.</summary>
    public extern string PractitionerId { get; init; }

    /// <summary>Medication code (RxNorm).</summary>
    public extern string MedicationCode { get; init; }

    /// <summary>Medication display name.</summary>
    public extern string MedicationDisplay { get; init; }

    /// <summary>Dosage instruction.</summary>
    public extern string? DosageInstruction { get; init; }

    /// <summary>Quantity.</summary>
    public extern double? Quantity { get; init; }

    /// <summary>Unit.</summary>
    public extern string? Unit { get; init; }

    /// <summary>Number of refills.</summary>
    public extern int Refills { get; init; }

    /// <summary>Authored on date.</summary>
    public extern string AuthoredOn { get; init; }
}
