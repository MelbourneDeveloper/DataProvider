using System.Net;
using Microsoft.Playwright;

namespace Dashboard.Integration.Tests;

/// <summary>
/// E2E tests for authentication (login, logout, WebAuthn).
/// </summary>
[Collection("E2E Tests")]
[Trait("Category", "E2E")]
public sealed class AuthE2ETests
{
    private readonly E2EFixture _fixture;

    /// <summary>
    /// Constructor receives shared fixture.
    /// </summary>
    public AuthE2ETests(E2EFixture fixture) => _fixture = fixture;

    /// <summary>
    /// Login page uses discoverable credentials (no email required).
    /// </summary>
    [Fact]
    public async Task LoginPage_DoesNotRequireEmailForSignIn()
    {
        var page = await _fixture.Browser!.NewPageAsync();
        page.Console += (_, msg) => Console.WriteLine($"[BROWSER] {msg.Type}: {msg.Text}");

        await page.GotoAsync(E2EFixture.DashboardUrlNoTestMode);
        await page.EvaluateAsync("() => { localStorage.removeItem('gatekeeper_token'); localStorage.removeItem('gatekeeper_user'); }");
        await page.ReloadAsync();
        await page.WaitForSelectorAsync(".login-card", new PageWaitForSelectorOptions { Timeout = 20000 });

        var pageContent = await page.ContentAsync();
        Assert.Contains("Healthcare Dashboard", pageContent);
        Assert.Contains("Sign in with your passkey", pageContent);

        var emailInputVisible = await page.IsVisibleAsync("input[type='email']");
        Assert.False(emailInputVisible, "Login mode should NOT show email field");

        var signInButton = page.Locator("button:has-text('Sign in with Passkey')");
        await signInButton.WaitForAsync(new LocatorWaitForOptions { Timeout = 5000 });
        Assert.True(await signInButton.IsVisibleAsync());

        await page.CloseAsync();
    }

    /// <summary>
    /// Registration page requires email and display name.
    /// </summary>
    [Fact]
    public async Task LoginPage_RegistrationRequiresEmailAndDisplayName()
    {
        var page = await _fixture.Browser!.NewPageAsync();
        page.Console += (_, msg) => Console.WriteLine($"[BROWSER] {msg.Type}: {msg.Text}");

        await page.GotoAsync(E2EFixture.DashboardUrlNoTestMode);
        await page.EvaluateAsync("() => { localStorage.removeItem('gatekeeper_token'); localStorage.removeItem('gatekeeper_user'); }");
        await page.ReloadAsync();
        await page.WaitForSelectorAsync(".login-card", new PageWaitForSelectorOptions { Timeout = 20000 });

        await page.ClickAsync("button:has-text('Register')");
        await Task.Delay(500);

        var pageContent = await page.ContentAsync();
        Assert.Contains("Create your account", pageContent);

        var emailInput = page.Locator("input[type='email']");
        var displayNameInput = page.Locator("input#displayName");

        Assert.True(await emailInput.IsVisibleAsync());
        Assert.True(await displayNameInput.IsVisibleAsync());

        await page.CloseAsync();
    }

    /// <summary>
    /// Gatekeeper API /auth/login/begin returns valid response for discoverable credentials.
    /// </summary>
    [Fact]
    public async Task GatekeeperApi_LoginBegin_ReturnsValidDiscoverableCredentialOptions()
    {
        using var client = E2EFixture.CreateAuthenticatedClient();

        var response = await client.PostAsync(
            $"{E2EFixture.GatekeeperUrl}/auth/login/begin",
            new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));

        Assert.True(response.IsSuccessStatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("ChallengeId", out var challengeId));
        Assert.False(string.IsNullOrEmpty(challengeId.GetString()));

        Assert.True(root.TryGetProperty("OptionsJson", out var optionsJson));
        var optionsJsonStr = optionsJson.GetString();
        Assert.False(string.IsNullOrEmpty(optionsJsonStr));

