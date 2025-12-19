-- Get all practitioners
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
ORDER BY NameFamily, NameGiven
