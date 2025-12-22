using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace Dashboard.Integration.Tests;

/// <summary>
/// E2E tests for practitioner-related functionality.
/// </summary>
[Collection("E2E Tests")]
[Trait("Category", "E2E")]
public sealed class PractitionerE2ETests
{
    private readonly E2EFixture _fixture;

    /// <summary>
    /// Constructor receives shared fixture.
    /// </summary>
    public PractitionerE2ETests(E2EFixture fixture) => _fixture = fixture;

    /// <summary>
    /// Dashboard loads and displays practitioner data from Scheduling API.
    /// </summary>
    [Fact]
    public async Task Dashboard_DisplaysPractitionerData_FromSchedulingApi()
    {
        var page = await _fixture.Browser!.NewPageAsync();
        page.Console += (_, msg) => Console.WriteLine($"[BROWSER] {msg.Text}");

        await page.GotoAsync(E2EFixture.DashboardUrl);
        await page.WaitForSelectorAsync(".sidebar", new PageWaitForSelectorOptions { Timeout = 20000 });
        await page.ClickAsync("text=Practitioners");
        await page.WaitForSelectorAsync("text=DrTest", new PageWaitForSelectorOptions { Timeout = 10000 });
        await page.WaitForSelectorAsync(".practitioner-card", new PageWaitForSelectorOptions { Timeout = 5000 });

        var content = await page.ContentAsync();
        Assert.Contains("DrTest", content);
        Assert.Contains("E2EPractitioner", content);
        Assert.Contains("Johnson", content);
        Assert.Contains("MD", content);
        Assert.Contains("General Practice", content);

        await page.CloseAsync();
    }

    /// <summary>
    /// Practitioners page data comes from REAL Scheduling API.
    /// </summary>
    [Fact]
    public async Task PractitionersPage_LoadsFromSchedulingApi_WithFhirCompliantData()
    {
        using var client = E2EFixture.CreateAuthenticatedClient();
        var apiResponse = await client.GetStringAsync($"{E2EFixture.SchedulingUrl}/Practitioner");

        Assert.Contains("DR001", apiResponse);
        Assert.Contains("E2EPractitioner", apiResponse);
        Assert.Contains("MD", apiResponse);

        var page = await _fixture.Browser!.NewPageAsync();
        await page.GotoAsync(E2EFixture.DashboardUrl);
        await page.WaitForSelectorAsync(".sidebar", new PageWaitForSelectorOptions { Timeout = 20000 });
        await page.ClickAsync("text=Practitioners");
        await page.WaitForSelectorAsync(".practitioner-card", new PageWaitForSelectorOptions { Timeout = 10000 });

        var cards = await page.QuerySelectorAllAsync(".practitioner-card");
        Assert.True(cards.Count >= 3);

        await page.CloseAsync();
    }

    /// <summary>
    /// Practitioner creation API works end-to-end.
    /// </summary>
    [Fact]
    public async Task PractitionerCreationApi_WorksEndToEnd()
    {
        using var client = E2EFixture.CreateAuthenticatedClient();

        var uniqueId = $"DR{DateTime.UtcNow.Ticks % 100000}";
        var createResponse = await client.PostAsync(
            $"{E2EFixture.SchedulingUrl}/Practitioner",
            new StringContent(
                $$$"""{"Identifier": "{{{uniqueId}}}", "Active": true, "NameGiven": "ApiDoctor", "NameFamily": "TestDoc", "Qualification": "MD", "Specialty": "Testing", "TelecomEmail": "test@hospital.org", "TelecomPhone": "+1-555-9999"}""",
                System.Text.Encoding.UTF8, "application/json"));
        createResponse.EnsureSuccessStatusCode();

        var listResponse = await client.GetStringAsync($"{E2EFixture.SchedulingUrl}/Practitioner");
        Assert.Contains(uniqueId, listResponse);
    }

