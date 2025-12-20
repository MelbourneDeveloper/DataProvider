namespace Gatekeeper.Api.Services;

/// <summary>
/// Service for session management.
/// </summary>
public static class SessionService
{
    /// <summary>
    /// Session information returned to clients.
    /// </summary>
    public sealed record SessionInfo(
        string UserId,
        string DisplayName,
        string? Email,
        ImmutableArray<string> Roles,
        DateTime ExpiresAt);

    /// <summary>
    /// Creates a new session for an authenticated user.
    /// </summary>
    public static async Task<Result<(string Token, SessionInfo Info), string>> CreateSessionAsync(
        SqliteConnection conn,
        string userId,
        string credentialId,
        string? ipAddress,
        string? userAgent,
        TimeSpan sessionDuration,
        ILogger logger)
    {
        try
        {
            var now = DateTime.UtcNow;
            var nowStr = now.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
            var expiresAt = now.Add(sessionDuration);
            var expiresAtStr = expiresAt.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);

            // Get user info
            var userResult = await conn.GetUserByIdAsync(userId).ConfigureAwait(false);
            if (userResult is not GetUserByIdOk(var users) || users.Count == 0)
            {
                return new Result<(string Token, SessionInfo Info), string>.Error("User not found");
            }

            var user = users[0];

            // Get user roles
            var rolesResult = await conn.GetUserRolesAsync(userId, nowStr).ConfigureAwait(false);
            var roles = rolesResult is GetUserRolesOk(var roleList)
                ? roleList.Select(r => r.Name).ToImmutableArray()
                : ImmutableArray<string>.Empty;

            // Generate session token (URL-safe base64)
            var sessionId = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
                .Replace("+", "-")
                .Replace("/", "_")
                .TrimEnd('=');

            // Insert session
            var insertResult = await conn.InsertSessionAsync(
                sessionId,
                userId,
                credentialId,
                nowStr,
                expiresAtStr,
                nowStr,
                ipAddress ?? (object)DBNull.Value,
                userAgent ?? (object)DBNull.Value).ConfigureAwait(false);

            if (insertResult is InsertSessionError(var err))
            {
                logger.Log(LogLevel.Error, "Failed to create session: {Message}", err.Message);
                return new Result<(string Token, SessionInfo Info), string>.Error("Failed to create session");
            }

            var sessionInfo = new SessionInfo(
                userId,
                user.DisplayName,
                user.Email,
                roles,
                expiresAt);

            logger.Log(LogLevel.Information, "Created session for user {UserId}", userId);
            return new Result<(string Token, SessionInfo Info), string>.Ok((sessionId, sessionInfo));
        }
        catch (Exception ex)
        {
            logger.Log(LogLevel.Error, ex, "Error creating session");
            return new Result<(string Token, SessionInfo Info), string>.Error(ex.Message);
        }
    }

    /// <summary>
    /// Validates a session token and returns session info.
    /// </summary>
    public static async Task<Result<SessionInfo, string>> ValidateSessionAsync(
        SqliteConnection conn,
        string token,
        ILogger logger)
    {
        try
        {
            var now = DateTime.UtcNow;
            var nowStr = now.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);

            // Get session
            var sessionResult = await conn.GetSessionByIdAsync(token).ConfigureAwait(false);
            if (sessionResult is not GetSessionByIdOk(var sessions) || sessions.Count == 0)
            {
                return new Result<SessionInfo, string>.Error("Session not found");
            }

            var session = sessions[0];

            // Check if expired
            if (DateTime.Parse(session.ExpiresAt, CultureInfo.InvariantCulture) < now)
            {
                return new Result<SessionInfo, string>.Error("Session expired");
            }

            // Check if revoked
            if (session.IsRevoked != 0)
            {
                return new Result<SessionInfo, string>.Error("Session revoked");
            }

            // Get user roles
            var rolesResult = await conn.GetUserRolesAsync(session.UserId, nowStr).ConfigureAwait(false);
            var roles = rolesResult is GetUserRolesOk(var roleList)
                ? roleList.Select(r => r.Name).ToImmutableArray()
                : ImmutableArray<string>.Empty;

            var sessionInfo = new SessionInfo(
                session.UserId,
                session.DisplayName,
                session.Email,
                roles,
                DateTime.Parse(session.ExpiresAt, CultureInfo.InvariantCulture));

            return new Result<SessionInfo, string>.Ok(sessionInfo);
        }
        catch (Exception ex)
        {
            logger.Log(LogLevel.Error, ex, "Error validating session");
            return new Result<SessionInfo, string>.Error(ex.Message);
        }
    }

    /// <summary>
    /// Revokes a session (logout).
    /// </summary>
    public static async Task<Result<bool, string>> RevokeSessionAsync(
        SqliteConnection conn,
        string token,
        ILogger logger)
    {
        try
        {
            var result = await conn.RevokeSessionAsync(token).ConfigureAwait(false);
            if (result is RevokeSessionError(var err))
            {
                logger.Log(LogLevel.Error, "Failed to revoke session: {Message}", err.Message);
                return new Result<bool, string>.Error("Failed to revoke session");
            }

            logger.Log(LogLevel.Information, "Revoked session");
            return new Result<bool, string>.Ok(true);
        }
        catch (Exception ex)
        {
            logger.Log(LogLevel.Error, ex, "Error revoking session");
            return new Result<bool, string>.Error(ex.Message);
        }
    }

    /// <summary>
    /// Extracts bearer token from Authorization header.
    /// </summary>
    public static string? ExtractBearerToken(string? authorizationHeader) =>
        string.IsNullOrEmpty(authorizationHeader) || !authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? null
            : authorizationHeader["Bearer ".Length..].Trim();
}
