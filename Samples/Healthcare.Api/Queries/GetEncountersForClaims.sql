-- Get encounters ready for insurance claim submission
-- This data will sync to Insurance.Claim via mapping
SELECT
    e.Id,
    e.Identifier,
    e.Status,
    e.Class,
    e.Type,
    e.TypeDisplay,
    e.ServiceType,
    e.Priority,
    e.SubjectId,
    e.ParticipantId,
    e.ParticipantName,
    e.PeriodStart,
    e.PeriodEnd,
    e.ReasonCode,
    e.ReasonDisplay,
    e.DiagnosisCode,
    e.DiagnosisDisplay,
    e.ServiceProviderId,
    e.ServiceProviderName,
    e.ExtTotalCharge,
    e.ExtClaimStatus,
    e.ExtClaimSubmittedDate,
    e.LastUpdated,
    -- Patient info for claim
    p.Identifier AS PatientMRN,
    p.FamilyName AS PatientFamilyName,
    p.GivenName AS PatientGivenName,
    p.BirthDate AS PatientBirthDate,
    p.Gender AS PatientGender,
    p.ExtInsurancePolicyNumber,
    p.ExtInsuranceGroupNumber,
    p.ExtInsurancePayerId
FROM Encounter e
JOIN Patient p ON p.Id = e.SubjectId
WHERE e.Status = 'finished'
    AND e.ExtClaimStatus = @claimStatus
    AND (@startDate IS NULL OR e.PeriodStart >= @startDate)
    AND (@endDate IS NULL OR e.PeriodStart <= @endDate)
ORDER BY e.PeriodStart DESC
