SELECT 
    u.Id,
    u.Name,
    u.Email,
    p.Title,
    p.Description
FROM Users u
INNER JOIN Posts p ON u.Id = p.UserId
WHERE u.Id = @userId 