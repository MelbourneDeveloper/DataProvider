SELECT
    users.name,
    orders.total
FROM users u
LEFT JOIN orders o ON users.id = orders.user_id