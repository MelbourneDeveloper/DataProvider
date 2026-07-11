SELECT 
    Id,
    Name,
    Email,
    CreatedDate
FROM Users
WHERE Id = @userId 