namespace Scheduling.Api;

/// <summary>
/// Create practitioner request.
/// </summary>
public sealed record CreatePractitionerRequest(
    string Identifier,
    string NameFamily,
    string NameGiven,
    string? Qualification,
    string? Specialty,
    string? TelecomEmail,
    string? TelecomPhone
);

/// <summary>
/// Update practitioner request.
/// </summary>
public sealed record UpdatePractitionerRequest(
    string Identifier,
    bool Active,
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
public sealed record CreateAppointmentRequest(
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

/// <summary>
/// Update appointment request.
/// </summary>
public sealed record UpdateAppointmentRequest(
    string ServiceCategory,
    string ServiceType,
    string? ReasonCode,
    string Priority,
    string? Description,
    string Start,
    string End,
    string PatientReference,
    string PractitionerReference,
    string? Comment,
    string Status
);
