-- Search patients by name, MRN, or date of birth
SELECT
    Id, MedicalRecordNumber, FirstName, LastName, DateOfBirth,
    Gender, BloodType, Phone, Email, IsActive
FROM Patient
WHERE (@searchTerm IS NULL OR
       FirstName LIKE '%' || @searchTerm || '%' OR
       LastName LIKE '%' || @searchTerm || '%' OR
       MedicalRecordNumber LIKE '%' || @searchTerm || '%')
  AND (@isActive IS NULL OR IsActive = @isActive)
ORDER BY LastName, FirstName
LIMIT @limit OFFSET @offset
