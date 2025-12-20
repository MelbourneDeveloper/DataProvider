-- name: GetUserById
SELECT id, display_name, email, created_at, last_login_at, is_active, metadata
FROM gk_user
WHERE id = @id;
