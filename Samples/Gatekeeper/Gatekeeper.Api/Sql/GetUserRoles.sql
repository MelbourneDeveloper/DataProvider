-- name: GetUserRoles
SELECT r.id, r.name, r.description, r.is_system, ur.granted_at, ur.expires_at
FROM gk_user_role ur
JOIN gk_role r ON ur.role_id = r.id
WHERE ur.user_id = @user_id
  AND (ur.expires_at IS NULL OR ur.expires_at > @now);
