-- name: CheckResourceGrant
SELECT rg.id, rg.user_id, rg.resource_type, rg.resource_id, rg.permission_id,
       rg.granted_at, rg.granted_by, rg.expires_at, p.code as permission_code
FROM gk_resource_grant rg
JOIN gk_permission p ON rg.permission_id = p.id
WHERE rg.user_id = @user_id
  AND rg.resource_type = @resource_type
  AND rg.resource_id = @resource_id
  AND p.code = @permission_code
  AND (rg.expires_at IS NULL OR rg.expires_at > @now);
