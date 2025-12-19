-- Get all allergies for a patient - CRITICAL for clinical safety
SELECT
    Id, PatientId, AllergenName, AllergenType, Severity, Reaction, ConfirmedDate
FROM Allergy
WHERE PatientId = @patientId
ORDER BY
    CASE Severity
        WHEN 'LifeThreatening' THEN 1
        WHEN 'Severe' THEN 2
        WHEN 'Moderate' THEN 3
        WHEN 'Mild' THEN 4
    END
