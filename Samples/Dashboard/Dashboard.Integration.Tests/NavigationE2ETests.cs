using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace Dashboard.Integration.Tests;

/// <summary>
/// E2E tests for browser navigation (back/forward, deep linking).
/// </summary>
[Collection("E2E Tests")]
[Trait("Category", "E2E")]
public sealed class NavigationE2ETests
{
    private readonly E2EFixture _fixture;

    /// <summary>
    /// Constructor receives shared fixture.
    /// </summary>
    public NavigationE2ETests(E2EFixture fixture) => _fixture = fixture;

    /// <summary>
    /// Browser back button navigates to previous view.
    /// </summary>
    [Fact]
    public async Task BrowserBackButton_NavigatesToPreviousView()
    {
        var page = await _fixture.Browser!.NewPageAsync();
        page.Console += (_, msg) => Console.WriteLine($"[BROWSER] {msg.Text}");

        await page.GotoAsync(E2EFixture.DashboardUrl);
        await page.WaitForSelectorAsync(
            ".sidebar",
            new PageWaitForSelectorOptions { Timeout = 20000 }
        );
        Assert.Contains("#dashboard", page.Url);

        await page.ClickAsync("text=Patients");
        await page.WaitForSelectorAsync(
            "[data-testid='add-patient-btn']",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );
        Assert.Contains("#patients", page.Url);

        await page.ClickAsync("text=Appointments");
        await page.WaitForSelectorAsync(
            "[data-testid='add-appointment-btn']",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );
        Assert.Contains("#appointments", page.Url);

        await page.GoBackAsync();
        await page.WaitForSelectorAsync(
            "[data-testid='add-patient-btn']",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );
        Assert.Contains("#patients", page.Url);

        await page.GoBackAsync();
        await page.WaitForSelectorAsync(
            ".metric-card",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );
        Assert.Contains("#dashboard", page.Url);

        await page.CloseAsync();
    }

