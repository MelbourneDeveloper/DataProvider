namespace Healthcare.PatientService;

/// <summary>
/// Patient record with medical profile information.
/// </summary>
/// <param name="Id">UUID primary key.</param>
/// <param name="Active">Whether the patient is active.</param>
/// <param name="GivenName">Patient's given name.</param>
/// <param name="FamilyName">Patient's family name.</param>
/// <param name="BirthDate">Date of birth (FHIR date string).</param>
/// <param name="Gender">FHIR administrative gender.</param>
/// <param name="Phone">Contact phone number.</param>
/// <param name="Email">Contact email.</param>
/// <param name="AddressLine">Street address.</param>
/// <param name="City">City.</param>
/// <param name="State">State or province.</param>
/// <param name="PostalCode">Postal/ZIP code.</param>
/// <param name="Country">Country.</param>
/// <param name="LastUpdated">FHIR meta.lastUpdated timestamp.</param>
/// <param name="VersionId">FHIR meta.versionId value.</param>
public sealed record Patient(
    string Id,
    bool Active,
    string GivenName,
    string FamilyName,
    string? BirthDate,
    string? Gender,
    string? Phone,
    string? Email,
    string? AddressLine,
    string? City,
    string? State,
    string? PostalCode,
    string? Country,
    string LastUpdated,
    long VersionId
);

/// <summary>
/// Medical record for a patient visit.
/// </summary>
/// <param name="Id">UUID primary key.</param>
/// <param name="PatientId">Reference to patient.</param>
/// <param name="VisitDate">Date of the medical visit.</param>
/// <param name="ChiefComplaint">Primary reason for visit.</param>
/// <param name="Diagnosis">Physician's diagnosis.</param>
/// <param name="Treatment">Prescribed treatment plan.</param>
/// <param name="Prescriptions">Medications prescribed (JSON array).</param>
/// <param name="Notes">Additional clinical notes.</param>
/// <param name="ProviderId">Healthcare provider ID (synced from AppointmentService).</param>
/// <param name="CreatedAt">Record creation timestamp.</param>
public sealed record MedicalRecord(
    string Id,
    string PatientId,
    string VisitDate,
    string ChiefComplaint,
    string? Diagnosis,
    string? Treatment,
    string? Prescriptions,
    string? Notes,
    string? ProviderId,
    string CreatedAt
);

/// <summary>
/// Patient creation request.
/// </summary>
public sealed record CreatePatientRequest(
    bool Active,
    string GivenName,
    string FamilyName,
    string? BirthDate,
    string? Gender,
    string? Phone,
    string? Email,
    string? AddressLine,
    string? City,
    string? State,
    string? PostalCode,
    string? Country
);

/// <summary>
/// Medical record creation request.
/// </summary>
public sealed record CreateMedicalRecordRequest(
    string PatientId,
    string VisitDate,
    string ChiefComplaint,
    string? Diagnosis,
    string? Treatment,
    string? Prescriptions,
    string? Notes,
    string? ProviderId
);
