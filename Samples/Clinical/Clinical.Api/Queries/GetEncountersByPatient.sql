-- Get encounters for a patient
-- Parameters: @patientId
SELECT
    Id,
    Status,
    Class,
    PatientId,
    PractitionerId,
    ServiceType,
    ReasonCode,
    PeriodStart,
    PeriodEnd,
    Notes,
    LastUpdated,
    VersionId
FROM fhir_Encounter
WHERE PatientId = @patientId
ORDER BY PeriodStart DESC
