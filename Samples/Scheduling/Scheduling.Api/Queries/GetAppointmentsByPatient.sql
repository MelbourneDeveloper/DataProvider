-- Get appointments for a patient
-- Parameters: @patientReference
SELECT
    Id,
    Status,
    ServiceCategory,
    ServiceType,
    ReasonCode,
    Priority,
    Description,
    StartTime,
    EndTime,
    MinutesDuration,
    PatientReference,
    PractitionerReference,
    Created,
    Comment
FROM fhir_Appointment
WHERE PatientReference = @patientReference
ORDER BY StartTime DESC
