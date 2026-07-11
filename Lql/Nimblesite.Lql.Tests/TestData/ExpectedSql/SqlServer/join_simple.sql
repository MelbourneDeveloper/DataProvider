SELECT users.name, orders.total
FROM users u
INNER JOIN orders o ON users.id = orders.user_id