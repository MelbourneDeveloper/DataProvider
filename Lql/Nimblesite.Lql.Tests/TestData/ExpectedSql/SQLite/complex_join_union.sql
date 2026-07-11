INSERT INTO report_table (id, name)
SELECT users.id, users.name FROM users INNER JOIN orders ON users.id = orders.user_id WHERE orders.status = 'completed'