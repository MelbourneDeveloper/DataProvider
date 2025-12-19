-- Get active medications for a patient with prescribing physician
SELECT
    m.Id, m.PatientId, m.DrugName, m.Dosage, m.Frequency,
    m.StartDate, m.EndDate, m.IsActive, m.RefillsRemaining, m.PharmacyNotes,
    ph.Id AS PhysicianId, ph.FirstName AS PhysicianFirstName,
    ph.LastName AS PhysicianLastName, ph.Specialty AS PhysicianSpecialty
FROM Medication m
INNER JOIN Physician ph ON ph.Id = m.PrescribedByPhysicianId
WHERE m.PatientId = @patientId
  AND (@activeOnly IS NULL OR m.IsActive = @activeOnly)
ORDER BY m.IsActive DESC, m.StartDate DESC
