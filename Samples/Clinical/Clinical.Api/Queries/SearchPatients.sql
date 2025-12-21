-- Search patients by name or email
-- Parameters: @term
SELECT
    Id,
    Active,
    GivenName,
    FamilyName,
    BirthDate,
    Gender,
    Phone,
    Email,
    AddressLine,
    City,
    State,
    PostalCode,
    Country,
    LastUpdated,
    VersionId
FROM fhir_Patient
WHERE GivenName LIKE @term
   OR FamilyName LIKE @term
   OR Email LIKE @term
ORDER BY FamilyName, GivenName
