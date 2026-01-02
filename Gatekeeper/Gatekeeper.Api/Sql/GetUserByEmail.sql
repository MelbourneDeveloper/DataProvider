-- name: GetUserByEmail
SELECT id, display_name, email, created_at, last_login_at, is_active, metadata
FROM gk_user
WHERE email = @email AND is_active = 1;
