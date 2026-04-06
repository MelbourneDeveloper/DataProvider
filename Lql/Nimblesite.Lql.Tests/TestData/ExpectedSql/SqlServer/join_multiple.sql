SELECT users.name, orders.total, products.name
FROM users u
INNER JOIN orders o ON users.id = orders.user_id
INNER JOIN products p ON orders.product_id = products.id