namespace Healthcare.AppointmentService;

// FHIR-style resources for healthcare scheduling

/// <summary>
/// FHIR Practitioner resource - healthcare provider.
/// Maps to FHIR R4 Practitioner resource.
/// </summary>
/// <param name="Id">Logical ID (UUID).</param>
/// <param name="Identifier">Business identifier (NPI, license number).</param>
/// <param name="Active">Whether practitioner is active.</param>
/// <param name="NameFamily">Family/last name.</param>
/// <param name="NameGiven">Given/first name.</param>
/// <param name="Qualification">Qualification code (MD, RN, etc.).</param>
/// <param name="Specialty">Medical specialty (cardiology, pediatrics, etc.).</param>
/// <param name="TelecomEmail">Contact email.</param>
/// <param name="TelecomPhone">Contact phone.</param>
public sealed record Practitioner(
    string Id,
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
/// FHIR Appointment resource - scheduled healthcare encounter.
/// Maps to FHIR R4 Appointment resource.
/// </summary>
/// <param name="Id">Logical ID (UUID).</param>
/// <param name="Status">Appointment status (proposed|pending|booked|arrived|fulfilled|cancelled|noshow).</param>
/// <param name="ServiceCategory">Category of service (consultation, follow-up, procedure).</param>
/// <param name="ServiceType">Type of service (general-checkup, cardiology-consult).</param>
/// <param name="ReasonCode">Reason for appointment.</param>
/// <param name="Priority">Urgency (routine|urgent|asap|stat).</param>
/// <param name="Description">Description of appointment.</param>
/// <param name="Start">Start datetime (ISO 8601).</param>
/// <param name="End">End datetime (ISO 8601).</param>
/// <param name="MinutesDuration">Duration in minutes.</param>
/// <param name="PatientReference">Reference to patient (Patient/uuid).</param>
/// <param name="PractitionerReference">Reference to practitioner (Practitioner/uuid).</param>
/// <param name="Created">When created (ISO 8601).</param>
/// <param name="Comment">Additional comments.</param>
public sealed record Appointment(
    string Id,
    string Status,
    string? ServiceCategory,
    string? ServiceType,
    string? ReasonCode,
    string Priority,
    string? Description,
    string Start,
    string End,
    int MinutesDuration,
    string PatientReference,
    string PractitionerReference,
    string Created,
    string? Comment
);

/// <summary>
/// FHIR Schedule resource - availability for a practitioner.
/// </summary>
/// <param name="Id">Logical ID (UUID).</param>
/// <param name="Active">Whether schedule is active.</param>
/// <param name="PractitionerReference">Reference to practitioner.</param>
/// <param name="PlanningHorizon">Planning horizon in days.</param>
/// <param name="Comment">Notes about the schedule.</param>
public sealed record Schedule(
    string Id,
    bool Active,
    string PractitionerReference,
    int PlanningHorizon,
    string? Comment
);

/// <summary>
/// FHIR Slot resource - available time slot for booking.
/// </summary>
/// <param name="Id">Logical ID (UUID).</param>
/// <param name="ScheduleReference">Reference to schedule.</param>
/// <param name="Status">Slot status (free|busy|busy-unavailable|busy-tentative).</param>
/// <param name="Start">Start datetime.</param>
/// <param name="End">End datetime.</param>
/// <param name="Overbooked">Whether slot is overbooked.</param>
/// <param name="Comment">Notes about the slot.</param>
public sealed record Slot(
    string Id,
    string ScheduleReference,
    string Status,
    string Start,
    string End,
    bool Overbooked,
    string? Comment
);

// Request DTOs

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
