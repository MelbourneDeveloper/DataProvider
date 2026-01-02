-- Gets a session for revocation (no filters)
-- @jti: The session ID (JWT ID) to get
SELECT id, user_id, credential_id, created_at, expires_at, last_activity_at,
       ip_address, user_agent, is_revoked
FROM gk_session
WHERE id = @jti;
