using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace Dashboard.Integration.Tests;

/// <summary>
/// E2E tests for patient-related functionality.
/// </summary>
[Collection("E2E Tests")]
[Trait("Category", "E2E")]
public sealed class PatientE2ETests
{
    private readonly E2EFixture _fixture;

    /// <summary>
    /// Constructor receives shared fixture.
    /// </summary>
    public PatientE2ETests(E2EFixture fixture) => _fixture = fixture;

    /// <summary>
    /// Dashboard loads and displays patient data from Clinical API.
    /// </summary>
    [Fact]
    public async Task Dashboard_DisplaysPatientData_FromClinicalApi()
    {
        var page = await _fixture.Browser!.NewPageAsync();
        page.Console += (_, msg) => Console.WriteLine($"[BROWSER] {msg.Type}: {msg.Text}");

        await page.GotoAsync(E2EFixture.DashboardUrl);
        await page.WaitForSelectorAsync(
            ".sidebar",
            new PageWaitForSelectorOptions { Timeout = 20000 }
        );
        await page.ClickAsync("text=Patients");
        await page.WaitForSelectorAsync(
            "text=TestPatient",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        var content = await page.ContentAsync();
        Assert.Contains("TestPatient", content);
        Assert.Contains("E2ETest", content);

        await page.CloseAsync();
    }

    /// <summary>
    /// Add Patient button opens modal and creates patient via API.
    /// </summary>
    [Fact]
    public async Task AddPatientButton_OpensModal_AndCreatesPatient()
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
        await page.ClickAsync("[data-testid='add-patient-btn']");
        await page.WaitForSelectorAsync(
            ".modal",
            new PageWaitForSelectorOptions { Timeout = 5000 }
        );

        var uniqueName = $"E2ECreated{DateTime.UtcNow.Ticks % 100000}";
        await page.FillAsync("[data-testid='patient-given-name']", uniqueName);
        await page.FillAsync("[data-testid='patient-family-name']", "TestCreated");
        await page.SelectOptionAsync("[data-testid='patient-gender']", "male");
        await page.ClickAsync("[data-testid='submit-patient']");

        await page.WaitForSelectorAsync(
            $"text={uniqueName}",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        using var client = E2EFixture.CreateAuthenticatedClient();
        var response = await client.GetStringAsync($"{E2EFixture.ClinicalUrl}/fhir/Patient/");
        Assert.Contains(uniqueName, response);

        await page.CloseAsync();
    }

    /// <summary>
    /// Patient Search button navigates to search and finds patients.
    /// </summary>
    [Fact]
    public async Task PatientSearchButton_NavigatesToSearch_AndFindsPatients()
    {
        var page = await _fixture.Browser!.NewPageAsync();

        await page.GotoAsync(E2EFixture.DashboardUrl);
        await page.WaitForSelectorAsync(
            ".sidebar",
            new PageWaitForSelectorOptions { Timeout = 20000 }
        );
        await page.ClickAsync("text=Patient Search");
        await page.WaitForSelectorAsync(
            "input[placeholder*='Search']",
            new PageWaitForSelectorOptions { Timeout = 5000 }
        );
        await page.FillAsync("input[placeholder*='Search']", "E2ETest");
        await page.WaitForSelectorAsync(
            "text=TestPatient",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        var content = await page.ContentAsync();
        Assert.Contains("TestPatient", content);

        await page.CloseAsync();
    }

    /// <summary>
    /// Patient creation API works end-to-end.
    /// </summary>
    [Fact]
    public async Task PatientCreationApi_WorksEndToEnd()
    {
        using var client = E2EFixture.CreateAuthenticatedClient();

        var uniqueName = $"ApiTest{DateTime.UtcNow.Ticks % 100000}";
        var createResponse = await client.PostAsync(
            $"{E2EFixture.ClinicalUrl}/fhir/Patient/",
            new StringContent(
                $$$"""{"Active": true, "GivenName": "{{{uniqueName}}}", "FamilyName": "ApiCreated", "Gender": "female"}""",
                System.Text.Encoding.UTF8,
                "application/json"
            )
        );
        createResponse.EnsureSuccessStatusCode();

        var listResponse = await client.GetStringAsync($"{E2EFixture.ClinicalUrl}/fhir/Patient/");
        Assert.Contains(uniqueName, listResponse);
    }

    /// <summary>
    /// Edit Patient button opens edit page and updates patient via API.
    /// </summary>
    [Fact]
    public async Task EditPatientButton_OpensEditPage_AndUpdatesPatient()
    {
        using var client = E2EFixture.CreateAuthenticatedClient();

        var uniqueName = $"EditTest{DateTime.UtcNow.Ticks % 100000}";
        var createResponse = await client.PostAsync(
            $"{E2EFixture.ClinicalUrl}/fhir/Patient/",
            new StringContent(
                $$$"""{"Active": true, "GivenName": "{{{uniqueName}}}", "FamilyName": "ToBeEdited", "Gender": "female"}""",
                System.Text.Encoding.UTF8,
                "application/json"
            )
        );
        createResponse.EnsureSuccessStatusCode();
        var createdPatientJson = await createResponse.Content.ReadAsStringAsync();

        var patientIdMatch = Regex.Match(createdPatientJson, "\"Id\"\\s*:\\s*\"([^\"]+)\"");
        Assert.True(patientIdMatch.Success);
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

        var newFamilyName = $"Edited{DateTime.UtcNow.Ticks % 100000}";
        await page.FillAsync("[data-testid='edit-family-name']", newFamilyName);
        await page.ClickAsync("[data-testid='save-patient']");
        await page.WaitForSelectorAsync(
            "[data-testid='edit-success']",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        var updatedPatientJson = await client.GetStringAsync(
            $"{E2EFixture.ClinicalUrl}/fhir/Patient/{patientId}"
        );
        Assert.Contains(newFamilyName, updatedPatientJson);

        await page.CloseAsync();
    }

    /// <summary>
    /// Patient update API works end-to-end.
    /// </summary>
    [Fact]
    public async Task PatientUpdateApi_WorksEndToEnd()
    {
        using var client = E2EFixture.CreateAuthenticatedClient();

        var uniqueName = $"UpdateApiTest{DateTime.UtcNow.Ticks % 100000}";
        var createResponse = await client.PostAsync(
            $"{E2EFixture.ClinicalUrl}/fhir/Patient/",
            new StringContent(
                $$$"""{"Active": true, "GivenName": "{{{uniqueName}}}", "FamilyName": "Original", "Gender": "male"}""",
                System.Text.Encoding.UTF8,
                "application/json"
            )
        );
        createResponse.EnsureSuccessStatusCode();
        var createdPatientJson = await createResponse.Content.ReadAsStringAsync();

        var patientIdMatch = Regex.Match(createdPatientJson, "\"Id\"\\s*:\\s*\"([^\"]+)\"");
        var patientId = patientIdMatch.Groups[1].Value;

        var updatedFamilyName = $"Updated{DateTime.UtcNow.Ticks % 100000}";
        var updateResponse = await client.PutAsync(
            $"{E2EFixture.ClinicalUrl}/fhir/Patient/{patientId}",
            new StringContent(
                $$$"""{"Active": true, "GivenName": "{{{uniqueName}}}", "FamilyName": "{{{updatedFamilyName}}}", "Gender": "male", "Email": "updated@test.com"}""",
                System.Text.Encoding.UTF8,
                "application/json"
            )
        );
        updateResponse.EnsureSuccessStatusCode();

        var getResponse = await client.GetStringAsync(
            $"{E2EFixture.ClinicalUrl}/fhir/Patient/{patientId}"
        );
        Assert.Contains(updatedFamilyName, getResponse);
        Assert.Contains("updated@test.com", getResponse);
    }
}
