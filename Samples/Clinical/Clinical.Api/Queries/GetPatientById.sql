-- Get patient by ID
-- Parameters: @id
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
WHERE Id = @id
