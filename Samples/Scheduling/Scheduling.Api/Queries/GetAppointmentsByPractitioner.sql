-- Get appointments for a practitioner
-- Parameters: @practitionerReference
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
WHERE PractitionerReference = @practitionerReference
  AND Status = 'booked'
ORDER BY StartTime
