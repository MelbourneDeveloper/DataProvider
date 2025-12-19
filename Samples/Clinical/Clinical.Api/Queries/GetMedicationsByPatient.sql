-- Get medication requests for a patient
-- Parameters: @patientId
SELECT
    Id,
    Status,
    Intent,
    PatientId,
    PractitionerId,
    EncounterId,
    MedicationCode,
    MedicationDisplay,
    DosageInstruction,
    Quantity,
    Unit,
    Refills,
    AuthoredOn,
    LastUpdated,
    VersionId
FROM fhir_MedicationRequest
WHERE PatientId = @patientId
ORDER BY AuthoredOn DESC
