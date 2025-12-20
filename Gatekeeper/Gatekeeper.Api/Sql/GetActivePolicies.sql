-- name: GetActivePolicies
SELECT id, name, description, resource_type, action, condition, effect, priority
FROM gk_policy
WHERE is_active = 1
  AND (resource_type = @resource_type OR resource_type = '*')
  AND (action = @action OR action = '*')
ORDER BY priority DESC;
