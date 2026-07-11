SELECT 
    u.Id,
    u.Name,
    u.Email,
    p.Title,
    p.Description,
    c.Content,
    c.CreatedDate as CommentDate
FROM Users u
INNER JOIN Posts p ON u.Id = p.UserId
LEFT JOIN Comments c ON p.Id = c.PostId
WHERE u.Id = @userId
ORDER BY p.CreatedDate DESC, c.CreatedDate ASC 