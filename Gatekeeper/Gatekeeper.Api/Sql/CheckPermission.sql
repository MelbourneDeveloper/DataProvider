-- name: CheckPermission
-- Checks if user has a specific permission code (via roles or direct grant)
SELECT 1 AS has_permission
FROM gk_permission p
WHERE p.code = @permissionCode
  AND (
    -- Check role permissions
    EXISTS (
      SELECT 1 FROM gk_role_permission rp
      JOIN gk_user_role ur ON rp.role_id = ur.role_id
      WHERE rp.permission_id = p.id
        AND ur.user_id = @userId
        AND (ur.expires_at IS NULL OR ur.expires_at > @now)
    )
    OR
    -- Check direct permissions
    EXISTS (
      SELECT 1 FROM gk_user_permission up
      WHERE up.permission_id = p.id
        AND up.user_id = @userId
        AND (up.expires_at IS NULL OR up.expires_at > @now)
    )
  )
LIMIT 1;
