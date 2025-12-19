-- Get upcoming appointments
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
WHERE Status = 'booked'
ORDER BY StartTime
LIMIT 50
