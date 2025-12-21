-- Gets the revocation status of a session
-- @jti: The session ID (JWT ID) to check
SELECT is_revoked FROM gk_session WHERE id = @jti;
