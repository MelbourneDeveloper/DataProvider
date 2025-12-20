-- name: GetUserPermissions
-- Returns all permissions for a user: from roles + direct grants
SELECT DISTINCT p.id, p.code, p.resource_type, p.action, p.description,
       'role' as source, r.name as source_name, NULL as scope_type, NULL as scope_value
FROM gk_user_role ur
JOIN gk_role r ON ur.role_id = r.id
JOIN gk_role_permission rp ON r.id = rp.role_id
JOIN gk_permission p ON rp.permission_id = p.id
WHERE ur.user_id = @user_id
  AND (ur.expires_at IS NULL OR ur.expires_at > @now)

UNION ALL

SELECT p.id, p.code, p.resource_type, p.action, p.description,
       'direct' as source, NULL as source_name, up.scope_type, up.scope_value
FROM gk_user_permission up
JOIN gk_permission p ON up.permission_id = p.id
WHERE up.user_id = @user_id
  AND (up.expires_at IS NULL OR up.expires_at > @now);
