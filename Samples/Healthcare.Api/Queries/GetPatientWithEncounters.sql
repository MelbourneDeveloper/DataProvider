-- Get patient with all encounters (FHIR Patient + Encounter resources)
-- Used for patient detail view in healthcare portal
SELECT
    p.Id,
    p.Identifier,
    p.Active,
    p.FamilyName,
    p.GivenName,
    p.BirthDate,
    p.Gender,
    p.TelecomEmail,
    p.TelecomPhone,
    p.AddressLine,
    p.AddressCity,
    p.AddressState,
    p.AddressPostalCode,
    p.ExtInsurancePolicyNumber,
    p.ExtInsuranceGroupNumber,
    p.LastUpdated AS PatientLastUpdated,
    e.Id AS EncounterId,
    e.Identifier AS EncounterIdentifier,
    e.Status AS EncounterStatus,
    e.Class AS EncounterClass,
    e.Type AS EncounterType,
    e.TypeDisplay,
    e.PeriodStart,
    e.PeriodEnd,
    e.DiagnosisCode,
    e.DiagnosisDisplay,
    e.ParticipantName,
    e.ServiceProviderName,
    e.ExtTotalCharge,
    e.ExtClaimStatus,
    e.LastUpdated AS EncounterLastUpdated
FROM Patient p
LEFT JOIN Encounter e ON e.SubjectId = p.Id
WHERE p.Id = @patientId
ORDER BY e.PeriodStart DESC
