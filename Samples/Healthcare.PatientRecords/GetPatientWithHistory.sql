-- Get patient with full medical history (conditions, medications, allergies)
SELECT
    p.Id, p.MedicalRecordNumber, p.FirstName, p.LastName, p.DateOfBirth,
    p.Gender, p.BloodType, p.Email, p.Phone,
    p.EmergencyContactName, p.EmergencyContactPhone,
    p.InsuranceProvider, p.InsurancePolicyNumber,
    p.IsActive, p.CreatedAt, p.UpdatedAt,
    mc.Id AS ConditionId, mc.IcdCode, mc.ConditionName, mc.DiagnosedDate,
    mc.Severity AS ConditionSeverity, mc.Status AS ConditionStatus, mc.Notes AS ConditionNotes
FROM Patient p
LEFT JOIN MedicalCondition mc ON mc.PatientId = p.Id
WHERE p.Id = @patientId
ORDER BY mc.DiagnosedDate DESC
