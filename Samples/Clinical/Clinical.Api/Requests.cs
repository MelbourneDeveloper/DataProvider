namespace Clinical.Api;

/// <summary>
/// Patient creation request DTO.
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
/// Patient update request DTO.
/// </summary>
public sealed record UpdatePatientRequest(
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
/// Encounter creation request DTO.
/// </summary>
public sealed record CreateEncounterRequest(
    string Status,
    string Class,
    string? PractitionerId,
    string? ServiceType,
    string? ReasonCode,
    string PeriodStart,
    string? PeriodEnd,
    string? Notes
);

/// <summary>
/// Condition creation request DTO.
/// </summary>
public sealed record CreateConditionRequest(
    string ClinicalStatus,
    string? VerificationStatus,
    string? Category,
    string? Severity,
    string CodeSystem,
    string CodeValue,
    string CodeDisplay,
    string? EncounterReference,
    string? OnsetDateTime,
    string? RecorderReference,
    string? NoteText
);

/// <summary>
/// MedicationRequest creation request DTO.
/// </summary>
public sealed record CreateMedicationRequestRequest(
    string Status,
    string Intent,
    string PractitionerId,
    string? EncounterId,
    string MedicationCode,
    string MedicationDisplay,
    string? DosageInstruction,
    double? Quantity,
    string? Unit,
    int Refills
);
