-- Revokes a session by setting is_revoked = true
-- @jti: The session ID (JWT ID) to revoke
UPDATE gk_session SET is_revoked = true WHERE id = @jti RETURNING id, is_revoked;
