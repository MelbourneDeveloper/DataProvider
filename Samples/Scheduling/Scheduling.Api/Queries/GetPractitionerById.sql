-- Get practitioner by ID
-- Parameters: @id
SELECT
    Id,
    Identifier,
    Active,
    NameFamily,
    NameGiven,
    Qualification,
    Specialty,
    TelecomEmail,
    TelecomPhone
FROM fhir_Practitioner
WHERE Id = @id
