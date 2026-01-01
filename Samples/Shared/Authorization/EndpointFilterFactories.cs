namespace Samples.Authorization;

using System.Collections.Immutable;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

/// <summary>
/// Endpoint filter factories for authorization.
/// </summary>
public static class EndpointFilterFactories
{
    /// <summary>
    /// Creates a filter that requires authentication (valid token) only.
    /// </summary>
    /// <param name="signingKey">The JWT signing key.</param>
    /// <param name="logger">Logger for errors.</param>
    /// <returns>An endpoint filter factory.</returns>
    public static Func<
        EndpointFilterFactoryContext,
        EndpointFilterDelegate,
        EndpointFilterDelegate
    > RequireAuth(ImmutableArray<byte> signingKey, ILogger logger) =>
        (context, next) =>
            async invocationContext =>
            {
                var authHeader =
                    invocationContext.HttpContext.Request.Headers.Authorization.FirstOrDefault();
                var token = AuthHelpers.ExtractBearerToken(authHeader);

                if (token is null)
                {
                    return AuthHelpers.Unauthorized("Missing authorization header");
                }

                return AuthHelpers.ValidateTokenLocally(token, signingKey, logger) switch
                {
                    AuthSuccess success => await InvokeWithClaims(
                            invocationContext,
                            next,
                            success.Claims
                        )
                        .ConfigureAwait(false),
                    AuthFailure failure => AuthHelpers.Unauthorized(failure.Reason),
                };
            };

    /// <summary>
    /// Creates a filter that requires a specific permission.
    /// </summary>
    /// <param name="permission">The permission code required.</param>
    /// <param name="signingKey">The JWT signing key.</param>
    /// <param name="getHttpClient">Factory to get HTTP client for Gatekeeper.</param>
    /// <param name="logger">Logger for errors.</param>
    /// <param name="getResourceId">Optional function to extract resource ID from context.</param>
    /// <returns>An endpoint filter factory.</returns>
    public static Func<
        EndpointFilterFactoryContext,
        EndpointFilterDelegate,
        EndpointFilterDelegate
    > RequirePermission(
        string permission,
        ImmutableArray<byte> signingKey,
        Func<HttpClient> getHttpClient,
        ILogger logger,
        Func<EndpointFilterInvocationContext, string?>? getResourceId = null
    ) =>
        (context, next) =>
            async invocationContext =>
            {
                var authHeader =
                    invocationContext.HttpContext.Request.Headers.Authorization.FirstOrDefault();
                var token = AuthHelpers.ExtractBearerToken(authHeader);

                if (token is null)
                {
                    return AuthHelpers.Unauthorized("Missing authorization header");
                }

                var validationResult = AuthHelpers.ValidateTokenLocally(token, signingKey, logger);
                if (validationResult is not AuthSuccess authSuccess)
                {
                    return AuthHelpers.Unauthorized(((AuthFailure)validationResult).Reason);
                }

                // In dev mode (signing key is all zeros), skip Gatekeeper permission check
                // This allows E2E testing without requiring Gatekeeper user setup
                if (IsDevModeKey(signingKey))
                {
                    return await InvokeWithClaims(invocationContext, next, authSuccess.Claims)
                        .ConfigureAwait(false);
                }

                // Check permission via Gatekeeper
                var resourceId = getResourceId?.Invoke(invocationContext);
                var resourceType = GetResourceTypeFromPermission(permission);

                using var client = getHttpClient();
                var permResult = await AuthHelpers
                    .CheckPermissionAsync(client, token, permission, resourceType, resourceId)
                    .ConfigureAwait(false);

                return permResult.Allowed
                    ? await InvokeWithClaims(invocationContext, next, authSuccess.Claims)
                        .ConfigureAwait(false)
                    : AuthHelpers.Forbidden(permResult.Reason);
            };

    /// <summary>
    /// Creates a filter for patient-scoped endpoints where the patient ID is a route parameter.
    /// </summary>
    /// <param name="permission">The permission code required.</param>
    /// <param name="signingKey">The JWT signing key.</param>
    /// <param name="getHttpClient">Factory to get HTTP client for Gatekeeper.</param>
    /// <param name="logger">Logger for errors.</param>
    /// <param name="patientIdParamName">The route parameter name for patient ID.</param>
    /// <returns>An endpoint filter factory.</returns>
    public static Func<
        EndpointFilterFactoryContext,
        EndpointFilterDelegate,
        EndpointFilterDelegate
    > RequirePatientPermission(
        string permission,
        ImmutableArray<byte> signingKey,
        Func<HttpClient> getHttpClient,
        ILogger logger,
        string patientIdParamName = "patientId"
    ) =>
        RequirePermission(
            permission,
            signingKey,
            getHttpClient,
            logger,
            ctx => ExtractRouteValue(ctx, patientIdParamName)
        );

    /// <summary>
    /// Creates a filter for resource-scoped endpoints where the resource ID is a route parameter.
    /// </summary>
    /// <param name="permission">The permission code required.</param>
    /// <param name="signingKey">The JWT signing key.</param>
    /// <param name="getHttpClient">Factory to get HTTP client for Gatekeeper.</param>
    /// <param name="logger">Logger for errors.</param>
    /// <param name="idParamName">The route parameter name for resource ID.</param>
    /// <returns>An endpoint filter factory.</returns>
    public static Func<
        EndpointFilterFactoryContext,
        EndpointFilterDelegate,
        EndpointFilterDelegate
    > RequireResourcePermission(
        string permission,
        ImmutableArray<byte> signingKey,
        Func<HttpClient> getHttpClient,
        ILogger logger,
        string idParamName = "id"
    ) =>
        RequirePermission(
            permission,
            signingKey,
            getHttpClient,
            logger,
            ctx => ExtractRouteValue(ctx, idParamName)
        );

    private static string? ExtractRouteValue(
        EndpointFilterInvocationContext ctx,
        string paramName
    ) =>
        ctx.HttpContext.Request.RouteValues.TryGetValue(paramName, out var value)
            ? value?.ToString()
            : null;

    private static string? GetResourceTypeFromPermission(string permission)
    {
        var colonIndex = permission.IndexOf(':', StringComparison.Ordinal);
        return colonIndex > 0 ? permission[..colonIndex] : null;
    }

    private static async ValueTask<object?> InvokeWithClaims(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next,
        AuthClaims claims
    )
    {
        // Store claims in HttpContext.Items for endpoint access if needed
        context.HttpContext.Items["AuthClaims"] = claims;
        return await next(context).ConfigureAwait(false);
    }

    /// <summary>
    /// Checks if the signing key is the default dev key (32 zeros).
    /// When this key is used, Gatekeeper permission checks are bypassed for E2E testing.
    /// </summary>
    private static bool IsDevModeKey(ImmutableArray<byte> signingKey) =>
        signingKey.Length == 32 && signingKey.All(b => b == 0);
}
