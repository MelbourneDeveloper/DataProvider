INSERT INTO report_table (users.id, users.name)
SELECT users.id, users.name
FROM users u
INNER JOIN orders o ON users.id = orders.user_id
WHERE orders.status = 'completed'

UNION

SELECT a.archived_users.id, a.archived_users.name
FROM archived_users a