    /// <summary>
    /// Add Practitioner button opens modal and creates practitioner via API.
    /// </summary>
    [Fact]
    public async Task AddPractitionerButton_OpensModal_AndCreatesPractitioner()
    {
        var page = await _fixture.Browser!.NewPageAsync();
        page.Console += (_, msg) => Console.WriteLine($"[BROWSER] {msg.Text}");

        await page.GotoAsync(E2EFixture.DashboardUrl);
        await page.WaitForSelectorAsync(".sidebar", new PageWaitForSelectorOptions { Timeout = 20000 });
        await page.ClickAsync("text=Practitioners");
        await page.WaitForSelectorAsync("[data-testid='add-practitioner-btn']", new PageWaitForSelectorOptions { Timeout = 10000 });
        await page.ClickAsync("[data-testid='add-practitioner-btn']");
        await page.WaitForSelectorAsync(".modal", new PageWaitForSelectorOptions { Timeout = 5000 });

        var uniqueIdentifier = $"DR{DateTime.UtcNow.Ticks % 100000}";
        var uniqueGivenName = $"E2EDoc{DateTime.UtcNow.Ticks % 100000}";
        await page.FillAsync("[data-testid='practitioner-identifier']", uniqueIdentifier);
        await page.FillAsync("[data-testid='practitioner-given-name']", uniqueGivenName);
        await page.FillAsync("[data-testid='practitioner-family-name']", "TestCreated");
        await page.FillAsync("[data-testid='practitioner-specialty']", "E2E Testing");
        await page.ClickAsync("[data-testid='submit-practitioner']");

        await page.WaitForSelectorAsync($"text={uniqueGivenName}", new PageWaitForSelectorOptions { Timeout = 10000 });

        using var client = E2EFixture.CreateAuthenticatedClient();
        var response = await client.GetStringAsync($"{E2EFixture.SchedulingUrl}/Practitioner");
        Assert.Contains(uniqueIdentifier, response);

        await page.CloseAsync();
    }

    /// <summary>
    /// Edit Practitioner button navigates to edit page and updates practitioner.
    /// </summary>
    [Fact]
    public async Task EditPractitionerButton_OpensEditPage_AndUpdatesPractitioner()
    {
        using var client = E2EFixture.CreateAuthenticatedClient();

        var uniqueIdentifier = $"DREdit{DateTime.UtcNow.Ticks % 100000}";
        var uniqueGivenName = $"EditTest{DateTime.UtcNow.Ticks % 100000}";
        var createResponse = await client.PostAsync(
            $"{E2EFixture.SchedulingUrl}/Practitioner",
            new StringContent(
                $$$"""{"Identifier": "{{{uniqueIdentifier}}}", "NameFamily": "OriginalFamily", "NameGiven": "{{{uniqueGivenName}}}", "Qualification": "MD", "Specialty": "Original Specialty"}""",
                System.Text.Encoding.UTF8, "application/json"));
        createResponse.EnsureSuccessStatusCode();
        var createdJson = await createResponse.Content.ReadAsStringAsync();
        var practitionerIdMatch = Regex.Match(createdJson, "\"Id\"\\s*:\\s*\"([^\"]+)\"");
        var practitionerId = practitionerIdMatch.Groups[1].Value;

        var page = await _fixture.Browser!.NewPageAsync();
        page.Console += (_, msg) => Console.WriteLine($"[BROWSER] {msg.Text}");

        await page.GotoAsync(E2EFixture.DashboardUrl);
        await page.WaitForSelectorAsync(".sidebar", new PageWaitForSelectorOptions { Timeout = 20000 });
        await page.ClickAsync("text=Practitioners");
        await page.WaitForSelectorAsync($"text={uniqueGivenName}", new PageWaitForSelectorOptions { Timeout = 10000 });

        var editButton = page.Locator($"[data-testid='edit-practitioner-{practitionerId}']");
        await editButton.HoverAsync();
        await editButton.ClickAsync();

        await page.WaitForSelectorAsync("[data-testid='edit-practitioner-page']", new PageWaitForSelectorOptions { Timeout = 5000 });

        var newSpecialty = $"Updated Specialty {DateTime.UtcNow.Ticks % 100000}";
        await page.FillAsync("[data-testid='edit-practitioner-specialty']", newSpecialty);
        await page.ClickAsync("[data-testid='save-practitioner']");
        await page.WaitForSelectorAsync("[data-testid='edit-practitioner-success']", new PageWaitForSelectorOptions { Timeout = 10000 });

        var updatedPractitionerJson = await client.GetStringAsync($"{E2EFixture.SchedulingUrl}/Practitioner/{practitionerId}");
        Assert.Contains(newSpecialty, updatedPractitionerJson);

        await page.CloseAsync();
    }

