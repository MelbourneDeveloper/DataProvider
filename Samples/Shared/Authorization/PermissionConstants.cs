namespace Samples.Authorization;

/// <summary>
/// FHIR-style permission codes for Clinical and Scheduling domains.
/// </summary>
public static class FhirPermissions
{
    // Patient resource permissions
    /// <summary>Read patient records.</summary>
    public const string PatientRead = "patient:read";

    /// <summary>Create patient records.</summary>
    public const string PatientCreate = "patient:create";

    /// <summary>Update patient records.</summary>
    public const string PatientUpdate = "patient:update";

    /// <summary>Delete patient records.</summary>
    public const string PatientDelete = "patient:delete";

    /// <summary>Full patient access (wildcard).</summary>
    public const string PatientAll = "patient:*";

    // Encounter resource permissions
    /// <summary>Read encounter records.</summary>
    public const string EncounterRead = "encounter:read";

    /// <summary>Create encounter records.</summary>
    public const string EncounterCreate = "encounter:create";

    /// <summary>Update encounter records.</summary>
    public const string EncounterUpdate = "encounter:update";

    /// <summary>Full encounter access (wildcard).</summary>
    public const string EncounterAll = "encounter:*";

    // Condition resource permissions
    /// <summary>Read condition records.</summary>
    public const string ConditionRead = "condition:read";

    /// <summary>Create condition records.</summary>
    public const string ConditionCreate = "condition:create";

    /// <summary>Update condition records.</summary>
    public const string ConditionUpdate = "condition:update";

    /// <summary>Full condition access (wildcard).</summary>
    public const string ConditionAll = "condition:*";

    // MedicationRequest resource permissions
    /// <summary>Read medication request records.</summary>
    public const string MedicationRequestRead = "medicationrequest:read";

    /// <summary>Create medication request records.</summary>
    public const string MedicationRequestCreate = "medicationrequest:create";

    /// <summary>Update medication request records.</summary>
    public const string MedicationRequestUpdate = "medicationrequest:update";

    /// <summary>Full medication request access (wildcard).</summary>
    public const string MedicationRequestAll = "medicationrequest:*";

    // Practitioner resource permissions
    /// <summary>Read practitioner records.</summary>
    public const string PractitionerRead = "practitioner:read";

    /// <summary>Create practitioner records.</summary>
    public const string PractitionerCreate = "practitioner:create";

    /// <summary>Update practitioner records.</summary>
    public const string PractitionerUpdate = "practitioner:update";

    /// <summary>Full practitioner access (wildcard).</summary>
    public const string PractitionerAll = "practitioner:*";

    // Appointment resource permissions
    /// <summary>Read appointment records.</summary>
    public const string AppointmentRead = "appointment:read";

    /// <summary>Create appointment records.</summary>
    public const string AppointmentCreate = "appointment:create";

    /// <summary>Update appointment records.</summary>
    public const string AppointmentUpdate = "appointment:update";

    /// <summary>Full appointment access (wildcard).</summary>
    public const string AppointmentAll = "appointment:*";

    // Sync operation permissions
    /// <summary>Read sync data.</summary>
    public const string SyncRead = "sync:read";

    /// <summary>Write sync data.</summary>
    public const string SyncWrite = "sync:write";

    /// <summary>Full sync access (wildcard).</summary>
    public const string SyncAll = "sync:*";
}
