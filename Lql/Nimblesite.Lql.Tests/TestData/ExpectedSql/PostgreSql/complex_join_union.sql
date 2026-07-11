INSERT INTO report_table (id, name)
(SELECT
    u.id,
    u.name
FROM users u
INNER JOIN orders o ON u.id = o.user_id
WHERE o.status = 'completed'
UNION
SELECT a.archived_users.id, a.archived_users.name
FROM archived_users a)