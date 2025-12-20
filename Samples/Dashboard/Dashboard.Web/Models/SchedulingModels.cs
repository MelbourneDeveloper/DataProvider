namespace Dashboard.Models;

using H5;

/// <summary>
/// FHIR Practitioner resource model.
/// </summary>
[External]
[Name("Object")]
public record Practitioner
{
    /// <summary>Practitioner unique identifier.</summary>
    public extern string Id { get; init; }

    /// <summary>Practitioner identifier (NPI).</summary>
    public extern string Identifier { get; init; }

    /// <summary>Whether practitioner is active.</summary>
    public extern bool Active { get; init; }

    /// <summary>Family name.</summary>
    public extern string NameFamily { get; init; }

    /// <summary>Given name.</summary>
    public extern string NameGiven { get; init; }

    /// <summary>Qualification.</summary>
    public extern string? Qualification { get; init; }

    /// <summary>Specialty.</summary>
    public extern string? Specialty { get; init; }

    /// <summary>Email.</summary>
    public extern string? TelecomEmail { get; init; }

    /// <summary>Phone.</summary>
    public extern string? TelecomPhone { get; init; }
}

/// <summary>
/// FHIR Appointment resource model.
/// </summary>
[External]
[Name("Object")]
public record Appointment
{
    /// <summary>Appointment unique identifier.</summary>
    public extern string Id { get; init; }

    /// <summary>Appointment status.</summary>
    public extern string Status { get; init; }

    /// <summary>Service category.</summary>
    public extern string? ServiceCategory { get; init; }

    /// <summary>Service type.</summary>
    public extern string? ServiceType { get; init; }

    /// <summary>Reason code.</summary>
    public extern string? ReasonCode { get; init; }

    /// <summary>Priority.</summary>
    public extern string Priority { get; init; }

    /// <summary>Description.</summary>
    public extern string? Description { get; init; }

    /// <summary>Start time.</summary>
    public extern string StartTime { get; init; }

    /// <summary>End time.</summary>
    public extern string EndTime { get; init; }

    /// <summary>Duration in minutes.</summary>
    public extern int MinutesDuration { get; init; }

    /// <summary>Patient reference.</summary>
    public extern string PatientReference { get; init; }

    /// <summary>Practitioner reference.</summary>
    public extern string PractitionerReference { get; init; }

    /// <summary>Created timestamp.</summary>
    public extern string Created { get; init; }

    /// <summary>Comment.</summary>
    public extern string? Comment { get; init; }
}

/// <summary>
/// FHIR Schedule resource model.
/// </summary>
[External]
[Name("Object")]
public record Schedule
{
    /// <summary>Schedule unique identifier.</summary>
    public extern string Id { get; init; }

    /// <summary>Whether schedule is active.</summary>
    public extern bool Active { get; init; }

    /// <summary>Practitioner reference.</summary>
    public extern string PractitionerReference { get; init; }

    /// <summary>Planning horizon in days.</summary>
    public extern int PlanningHorizon { get; init; }

    /// <summary>Comment.</summary>
    public extern string? Comment { get; init; }
}

/// <summary>
/// FHIR Slot resource model.
/// </summary>
[External]
[Name("Object")]
public record Slot
{
    /// <summary>Slot unique identifier.</summary>
    public extern string Id { get; init; }

    /// <summary>Schedule reference.</summary>
    public extern string ScheduleReference { get; init; }

    /// <summary>Slot status.</summary>
    public extern string Status { get; init; }

    /// <summary>Start time.</summary>
    public extern string StartTime { get; init; }

    /// <summary>End time.</summary>
    public extern string EndTime { get; init; }

    /// <summary>Whether overbooked.</summary>
    public extern bool Overbooked { get; init; }

    /// <summary>Comment.</summary>
    public extern string? Comment { get; init; }
}
