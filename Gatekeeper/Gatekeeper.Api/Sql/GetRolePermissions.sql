-- name: GetRolePermissions
SELECT p.id, p.code, p.resource_type, p.action, p.description, p.created_at,
       rp.granted_at
FROM gk_permission p
JOIN gk_role_permission rp ON p.id = rp.permission_id
WHERE rp.role_id = @roleId;
