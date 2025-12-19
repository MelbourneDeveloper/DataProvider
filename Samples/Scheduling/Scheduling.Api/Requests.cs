namespace Scheduling.Api;

/// <summary>
/// Create practitioner request.
/// </summary>
internal sealed record CreatePractitionerRequest(
    string Identifier,
    string NameFamily,
    string NameGiven,
    string? Qualification,
    string? Specialty,
    string? TelecomEmail,
    string? TelecomPhone
);

/// <summary>
/// Create appointment request.
/// </summary>
internal sealed record CreateAppointmentRequest(
    string ServiceCategory,
    string ServiceType,
    string? ReasonCode,
    string Priority,
    string? Description,
    string Start,
    string End,
    string PatientReference,
    string PractitionerReference,
    string? Comment
);
