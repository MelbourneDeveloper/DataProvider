-- Get physicians filtered by department and/or specialty
SELECT
    ph.Id, ph.EmployeeNumber, ph.FirstName, ph.LastName,
    ph.Specialty, ph.LicenseNumber, ph.Email, ph.Phone, ph.IsActive,
    d.Id AS DepartmentId, d.Name AS DepartmentName, d.Code AS DepartmentCode
FROM Physician ph
LEFT JOIN Department d ON d.Id = ph.DepartmentId
WHERE (@departmentId IS NULL OR ph.DepartmentId = @departmentId)
  AND (@specialty IS NULL OR ph.Specialty = @specialty)
  AND (@isActive IS NULL OR ph.IsActive = @isActive)
ORDER BY ph.LastName, ph.FirstName
