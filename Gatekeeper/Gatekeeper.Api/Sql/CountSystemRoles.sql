-- name: CountSystemRoles
SELECT COUNT(*) as cnt FROM gk_role WHERE is_system = 1;
