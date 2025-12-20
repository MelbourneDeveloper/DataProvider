-- name: GetAllUsers
SELECT id, display_name, email, created_at, last_login_at, is_active
FROM gk_user
ORDER BY display_name;
