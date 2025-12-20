namespace Gatekeeper.Api.Services;

/// <summary>
/// Service for WebAuthn/FIDO2 passkey operations using fido2-net-lib.
/// </summary>
public static class PasskeyService
{
    /// <summary>
    /// Result type for registration begin operation.
    /// </summary>
    public sealed record RegisterBeginResult(
        string ChallengeId,
        CredentialCreateOptions Options);

    /// <summary>
    /// Result type for registration complete operation.
    /// </summary>
    public sealed record RegisterCompleteResult(
        string UserId,
        string CredentialId);

    /// <summary>
    /// Result type for login begin operation.
    /// </summary>
    public sealed record LoginBeginResult(
        string ChallengeId,
        AssertionOptions Options);

    /// <summary>
    /// Result type for login complete operation.
    /// </summary>
    public sealed record LoginCompleteResult(
        string UserId,
        string CredentialId);

    /// <summary>
    /// Begins passkey registration for a new or existing user.
    /// </summary>
    public static Result<RegisterBeginResult, string> BeginRegistration(
        SqliteConnection conn,
        IFido2 fido2,
        string email,
        string displayName,
        ILogger logger)
    {
        try
        {
            var now = DateTime.UtcNow;
            var nowStr = now.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
            var expiresAt = now.AddMinutes(5).ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);

            // Check if user exists
            var existingUserResult = conn.GetUserByEmailAsync(email).GetAwaiter().GetResult();
            string userId;
            ImmutableArray<byte[]> existingCredentials = [];

            if (existingUserResult is GetUserByEmailOk(var users) && users.Count > 0)
            {
                // Existing user - get their credentials to exclude
                userId = users[0].Id;
                var credsResult = conn.GetCredentialsByUserIdAsync(userId).GetAwaiter().GetResult();
                if (credsResult is GetCredentialsByUserIdOk(var creds))
                {
                    existingCredentials = [.. creds.Select(c => Convert.FromBase64String(c.Id))];
                }
            }
            else
            {
                // New user - create them
                userId = Guid.NewGuid().ToString();
                var insertResult = conn.InsertUserAsync(userId, displayName, email, nowStr, DBNull.Value)
                    .GetAwaiter().GetResult();
                if (insertResult is InsertUserError(var err))
                {
                    logger.Log(LogLevel.Error, "Failed to create user: {Message}", err.Message);
                    return new Result<RegisterBeginResult, string>.Error("Failed to create user");
                }
            }

            // Create FIDO2 user
            var fido2User = new Fido2User
            {
                Id = System.Text.Encoding.UTF8.GetBytes(userId),
                Name = email,
                DisplayName = displayName
            };

            // Exclude existing credentials
            var excludeCredentials = existingCredentials
                .Select(id => new PublicKeyCredentialDescriptor(id))
                .ToList();

            // Create registration options
            var options = fido2.RequestNewCredential(
                fido2User,
                excludeCredentials,
                AuthenticatorSelection.Default,
                AttestationConveyancePreference.None);

            // Store challenge
            var challengeId = Guid.NewGuid().ToString();
            var challengeBytes = options.Challenge;
            var insertChallengeResult = conn.InsertChallengeAsync(
                challengeId,
                userId,
                challengeBytes,
                "registration",
                nowStr,
                expiresAt).GetAwaiter().GetResult();

            if (insertChallengeResult is InsertChallengeError(var challengeErr))
            {
                logger.Log(LogLevel.Error, "Failed to store challenge: {Message}", challengeErr.Message);
                return new Result<RegisterBeginResult, string>.Error("Failed to store challenge");
            }

            logger.Log(LogLevel.Information, "Started passkey registration for {Email}", email);
            return new Result<RegisterBeginResult, string>.Ok(new RegisterBeginResult(challengeId, options));
        }
        catch (Exception ex)
        {
            logger.Log(LogLevel.Error, ex, "Error beginning registration");
            return new Result<RegisterBeginResult, string>.Error(ex.Message);
        }
    }

    /// <summary>
    /// Completes passkey registration with authenticator response.
    /// </summary>
    public static async Task<Result<RegisterCompleteResult, string>> CompleteRegistrationAsync(
        SqliteConnection conn,
        IFido2 fido2,
        string challengeId,
        AuthenticatorAttestationRawResponse attestationResponse,
        string? deviceName,
        ILogger logger)
    {
        try
        {
            var now = DateTime.UtcNow;
            var nowStr = now.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);

            // Get and validate challenge
            var challengeResult = await conn.GetChallengeByIdAsync(challengeId).ConfigureAwait(false);
            if (challengeResult is not GetChallengeByIdOk(var challenges) || challenges.Count == 0)
            {
                return new Result<RegisterCompleteResult, string>.Error("Challenge not found");
            }

            var challenge = challenges[0];
            if (challenge.Type != "registration")
            {
                return new Result<RegisterCompleteResult, string>.Error("Invalid challenge type");
            }

            if (DateTime.Parse(challenge.ExpiresAt, CultureInfo.InvariantCulture) < now)
            {
                await conn.DeleteChallengeAsync(challengeId).ConfigureAwait(false);
                return new Result<RegisterCompleteResult, string>.Error("Challenge expired");
            }

            // Get user for the challenge
            var userId = challenge.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                return new Result<RegisterCompleteResult, string>.Error("Challenge has no associated user");
            }

            // Recreate options from stored challenge
            var userResult = await conn.GetUserByIdAsync(userId).ConfigureAwait(false);
            if (userResult is not GetUserByIdOk(var userList) || userList.Count == 0)
            {
                return new Result<RegisterCompleteResult, string>.Error("User not found");
            }

            var user = userList[0];
            var fido2User = new Fido2User
            {
                Id = System.Text.Encoding.UTF8.GetBytes(userId),
                Name = user.Email ?? "",
                DisplayName = user.DisplayName
            };

            // Verify the attestation
            var options = CredentialCreateOptions.FromJson(JsonSerializer.Serialize(new
            {
                challenge = Convert.ToBase64String(challenge.Challenge),
                rp = new { id = fido2.Config.ServerDomain, name = fido2.Config.ServerName },
                user = new { id = Convert.ToBase64String(fido2User.Id), name = fido2User.Name, displayName = fido2User.DisplayName },
                pubKeyCredParams = new[] { new { type = "public-key", alg = -7 }, new { type = "public-key", alg = -257 } },
                timeout = 60000,
                attestation = "none",
                authenticatorSelection = new { residentKey = "preferred", userVerification = "preferred" }
            }));

            var credentialMakeResult = await fido2.MakeNewCredentialAsync(
                attestationResponse,
                options,
                async (args, _) =>
                {
                    // Check credential doesn't already exist
                    var existingCred = await conn.GetCredentialByIdAsync(
                        Convert.ToBase64String(args.CredentialId)).ConfigureAwait(false);
                    return existingCred is not GetCredentialByIdOk(var existing) || existing.Count == 0;
                }).ConfigureAwait(false);

            if (credentialMakeResult.Result is null)
            {
                return new Result<RegisterCompleteResult, string>.Error("Failed to make credential");
            }

            var credential = credentialMakeResult.Result;

            // Store the credential
            var credentialId = Convert.ToBase64String(credential.Id);
            var insertCredResult = await conn.InsertCredentialAsync(
                credentialId,
                userId,
                credential.PublicKey,
                (long)credential.SignCount,
                credential.AaGuid.ToString(),
                credential.Type.ToString(),
                credential.Transports != null
                    ? JsonSerializer.Serialize(credential.Transports.Select(t => t.ToString()))
                    : DBNull.Value,
                credential.AttestationFormat,
                nowStr,
                deviceName ?? "Unknown Device",
                credential.IsBackupEligible ? 1L : 0L,
                credential.IsBackedUp ? 1L : 0L).ConfigureAwait(false);

            if (insertCredResult is InsertCredentialError(var credErr))
            {
                logger.Log(LogLevel.Error, "Failed to store credential: {Message}", credErr.Message);
                return new Result<RegisterCompleteResult, string>.Error("Failed to store credential");
            }

            // Delete the used challenge
            await conn.DeleteChallengeAsync(challengeId).ConfigureAwait(false);

            // Assign default 'user' role
            await AssignDefaultRoleAsync(conn, userId, nowStr, logger).ConfigureAwait(false);

            logger.Log(LogLevel.Information, "Completed passkey registration for user {UserId}", userId);
            return new Result<RegisterCompleteResult, string>.Ok(new RegisterCompleteResult(userId, credentialId));
        }
        catch (Exception ex)
        {
            logger.Log(LogLevel.Error, ex, "Error completing registration");
            return new Result<RegisterCompleteResult, string>.Error(ex.Message);
        }
    }

    /// <summary>
    /// Begins passkey authentication.
    /// </summary>
    public static Result<LoginBeginResult, string> BeginLogin(
        SqliteConnection conn,
        IFido2 fido2,
        string? email,
        ILogger logger)
    {
        try
        {
            var now = DateTime.UtcNow;
            var nowStr = now.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
            var expiresAt = now.AddMinutes(5).ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);

            ImmutableArray<PublicKeyCredentialDescriptor> allowedCredentials = [];
            string? userId = null;

            // If email provided, get user's credentials
            if (!string.IsNullOrEmpty(email))
            {
                var userResult = conn.GetUserByEmailAsync(email).GetAwaiter().GetResult();
                if (userResult is GetUserByEmailOk(var users) && users.Count > 0)
                {
                    userId = users[0].Id;
                    var credsResult = conn.GetCredentialsByUserIdAsync(userId).GetAwaiter().GetResult();
                    if (credsResult is GetCredentialsByUserIdOk(var creds))
                    {
                        allowedCredentials = [.. creds.Select(c =>
                            new PublicKeyCredentialDescriptor(Convert.FromBase64String(c.Id)))];
                    }
                }
            }

            // Create assertion options
            var options = fido2.GetAssertionOptions(
                allowedCredentials.ToList(),
                UserVerificationRequirement.Required);

            // Store challenge
            var challengeId = Guid.NewGuid().ToString();
            var insertChallengeResult = conn.InsertChallengeAsync(
                challengeId,
                userId ?? (object)DBNull.Value,
                options.Challenge,
                "authentication",
                nowStr,
                expiresAt).GetAwaiter().GetResult();

            if (insertChallengeResult is InsertChallengeError(var challengeErr))
            {
                logger.Log(LogLevel.Error, "Failed to store challenge: {Message}", challengeErr.Message);
                return new Result<LoginBeginResult, string>.Error("Failed to store challenge");
            }

            logger.Log(LogLevel.Information, "Started passkey login{Email}",
                string.IsNullOrEmpty(email) ? "" : $" for {email}");
            return new Result<LoginBeginResult, string>.Ok(new LoginBeginResult(challengeId, options));
        }
        catch (Exception ex)
        {
            logger.Log(LogLevel.Error, ex, "Error beginning login");
            return new Result<LoginBeginResult, string>.Error(ex.Message);
        }
    }

    /// <summary>
    /// Completes passkey authentication with authenticator response.
    /// </summary>
    public static async Task<Result<LoginCompleteResult, string>> CompleteLoginAsync(
        SqliteConnection conn,
        IFido2 fido2,
        string challengeId,
        AuthenticatorAssertionRawResponse assertionResponse,
        ILogger logger)
    {
        try
        {
            var now = DateTime.UtcNow;
            var nowStr = now.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);

            // Get and validate challenge
            var challengeResult = await conn.GetChallengeByIdAsync(challengeId).ConfigureAwait(false);
            if (challengeResult is not GetChallengeByIdOk(var challenges) || challenges.Count == 0)
            {
                return new Result<LoginCompleteResult, string>.Error("Challenge not found");
            }

            var challenge = challenges[0];
            if (challenge.Type != "authentication")
            {
                return new Result<LoginCompleteResult, string>.Error("Invalid challenge type");
            }

            if (DateTime.Parse(challenge.ExpiresAt, CultureInfo.InvariantCulture) < now)
            {
                await conn.DeleteChallengeAsync(challengeId).ConfigureAwait(false);
                return new Result<LoginCompleteResult, string>.Error("Challenge expired");
            }

            // Get the credential
            var credentialId = Convert.ToBase64String(assertionResponse.Id);
            var credResult = await conn.GetCredentialByIdAsync(credentialId).ConfigureAwait(false);
            if (credResult is not GetCredentialByIdOk(var credentials) || credentials.Count == 0)
            {
                return new Result<LoginCompleteResult, string>.Error("Credential not found");
            }

            var storedCredential = credentials[0];

            // Recreate assertion options from stored challenge
            var options = AssertionOptions.FromJson(JsonSerializer.Serialize(new
            {
                challenge = Convert.ToBase64String(challenge.Challenge),
                timeout = 60000,
                rpId = fido2.Config.ServerDomain,
                allowCredentials = Array.Empty<object>(),
                userVerification = "required"
            }));

            // Verify the assertion
            var assertionResult = await fido2.MakeAssertionAsync(
                assertionResponse,
                options,
                storedCredential.PublicKey,
                [],
                (uint)storedCredential.SignCount,
                async (args, _) =>
                {
                    // Verify user handle matches
                    var userIdFromHandle = System.Text.Encoding.UTF8.GetString(args.UserHandle);
                    return userIdFromHandle == storedCredential.UserId;
                }).ConfigureAwait(false);

            if (assertionResult.Status != "ok")
            {
                return new Result<LoginCompleteResult, string>.Error("Assertion verification failed");
            }

            // Update sign count
            await conn.UpdateCredentialSignCountAsync((long)assertionResult.SignCount, nowStr, credentialId)
                .ConfigureAwait(false);

            // Update user's last login
            await conn.UpdateUserLastLoginAsync(nowStr, storedCredential.UserId).ConfigureAwait(false);

            // Delete the used challenge
            await conn.DeleteChallengeAsync(challengeId).ConfigureAwait(false);

            logger.Log(LogLevel.Information, "Completed passkey login for user {UserId}", storedCredential.UserId);
            return new Result<LoginCompleteResult, string>.Ok(
                new LoginCompleteResult(storedCredential.UserId, credentialId));
        }
        catch (Exception ex)
        {
            logger.Log(LogLevel.Error, ex, "Error completing login");
            return new Result<LoginCompleteResult, string>.Error(ex.Message);
        }
    }

    private static async Task AssignDefaultRoleAsync(
        SqliteConnection conn,
        string userId,
        string grantedAt,
        ILogger logger)
    {
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT OR IGNORE INTO gk_user_role (user_id, role_id, granted_at)
                SELECT @user_id, id, @granted_at FROM gk_role WHERE name = 'user'
                """;
            cmd.Parameters.AddWithValue("@user_id", userId);
            cmd.Parameters.AddWithValue("@granted_at", grantedAt);
            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.Log(LogLevel.Warning, ex, "Failed to assign default role to user {UserId}", userId);
        }
    }
}
