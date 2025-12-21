-- Search practitioners by specialty
-- Parameters: @specialty
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
WHERE Specialty LIKE '%' || @specialty || '%'
ORDER BY NameFamily, NameGiven