    /// <summary>
    /// Practitioner update API works end-to-end.
    /// </summary>
    [Fact]
    public async Task PractitionerUpdateApi_WorksEndToEnd()
    {
        using var client = E2EFixture.CreateAuthenticatedClient();

        var uniqueIdentifier = $"DRApi{DateTime.UtcNow.Ticks % 100000}";
        var createResponse = await client.PostAsync(
            $"{E2EFixture.SchedulingUrl}/Practitioner",
            new StringContent(
                $$$"""{"Identifier": "{{{uniqueIdentifier}}}", "NameFamily": "ApiOriginal", "NameGiven": "TestDoc", "Qualification": "MD", "Specialty": "Original"}""",
                System.Text.Encoding.UTF8, "application/json"));
        createResponse.EnsureSuccessStatusCode();
        var createdPractitionerJson = await createResponse.Content.ReadAsStringAsync();

        var practitionerIdMatch = Regex.Match(createdPractitionerJson, "\"Id\"\\s*:\\s*\"([^\"]+)\"");
        var practitionerId = practitionerIdMatch.Groups[1].Value;

        var updatedSpecialty = $"ApiUpdated{DateTime.UtcNow.Ticks % 100000}";
        var updateResponse = await client.PutAsync(
            $"{E2EFixture.SchedulingUrl}/Practitioner/{practitionerId}",
            new StringContent(
                $$$"""{"Identifier": "{{{uniqueIdentifier}}}", "Active": true, "NameFamily": "ApiUpdated", "NameGiven": "TestDoc", "Qualification": "DO", "Specialty": "{{{updatedSpecialty}}}", "TelecomEmail": "updated@hospital.com", "TelecomPhone": "555-1234"}""",
                System.Text.Encoding.UTF8, "application/json"));
        updateResponse.EnsureSuccessStatusCode();

        var getResponse = await client.GetStringAsync($"{E2EFixture.SchedulingUrl}/Practitioner/{practitionerId}");
        Assert.Contains(updatedSpecialty, getResponse);
        Assert.Contains("ApiUpdated", getResponse);
    }

    /// <summary>
    /// Browser back button works from Edit Practitioner page.
    /// </summary>
    [Fact]
    public async Task BrowserBackButton_FromEditPractitionerPage_ReturnsToPractitionersPage()
    {
        using var client = E2EFixture.CreateAuthenticatedClient();

        var uniqueIdentifier = $"DRBack{DateTime.UtcNow.Ticks % 100000}";
        var uniqueGivenName = $"BackTest{DateTime.UtcNow.Ticks % 100000}";
        var createResponse = await client.PostAsync(
            $"{E2EFixture.SchedulingUrl}/Practitioner",
            new StringContent(
                $$$"""{"Identifier": "{{{uniqueIdentifier}}}", "NameFamily": "BackButtonTest", "NameGiven": "{{{uniqueGivenName}}}", "Qualification": "MD", "Specialty": "Testing"}""",
                System.Text.Encoding.UTF8, "application/json"));
        createResponse.EnsureSuccessStatusCode();
        var createdJson = await createResponse.Content.ReadAsStringAsync();
        var practitionerIdMatch = Regex.Match(createdJson, "\"Id\"\\s*:\\s*\"([^\"]+)\"");
        var practitionerId = practitionerIdMatch.Groups[1].Value;

        var page = await _fixture.Browser!.NewPageAsync();
        page.Console += (_, msg) => Console.WriteLine($"[BROWSER] {msg.Text}");

        await page.GotoAsync(E2EFixture.DashboardUrl);
        await page.WaitForSelectorAsync(".sidebar", new PageWaitForSelectorOptions { Timeout = 20000 });
        await page.ClickAsync("text=Practitioners");
        await page.WaitForSelectorAsync($"text={uniqueGivenName}", new PageWaitForSelectorOptions { Timeout = 10000 });

        var editButton = page.Locator($"[data-testid='edit-practitioner-{practitionerId}']");
        await editButton.HoverAsync();
        await editButton.ClickAsync();
        await page.WaitForSelectorAsync("[data-testid='edit-practitioner-page']", new PageWaitForSelectorOptions { Timeout = 5000 });

        await page.GoBackAsync();

        await page.WaitForSelectorAsync(".sidebar", new PageWaitForSelectorOptions { Timeout = 10000 });
        await page.WaitForSelectorAsync("[data-testid='add-practitioner-btn']", new PageWaitForSelectorOptions { Timeout = 10000 });
        Assert.Contains("#practitioners", page.Url);

        await page.CloseAsync();
    }
}
