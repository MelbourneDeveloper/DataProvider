-- Get appointment by ID
-- Parameters: @id
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
WHERE Id = @id
