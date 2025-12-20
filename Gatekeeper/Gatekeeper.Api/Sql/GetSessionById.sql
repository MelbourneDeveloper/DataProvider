-- name: GetSessionById
SELECT s.id, s.user_id, s.credential_id, s.created_at, s.expires_at, s.last_activity_at,
       s.ip_address, s.user_agent, s.is_revoked,
       u.display_name, u.email
FROM gk_session s
JOIN gk_user u ON s.user_id = u.id
WHERE s.id = @id AND s.is_revoked = 0 AND s.expires_at > @now AND u.is_active = 1;
