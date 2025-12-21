-- Revokes a session by setting is_revoked = 1
-- @jti: The session ID (JWT ID) to revoke
UPDATE gk_session SET is_revoked = 1 WHERE id = @jti RETURNING id, is_revoked;
