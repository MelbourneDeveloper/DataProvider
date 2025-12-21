-- name: GetAllPermissions
SELECT id, code, resource_type, action, description, created_at
FROM gk_permission
ORDER BY resource_type, action;
