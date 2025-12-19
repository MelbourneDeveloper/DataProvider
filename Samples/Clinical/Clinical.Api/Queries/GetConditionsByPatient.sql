-- Get conditions for a patient
-- Parameters: @patientId
SELECT
    Id,
    ClinicalStatus,
    VerificationStatus,
    Category,
    Severity,
    CodeSystem,
    CodeValue,
    CodeDisplay,
    SubjectReference,
    EncounterReference,
    OnsetDateTime,
    RecordedDate,
    RecorderReference,
    NoteText,
    LastUpdated,
    VersionId
FROM fhir_Condition
WHERE SubjectReference = @patientId
ORDER BY RecordedDate DESC