        using var optionsDoc = System.Text.Json.JsonDocument.Parse(optionsJsonStr!);
        var options = optionsDoc.RootElement;
        Assert.True(options.TryGetProperty("challenge", out _));
        Assert.True(options.TryGetProperty("rpId", out _));
    }

    /// <summary>
    /// Gatekeeper API /auth/register/begin returns valid response.
    /// </summary>
    [Fact]
    public async Task GatekeeperApi_RegisterBegin_ReturnsValidOptions()
    {
        using var client = E2EFixture.CreateAuthenticatedClient();

        var response = await client.PostAsync(
            $"{E2EFixture.GatekeeperUrl}/auth/register/begin",
            new StringContent(
                """{"Email": "test-e2e@example.com", "DisplayName": "E2E Test User"}""",
                System.Text.Encoding.UTF8, "application/json"));

        Assert.True(response.IsSuccessStatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("ChallengeId", out _));
        Assert.True(root.TryGetProperty("OptionsJson", out var optionsJson));

        using var optionsDoc = System.Text.Json.JsonDocument.Parse(optionsJson.GetString()!);
        var options = optionsDoc.RootElement;
        Assert.True(options.TryGetProperty("challenge", out _));
        Assert.True(options.TryGetProperty("rp", out _));
        Assert.True(options.TryGetProperty("user", out _));
    }

    /// <summary>
    /// Dashboard sign-in flow calls API and handles response correctly.
    /// </summary>
    [Fact]
    public async Task LoginPage_SignInButton_CallsApiWithoutJsonErrors()
    {
        var page = await _fixture.Browser!.NewPageAsync();
        var consoleErrors = new List<string>();
        var networkRequests = new List<string>();

        page.Console += (_, msg) =>
        {
            Console.WriteLine($"[BROWSER] {msg.Type}: {msg.Text}");
            if (msg.Type == "error")
                consoleErrors.Add(msg.Text);
        };

        page.Request += (_, request) =>
        {
            if (request.Url.Contains("/auth/"))
                networkRequests.Add($"{request.Method} {request.Url}");
        };

        await page.GotoAsync(E2EFixture.DashboardUrlNoTestMode);
        await page.EvaluateAsync("() => { localStorage.removeItem('gatekeeper_token'); localStorage.removeItem('gatekeeper_user'); }");
        await page.ReloadAsync();
        await page.WaitForSelectorAsync(".login-card", new PageWaitForSelectorOptions { Timeout = 20000 });

        await page.ClickAsync("button:has-text('Sign in with Passkey')");
        await Task.Delay(3000);

        Assert.Contains(networkRequests, r => r.Contains("/auth/login/begin"));

        var hasJsonParseError = consoleErrors.Any(e =>
            e.Contains("undefined") || e.Contains("is not valid JSON") || e.Contains("SyntaxError"));
        Assert.False(hasJsonParseError);

        await page.CloseAsync();
    }

    /// <summary>
    /// User menu click shows dropdown with Sign Out.
    /// </summary>
    [Fact]
    public async Task UserMenu_ClickShowsDropdownWithSignOut()
    {
        var page = await _fixture.Browser!.NewPageAsync();
        page.Console += (_, msg) => Console.WriteLine($"[BROWSER] {msg.Type}: {msg.Text}");

        await page.GotoAsync(E2EFixture.DashboardUrl);
        await page.WaitForSelectorAsync(".sidebar", new PageWaitForSelectorOptions { Timeout = 20000 });

        var userMenuButton = await page.QuerySelectorAsync("[data-testid='user-menu-button']");
        Assert.NotNull(userMenuButton);
        await userMenuButton.ClickAsync();

        await page.WaitForSelectorAsync("[data-testid='user-dropdown']", new PageWaitForSelectorOptions { Timeout = 5000 });

        var signOutButton = await page.QuerySelectorAsync("[data-testid='logout-button']");
        Assert.NotNull(signOutButton);
        Assert.True(await signOutButton.IsVisibleAsync());

        await page.CloseAsync();
    }

    /// <summary>
    /// Sign Out button click shows login page.
    /// </summary>
    [Fact]
    public async Task SignOutButton_ClickShowsLoginPage()
    {
        var page = await _fixture.Browser!.NewPageAsync();
        page.Console += (_, msg) => Console.WriteLine($"[BROWSER] {msg.Type}: {msg.Text}");

        // Use testMode URL to ensure app loads reliably
        await page.GotoAsync(E2EFixture.DashboardUrl);
        await page.WaitForSelectorAsync(".sidebar", new PageWaitForSelectorOptions { Timeout = 20000 });

        await page.ClickAsync("[data-testid='user-menu-button']");
        await page.WaitForSelectorAsync("[data-testid='user-dropdown']", new PageWaitForSelectorOptions { Timeout = 5000 });
        await page.ClickAsync("[data-testid='logout-button']");

        await page.WaitForSelectorAsync("[data-testid='login-page']", new PageWaitForSelectorOptions { Timeout = 10000 });

        var tokenAfterLogout = await page.EvaluateAsync<string?>("() => localStorage.getItem('gatekeeper_token')");
        Assert.Null(tokenAfterLogout);

        await page.CloseAsync();
    }

    /// <summary>
    /// Gatekeeper API logout revokes token.
    /// </summary>
    [Fact]
    public async Task GatekeeperApi_Logout_RevokesToken()
    {
        using var client = E2EFixture.CreateAuthenticatedClient();

        var logoutResponse = await client.PostAsync(
            $"{E2EFixture.GatekeeperUrl}/auth/logout",
            new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        using var unauthClient = new HttpClient();
        var unauthResponse = await unauthClient.PostAsync(
            $"{E2EFixture.GatekeeperUrl}/auth/logout",
            new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Unauthorized, unauthResponse.StatusCode);
    }

    /// <summary>
    /// User menu displays user initials and name in dropdown.
    /// </summary>
    [Fact]
    public async Task UserMenu_DisplaysUserInitialsAndNameInDropdown()
    {
        var page = await _fixture.Browser!.NewPageAsync();
        page.Console += (_, msg) => Console.WriteLine($"[BROWSER] {msg.Type}: {msg.Text}");

        // Set custom user data BEFORE loading
        await page.GotoAsync(E2EFixture.DashboardUrl);
        await page.EvaluateAsync(@"() => {
            localStorage.setItem('gatekeeper_token', 'fake-token-for-testing');
            localStorage.setItem('gatekeeper_user', JSON.stringify({
                userId: 'test-user', displayName: 'Alice Smith', email: 'alice@example.com'
            }));
        }");
        // Navigate again with testMode to pick up custom user data
        await page.GotoAsync(E2EFixture.DashboardUrl);
        await page.WaitForSelectorAsync(".sidebar", new PageWaitForSelectorOptions { Timeout = 20000 });

        var avatarText = await page.TextContentAsync("[data-testid='user-menu-button']");
        Assert.Equal("AS", avatarText?.Trim());

        await page.ClickAsync("[data-testid='user-menu-button']");
        await page.WaitForSelectorAsync("[data-testid='user-dropdown']", new PageWaitForSelectorOptions { Timeout = 5000 });

        var userNameText = await page.TextContentAsync(".user-dropdown-name");
        Assert.Contains("Alice Smith", userNameText);

        var emailText = await page.TextContentAsync(".user-dropdown-email");
        Assert.Contains("alice@example.com", emailText);

        await page.CloseAsync();
    }

    /// <summary>
    /// First-time sign-in must work WITHOUT browser refresh.
    /// </summary>
    [Fact]
    public async Task FirstTimeSignIn_TransitionsToDashboard_WithoutRefresh()
    {
        var page = await _fixture.Browser!.NewPageAsync();
        page.Console += (_, msg) => Console.WriteLine($"[BROWSER] {msg.Type}: {msg.Text}");

        await page.GotoAsync(E2EFixture.DashboardUrlNoTestMode);
        await page.EvaluateAsync("() => { localStorage.removeItem('gatekeeper_token'); localStorage.removeItem('gatekeeper_user'); }");
        await page.ReloadAsync();
        await page.WaitForSelectorAsync("[data-testid='login-page']", new PageWaitForSelectorOptions { Timeout = 20000 });

        // Wait for React to mount and set the __triggerLogin hook
        await page.WaitForFunctionAsync("() => typeof window.__triggerLogin === 'function'",
            new PageWaitForFunctionOptions { Timeout = 5000 });

        await page.EvaluateAsync(@"() => {
            localStorage.setItem('gatekeeper_token', 'simulated-jwt-token');
            localStorage.setItem('gatekeeper_user', JSON.stringify({
                userId: 'test-user-123', displayName: 'Test User', email: 'test@example.com'
            }));
            window.__triggerLogin({ userId: 'test-user-123', displayName: 'Test User', email: 'test@example.com' });
        }");

        await Task.Delay(500);

        try
        {
            await page.WaitForSelectorAsync(".sidebar", new PageWaitForSelectorOptions { Timeout = 5000 });
            var loginPageStillVisible = await page.IsVisibleAsync("[data-testid='login-page']");
            Assert.False(loginPageStillVisible);
            Assert.True(await page.IsVisibleAsync(".sidebar"));
        }
        catch (TimeoutException)
        {
            Assert.Fail("FIRST-TIME SIGN-IN BUG: App did not transition to dashboard after login.");
        }

        await page.CloseAsync();
    }
}
