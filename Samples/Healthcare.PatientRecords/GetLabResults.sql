-- Get lab results for a patient with optional date filtering
SELECT
    lr.Id, lr.PatientId, lr.TestName, lr.TestCode,
    lr.ResultValue, lr.ResultUnit, lr.ReferenceRange, lr.IsAbnormal,
    lr.CollectedAt, lr.ResultedAt, lr.Status, lr.Notes,
    ph.Id AS OrderingPhysicianId, ph.FirstName AS PhysicianFirstName,
    ph.LastName AS PhysicianLastName
FROM LabResult lr
INNER JOIN Physician ph ON ph.Id = lr.OrderedByPhysicianId
WHERE lr.PatientId = @patientId
  AND (@startDate IS NULL OR lr.CollectedAt >= @startDate)
  AND (@endDate IS NULL OR lr.CollectedAt <= @endDate)
  AND (@abnormalOnly IS NULL OR lr.IsAbnormal = @abnormalOnly)
ORDER BY lr.CollectedAt DESC
