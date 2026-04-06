SELECT users.id, users.name, users.email
FROM users
WHERE users.status = 'active' AND users.age > 18 AND users.country = 'US'