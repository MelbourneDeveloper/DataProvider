-- name: GetAllRoles
SELECT id, name, description, is_system, created_at, parent_role_id
FROM gk_role
ORDER BY name;
