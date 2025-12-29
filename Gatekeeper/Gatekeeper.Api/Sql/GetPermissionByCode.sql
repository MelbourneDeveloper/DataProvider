-- name: GetPermissionByCode
SELECT id, code, resource_type, action, description, created_at
FROM gk_permission
WHERE code = @code;
