-- name: GetUserPermissions
-- Returns all permissions for a user: from roles + direct grants
-- Note: source_type column uses role name prefix to indicate source (role-based vs direct)
SELECT DISTINCT p.id, p.code, p.resource_type, p.action, p.description,
       r.name as source_name,
       ur.role_id as source_type,
       p.resource_type as scope_type,
       p.action as scope_value
FROM gk_user_role ur
JOIN gk_role r ON ur.role_id = r.id
JOIN gk_role_permission rp ON r.id = rp.role_id
JOIN gk_permission p ON rp.permission_id = p.id
WHERE ur.user_id = @user_id
  AND (ur.expires_at IS NULL OR ur.expires_at > @now)

UNION ALL

SELECT p.id, p.code, p.resource_type, p.action, p.description,
       p.code as source_name,
       up.permission_id as source_type,
       COALESCE(up.scope_type, p.resource_type) as scope_type,
       COALESCE(up.scope_value, p.action) as scope_value
FROM gk_user_permission up
JOIN gk_permission p ON up.permission_id = p.id
WHERE up.user_id = @user_id
  AND (up.expires_at IS NULL OR up.expires_at > @now);
