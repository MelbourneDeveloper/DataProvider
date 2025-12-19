-- Get patients for sync to insurance system
-- Used by sync coordinator to push patient data as Member records
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
    p.AddressCountry,
    p.ExtInsurancePolicyNumber,
    p.ExtInsuranceGroupNumber,
    p.ExtInsurancePayerId,
    p.ContactName,
    p.ContactPhone,
    p.ContactRelationship,
    p.LastUpdated,
    p.VersionId
FROM Patient p
WHERE p.Active = 1
    AND p.ExtInsurancePolicyNumber IS NOT NULL
    AND (@lastSyncDate IS NULL OR p.LastUpdated > @lastSyncDate)
ORDER BY p.LastUpdated ASC