    /// <summary>
    /// Deep linking works - navigating directly to a hash URL loads correct view.
    /// </summary>
    [Fact]
    public async Task DeepLinking_LoadsCorrectView()
    {
        var page = await _fixture.Browser!.NewPageAsync();
        page.Console += (_, msg) => Console.WriteLine($"[BROWSER] {msg.Text}");

        await page.GotoAsync($"{E2EFixture.DashboardUrl}#patients");
        await page.WaitForSelectorAsync(
            ".sidebar",
            new PageWaitForSelectorOptions { Timeout = 20000 }
        );
        await page.WaitForSelectorAsync(
            "[data-testid='add-patient-btn']",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        var content = await page.ContentAsync();
        Assert.Contains("Patients", content);

        await page.GotoAsync($"{E2EFixture.DashboardUrl}#appointments");
        await page.WaitForSelectorAsync(
            "[data-testid='add-appointment-btn']",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        content = await page.ContentAsync();
        Assert.Contains("Appointments", content);

        await page.CloseAsync();
    }

    /// <summary>
    /// Cancel button on edit page uses history.back().
    /// </summary>
    [Fact]
    public async Task EditPatientCancelButton_UsesHistoryBack()
    {
        using var client = E2EFixture.CreateAuthenticatedClient();

        var uniqueName = $"CancelTest{DateTime.UtcNow.Ticks % 100000}";
        var createResponse = await client.PostAsync(
            $"{E2EFixture.ClinicalUrl}/fhir/Patient/",
            new StringContent(
                $$$"""{"Active": true, "GivenName": "{{{uniqueName}}}", "FamilyName": "CancelTestPatient", "Gender": "male"}""",
                System.Text.Encoding.UTF8,
                "application/json"
            )
        );
        createResponse.EnsureSuccessStatusCode();
        var createdJson = await createResponse.Content.ReadAsStringAsync();
        var patientIdMatch = Regex.Match(createdJson, "\"Id\"\\s*:\\s*\"([^\"]+)\"");
        var patientId = patientIdMatch.Groups[1].Value;

        var page = await _fixture.Browser!.NewPageAsync();
        page.Console += (_, msg) => Console.WriteLine($"[BROWSER] {msg.Text}");

        await page.GotoAsync(E2EFixture.DashboardUrl);
        await page.WaitForSelectorAsync(
            ".sidebar",
            new PageWaitForSelectorOptions { Timeout = 20000 }
        );
        await page.ClickAsync("text=Patients");
        await page.WaitForSelectorAsync(
            "[data-testid='add-patient-btn']",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        await page.FillAsync("input[placeholder*='Search']", uniqueName);
        await page.WaitForSelectorAsync(
            $"text={uniqueName}",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );
        await page.ClickAsync($"[data-testid='edit-patient-{patientId}']");
        await page.WaitForSelectorAsync(
            "[data-testid='edit-patient-page']",
            new PageWaitForSelectorOptions { Timeout = 5000 }
        );

        await page.ClickAsync("button:has-text('Cancel')");
        await page.WaitForSelectorAsync(
            "[data-testid='add-patient-btn']",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        Assert.Contains("#patients", page.Url);
        Assert.DoesNotContain("/edit/", page.Url);

        await page.CloseAsync();
    }

    /// <summary>
    /// Browser back button from Edit Patient page returns to patients list.
    /// </summary>
    [Fact]
    public async Task BrowserBackButton_FromEditPage_ReturnsToPatientsPage()
    {
        using var client = E2EFixture.CreateAuthenticatedClient();

        var uniqueName = $"BackBtnTest{DateTime.UtcNow.Ticks % 100000}";
        var createResponse = await client.PostAsync(
            $"{E2EFixture.ClinicalUrl}/fhir/Patient/",
            new StringContent(
                $$$"""{"Active": true, "GivenName": "{{{uniqueName}}}", "FamilyName": "BackButtonTest", "Gender": "female"}""",
                System.Text.Encoding.UTF8,
                "application/json"
            )
        );
        createResponse.EnsureSuccessStatusCode();
        var createdJson = await createResponse.Content.ReadAsStringAsync();
        var patientIdMatch = Regex.Match(createdJson, "\"Id\"\\s*:\\s*\"([^\"]+)\"");
        var patientId = patientIdMatch.Groups[1].Value;

        var page = await _fixture.Browser!.NewPageAsync();
        page.Console += (_, msg) => Console.WriteLine($"[BROWSER] {msg.Text}");

        await page.GotoAsync(E2EFixture.DashboardUrl);
        await page.WaitForSelectorAsync(
            ".sidebar",
            new PageWaitForSelectorOptions { Timeout = 20000 }
        );
        await page.ClickAsync("text=Patients");
        await page.WaitForSelectorAsync(
            "[data-testid='add-patient-btn']",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        await page.FillAsync("input[placeholder*='Search']", uniqueName);
        await page.WaitForSelectorAsync(
            $"text={uniqueName}",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );
        await page.ClickAsync($"[data-testid='edit-patient-{patientId}']");
        await page.WaitForSelectorAsync(
            "[data-testid='edit-patient-page']",
            new PageWaitForSelectorOptions { Timeout = 5000 }
        );

        await page.GoBackAsync();

        await page.WaitForSelectorAsync(
            ".sidebar",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );
        await page.WaitForSelectorAsync(
            "[data-testid='add-patient-btn']",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );
        Assert.Contains("#patients", page.Url);

        var content = await page.ContentAsync();
        Assert.Contains("Patients", content);
        Assert.Contains("Add Patient", content);

        await page.GoBackAsync();
        await page.WaitForSelectorAsync(
            ".metric-card",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );
        Assert.Contains("#dashboard", page.Url);

        await page.CloseAsync();
    }

    /// <summary>
    /// Forward button works after going back.
    /// </summary>
    [Fact]
    public async Task BrowserForwardButton_WorksAfterGoingBack()
    {
        var page = await _fixture.Browser!.NewPageAsync();
        page.Console += (_, msg) => Console.WriteLine($"[BROWSER] {msg.Text}");

        await page.GotoAsync(E2EFixture.DashboardUrl);
        await page.WaitForSelectorAsync(
            ".sidebar",
            new PageWaitForSelectorOptions { Timeout = 20000 }
        );

        await page.ClickAsync("text=Patients");
        await page.WaitForSelectorAsync(
            "[data-testid='add-patient-btn']",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        await page.ClickAsync("text=Practitioners");
        await page.WaitForSelectorAsync(
            ".practitioner-card, .empty-state",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );
        Assert.Contains("#practitioners", page.Url);

        await page.GoBackAsync();
        await page.WaitForSelectorAsync(
            "[data-testid='add-patient-btn']",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );
        Assert.Contains("#patients", page.Url);

        await page.GoForwardAsync();
        await page.WaitForSelectorAsync(
            ".practitioner-card, .empty-state",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );
        Assert.Contains("#practitioners", page.Url);

        var content = await page.ContentAsync();
        Assert.Contains("Practitioners", content);

        await page.CloseAsync();
    }
}
