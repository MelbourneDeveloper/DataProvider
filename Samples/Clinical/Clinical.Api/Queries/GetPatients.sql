-- Get patients with optional FHIR search parameters
-- Parameters: @active, @familyName, @givenName, @gender
SELECT
    p.Id,
    p.Active,
    p.GivenName,
    p.FamilyName,
    p.BirthDate,
    p.Gender,
    p.Phone,
    p.Email,
    p.AddressLine,
    p.City,
    p.State,
    p.PostalCode,
    p.Country,
    p.LastUpdated,
    p.VersionId
FROM fhir_Patient p
WHERE (@active IS NULL OR p.Active = @active)
  AND (@familyName IS NULL OR p.FamilyName LIKE '%' || @familyName || '%')
  AND (@givenName IS NULL OR p.GivenName LIKE '%' || @givenName || '%')
  AND (@gender IS NULL OR p.Gender = @gender)
ORDER BY p.FamilyName, p.GivenName
