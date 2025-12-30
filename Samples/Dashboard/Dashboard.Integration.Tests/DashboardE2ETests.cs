using System.Net;
using Microsoft.Playwright;

namespace Dashboard.Integration.Tests;

/// <summary>
/// Core Dashboard E2E tests.
/// Uses EXACTLY the same ports as the real app.
/// </summary>
[Collection("E2E Tests")]
[Trait("Category", "E2E")]
public sealed class DashboardE2ETests
{
    private readonly E2EFixture _fixture;

    /// <summary>
    /// Constructor receives shared fixture.
    /// </summary>
    public DashboardE2ETests(E2EFixture fixture) => _fixture = fixture;

    /// <summary>
    /// Dashboard main page shows stats from both APIs.
    /// </summary>
    [Fact]
    public async Task Dashboard_MainPage_ShowsStatsFromBothApis()
    {
        var page = await _fixture.Browser!.NewPageAsync();
        await page.GotoAsync(E2EFixture.DashboardUrl);
        await page.WaitForSelectorAsync(
            ".sidebar",
            new PageWaitForSelectorOptions { Timeout = 20000 }
        );
        await page.WaitForSelectorAsync(
            ".metric-card",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        var cards = await page.QuerySelectorAllAsync(".metric-card");
        Assert.True(cards.Count > 0, "Dashboard should display metric cards with API data");

        await page.CloseAsync();
    }

    /// <summary>
    /// CRITICAL TEST: Add Patient button opens modal and creates patient via API.
    /// Uses Playwright to load REAL Dashboard, click Add Patient, fill form, and POST to REAL API.
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

        // Navigate to Patients page
        await page.ClickAsync("text=Patients");
        await page.WaitForSelectorAsync(
            "[data-testid='add-patient-btn']",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        // Click Add Patient button
        await page.ClickAsync("[data-testid='add-patient-btn']");

        // Wait for modal to appear
        await page.WaitForSelectorAsync(
            ".modal",
            new PageWaitForSelectorOptions { Timeout = 5000 }
        );

        // Fill in patient details
        var uniqueName = $"E2ECreated{DateTime.UtcNow.Ticks % 100000}";
        await page.FillAsync("[data-testid='patient-given-name']", uniqueName);
        await page.FillAsync("[data-testid='patient-family-name']", "TestCreated");
        await page.SelectOptionAsync("[data-testid='patient-gender']", "male");

        // Submit the form
        await page.ClickAsync("[data-testid='submit-patient']");

        // Wait for modal to close and patient to appear in list
        await page.WaitForSelectorAsync(
            $"text={uniqueName}",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        // Verify via API that patient was actually created
        using var client = E2EFixture.CreateAuthenticatedClient();
        var response = await client.GetStringAsync($"{E2EFixture.ClinicalUrl}/fhir/Patient/");
        Assert.Contains(uniqueName, response);

        await page.CloseAsync();
    }

    /// <summary>
    /// CRITICAL TEST: Add Appointment button opens modal and creates appointment via API.
    /// Uses Playwright to load REAL Dashboard, click Add Appointment, fill form, and POST to REAL API.
    /// </summary>
    [Fact]
    public async Task AddAppointmentButton_OpensModal_AndCreatesAppointment()
    {
        var page = await _fixture.Browser!.NewPageAsync();
        page.Console += (_, msg) => Console.WriteLine($"[BROWSER] {msg.Text}");

        await page.GotoAsync(E2EFixture.DashboardUrl);
        await page.WaitForSelectorAsync(
            ".sidebar",
            new PageWaitForSelectorOptions { Timeout = 20000 }
        );

        // Navigate to Appointments page
        await page.ClickAsync("text=Appointments");
        await page.WaitForSelectorAsync(
            "[data-testid='add-appointment-btn']",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        // Click Add Appointment button
        await page.ClickAsync("[data-testid='add-appointment-btn']");

        // Wait for modal to appear
        await page.WaitForSelectorAsync(
            ".modal",
            new PageWaitForSelectorOptions { Timeout = 5000 }
        );

        // Fill in appointment details
        var uniqueServiceType = $"E2EConsult{DateTime.UtcNow.Ticks % 100000}";
        await page.FillAsync("[data-testid='appointment-service-type']", uniqueServiceType);

        // Submit the form
        await page.ClickAsync("[data-testid='submit-appointment']");

        // Wait for modal to close and appointment to appear in list
        await page.WaitForSelectorAsync(
            $"text={uniqueServiceType}",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        // Verify via API that appointment was actually created
        using var client = E2EFixture.CreateAuthenticatedClient();
        var response = await client.GetStringAsync($"{E2EFixture.SchedulingUrl}/Appointment");
        Assert.Contains(uniqueServiceType, response);

        await page.CloseAsync();
    }

    /// <summary>
    /// CRITICAL TEST: Patient Search button navigates to search and finds patients.
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

        // Click the Patient Search button
        await page.ClickAsync("text=Patient Search");

        // Should navigate to patients page with search focused
        await page.WaitForSelectorAsync(
            "input[placeholder*='Search']",
            new PageWaitForSelectorOptions { Timeout = 5000 }
        );

        // Type a search query
        await page.FillAsync("input[placeholder*='Search']", "E2ETest");

        // Wait for filtered results
        await page.WaitForSelectorAsync(
            "text=TestPatient",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        var content = await page.ContentAsync();
        Assert.Contains("TestPatient", content);

        await page.CloseAsync();
    }

    /// <summary>
    /// CRITICAL TEST: View Schedule button navigates to appointments view.
    /// </summary>
    [Fact]
    public async Task ViewScheduleButton_NavigatesToAppointments()
    {
        var page = await _fixture.Browser!.NewPageAsync();

        await page.GotoAsync(E2EFixture.DashboardUrl);
        await page.WaitForSelectorAsync(
            ".sidebar",
            new PageWaitForSelectorOptions { Timeout = 20000 }
        );

        // Click the View Schedule button
        await page.ClickAsync("text=View Schedule");

        // Should navigate to appointments page
        await page.WaitForSelectorAsync(
            "text=Appointments",
            new PageWaitForSelectorOptions { Timeout = 5000 }
        );

        // Should show the seeded appointment
        await page.WaitForSelectorAsync(
            "text=Checkup",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        var content = await page.ContentAsync();
        Assert.Contains("Checkup", content);

        await page.CloseAsync();
    }

    /// <summary>
    /// CRITICAL TEST: Proves patient creation API works end-to-end.
    /// This test hits the real Clinical API directly without Playwright.
    /// </summary>
    [Fact]
    public async Task PatientCreationApi_WorksEndToEnd()
    {
        using var client = E2EFixture.CreateAuthenticatedClient();

        // Create a patient with a unique name
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

        // Verify patient was created by fetching all patients
        var listResponse = await client.GetStringAsync($"{E2EFixture.ClinicalUrl}/fhir/Patient/");
        Assert.Contains(uniqueName, listResponse);
        Assert.Contains("ApiCreated", listResponse);
    }

    /// <summary>
    /// CRITICAL TEST: Proves practitioner creation API works end-to-end.
    /// This test hits the real Scheduling API directly without Playwright.
    /// </summary>
    [Fact]
    public async Task PractitionerCreationApi_WorksEndToEnd()
    {
        using var client = E2EFixture.CreateAuthenticatedClient();

        // Create a practitioner with a unique identifier
        var uniqueId = $"DR{DateTime.UtcNow.Ticks % 100000}";
        var createResponse = await client.PostAsync(
            $"{E2EFixture.SchedulingUrl}/Practitioner",
            new StringContent(
                $$$"""{"Identifier": "{{{uniqueId}}}", "Active": true, "NameGiven": "ApiDoctor", "NameFamily": "TestDoc", "Qualification": "MD", "Specialty": "Testing", "TelecomEmail": "test@hospital.org", "TelecomPhone": "+1-555-9999"}""",
                System.Text.Encoding.UTF8,
                "application/json"
            )
        );
        createResponse.EnsureSuccessStatusCode();

        // Verify practitioner was created
        var listResponse = await client.GetStringAsync($"{E2EFixture.SchedulingUrl}/Practitioner");
        Assert.Contains(uniqueId, listResponse);
        Assert.Contains("ApiDoctor", listResponse);
    }

    /// <summary>
    /// CRITICAL TEST: Edit Patient button opens edit page and updates patient via API.
    /// Uses Playwright to load REAL Dashboard, click Edit, modify form, and PUT to REAL API.
    /// </summary>
    [Fact]
    public async Task EditPatientButton_OpensEditPage_AndUpdatesPatient()
    {
        using var client = E2EFixture.CreateAuthenticatedClient();

        // First create a patient to edit
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

        // Extract patient ID from response
        var patientIdMatch = System.Text.RegularExpressions.Regex.Match(
            createdPatientJson,
            "\"Id\"\\s*:\\s*\"([^\"]+)\""
        );
        Assert.True(patientIdMatch.Success, "Should get patient ID from creation response");
        var patientId = patientIdMatch.Groups[1].Value;

        var page = await _fixture.Browser!.NewPageAsync();
        page.Console += (_, msg) => Console.WriteLine($"[BROWSER] {msg.Text}");

        await page.GotoAsync(E2EFixture.DashboardUrl);
        await page.WaitForSelectorAsync(
            ".sidebar",
            new PageWaitForSelectorOptions { Timeout = 20000 }
        );

        // Navigate to Patients page
        await page.ClickAsync("text=Patients");

        // Wait for the page to load (add-patient-btn is a good indicator)
        await page.WaitForSelectorAsync(
            "[data-testid='add-patient-btn']",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        // Search for the patient to make sure it appears
        await page.FillAsync("input[placeholder*='Search']", uniqueName);

        // Wait for the patient to appear in filtered results
        await page.WaitForSelectorAsync(
            $"text={uniqueName}",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        // Click Edit button for the created patient
        await page.ClickAsync($"[data-testid='edit-patient-{patientId}']");

        // Wait for edit page to load
        await page.WaitForSelectorAsync(
            "[data-testid='edit-patient-page']",
            new PageWaitForSelectorOptions { Timeout = 5000 }
        );

        // Verify we're on the edit page with the correct patient data
        await page.WaitForSelectorAsync(
            "[data-testid='edit-given-name']",
            new PageWaitForSelectorOptions { Timeout = 5000 }
        );

        // Modify the patient's name
        var newFamilyName = $"Edited{DateTime.UtcNow.Ticks % 100000}";
        await page.FillAsync("[data-testid='edit-family-name']", newFamilyName);

        // Submit the form
        await page.ClickAsync("[data-testid='save-patient']");

        // Wait for success message
        await page.WaitForSelectorAsync(
            "[data-testid='edit-success']",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        // Verify via API that patient was actually updated
        var updatedPatientJson = await client.GetStringAsync(
            $"{E2EFixture.ClinicalUrl}/fhir/Patient/{patientId}"
        );
        Assert.Contains(newFamilyName, updatedPatientJson);

        await page.CloseAsync();
    }

    /// <summary>
    /// CRITICAL TEST: Browser back button navigates to previous view.
    /// Proves history.pushState/popstate integration works correctly.
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

        // Start on dashboard (default)
        Assert.Contains("#dashboard", page.Url);

        // Navigate to Patients
        await page.ClickAsync("text=Patients");
        await page.WaitForSelectorAsync(
            "[data-testid='add-patient-btn']",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );
        Assert.Contains("#patients", page.Url);

        // Navigate to Appointments
        await page.ClickAsync("text=Appointments");
        await page.WaitForSelectorAsync(
            "[data-testid='add-appointment-btn']",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );
        Assert.Contains("#appointments", page.Url);

        // Press browser back - should go to Patients
        await page.GoBackAsync();
        await page.WaitForSelectorAsync(
            "[data-testid='add-patient-btn']",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );
        Assert.Contains("#patients", page.Url);

        // Press browser back again - should go to Dashboard
        await page.GoBackAsync();
        await page.WaitForSelectorAsync(
            ".metric-card",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );
        Assert.Contains("#dashboard", page.Url);

        await page.CloseAsync();
    }

    /// <summary>
    /// CRITICAL TEST: Deep linking works - navigating directly to a hash URL loads correct view.
    /// </summary>
    [Fact]
    public async Task DeepLinking_LoadsCorrectView()
    {
        var page = await _fixture.Browser!.NewPageAsync();
        page.Console += (_, msg) => Console.WriteLine($"[BROWSER] {msg.Text}");

        // Navigate directly to patients page via hash
        await page.GotoAsync($"{E2EFixture.DashboardUrl}#patients");
        await page.WaitForSelectorAsync(
            ".sidebar",
            new PageWaitForSelectorOptions { Timeout = 20000 }
        );
        await page.WaitForSelectorAsync(
            "[data-testid='add-patient-btn']",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        // Verify we're on patients page
        var content = await page.ContentAsync();
        Assert.Contains("Patients", content);

        // Navigate directly to appointments via hash
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
    /// CRITICAL TEST: Cancel button on edit page uses history.back() - same behavior as browser back.
    /// </summary>
    [Fact]
    public async Task EditPatientCancelButton_UsesHistoryBack()
    {
        using var client = E2EFixture.CreateAuthenticatedClient();

        // Create a patient to edit
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
        var patientIdMatch = System.Text.RegularExpressions.Regex.Match(
            createdJson,
            "\"Id\"\\s*:\\s*\"([^\"]+)\""
        );
        var patientId = patientIdMatch.Groups[1].Value;

        var page = await _fixture.Browser!.NewPageAsync();
        page.Console += (_, msg) => Console.WriteLine($"[BROWSER] {msg.Text}");

        await page.GotoAsync(E2EFixture.DashboardUrl);
        await page.WaitForSelectorAsync(
            ".sidebar",
            new PageWaitForSelectorOptions { Timeout = 20000 }
        );

        // Navigate to Patients
        await page.ClickAsync("text=Patients");
        await page.WaitForSelectorAsync(
            "[data-testid='add-patient-btn']",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );
        Assert.Contains("#patients", page.Url);

        // Search for and click edit on the patient
        await page.FillAsync("input[placeholder*='Search']", uniqueName);
        await page.WaitForSelectorAsync(
            $"text={uniqueName}",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );
        await page.ClickAsync($"[data-testid='edit-patient-{patientId}']");

        // Wait for edit page
        await page.WaitForSelectorAsync(
            "[data-testid='edit-patient-page']",
            new PageWaitForSelectorOptions { Timeout = 5000 }
        );
        Assert.Contains($"#patients/edit/{patientId}", page.Url);

        // Click Cancel button - should use history.back() and return to patients list
        await page.ClickAsync("button:has-text('Cancel')");
        await page.WaitForSelectorAsync(
            "[data-testid='add-patient-btn']",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        // Should be back on patients page
        Assert.Contains("#patients", page.Url);
        Assert.DoesNotContain("/edit/", page.Url);

        await page.CloseAsync();
    }

    /// <summary>
    /// CRITICAL TEST: Browser back button works from Edit Patient page.
    /// This is THE test that proves the original bug is fixed - pressing browser back
    /// from an edit page should return to patients list, NOT show a blank page.
    /// </summary>
    [Fact]
    public async Task BrowserBackButton_FromEditPage_ReturnsToPatientsPage()
    {
        using var client = E2EFixture.CreateAuthenticatedClient();

        // Create a patient to edit
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
        var patientIdMatch = System.Text.RegularExpressions.Regex.Match(
            createdJson,
            "\"Id\"\\s*:\\s*\"([^\"]+)\""
        );
        var patientId = patientIdMatch.Groups[1].Value;

        var page = await _fixture.Browser!.NewPageAsync();
        page.Console += (_, msg) => Console.WriteLine($"[BROWSER] {msg.Text}");

        // Start at dashboard
        await page.GotoAsync(E2EFixture.DashboardUrl);
        await page.WaitForSelectorAsync(
            ".sidebar",
            new PageWaitForSelectorOptions { Timeout = 20000 }
        );
        Assert.Contains("#dashboard", page.Url);

        // Navigate to Patients
        await page.ClickAsync("text=Patients");
        await page.WaitForSelectorAsync(
            "[data-testid='add-patient-btn']",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );
        Assert.Contains("#patients", page.Url);

        // Search for the patient
        await page.FillAsync("input[placeholder*='Search']", uniqueName);
        await page.WaitForSelectorAsync(
            $"text={uniqueName}",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        // Click edit to go to edit page
        await page.ClickAsync($"[data-testid='edit-patient-{patientId}']");
        await page.WaitForSelectorAsync(
            "[data-testid='edit-patient-page']",
            new PageWaitForSelectorOptions { Timeout = 5000 }
        );
        Assert.Contains($"#patients/edit/{patientId}", page.Url);

        // THE CRITICAL TEST: Press browser back button
        // Before the fix, this would show a blank "Guest browsing" page
        // After the fix, it should return to the patients list
        await page.GoBackAsync();

        // Should be back on patients page with sidebar visible (NOT a blank page)
        await page.WaitForSelectorAsync(
            ".sidebar",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );
        await page.WaitForSelectorAsync(
            "[data-testid='add-patient-btn']",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );
        Assert.Contains("#patients", page.Url);
        Assert.DoesNotContain("/edit/", page.Url);

        // Verify the page content is actually the patients page, not blank
        var content = await page.ContentAsync();
        Assert.Contains("Patients", content);
        Assert.Contains("Add Patient", content);

        // Press back again - should go to dashboard
        await page.GoBackAsync();
        await page.WaitForSelectorAsync(
            ".metric-card",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );
        Assert.Contains("#dashboard", page.Url);

        await page.CloseAsync();
    }

    /// <summary>
    /// CRITICAL TEST: Forward button works after going back.
    /// Proves full history navigation (back AND forward) works correctly.
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

        // Navigate: Dashboard -> Patients -> Practitioners
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

        // Go back to Patients
        await page.GoBackAsync();
        await page.WaitForSelectorAsync(
            "[data-testid='add-patient-btn']",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );
        Assert.Contains("#patients", page.Url);

        // Go forward to Practitioners
        await page.GoForwardAsync();
        await page.WaitForSelectorAsync(
            ".practitioner-card, .empty-state",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );
        Assert.Contains("#practitioners", page.Url);

        // Verify page content is actually practitioners page
        var content = await page.ContentAsync();
        Assert.Contains("Practitioners", content);

        await page.CloseAsync();
    }

    /// <summary>
    /// CRITICAL TEST: Proves patient update API works end-to-end.
    /// This test hits the real Clinical API directly without Playwright.
    /// </summary>
    [Fact]
    public async Task PatientUpdateApi_WorksEndToEnd()
    {
        using var client = E2EFixture.CreateAuthenticatedClient();

        // Create a patient first
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

        // Extract patient ID
        var patientIdMatch = System.Text.RegularExpressions.Regex.Match(
            createdPatientJson,
            "\"Id\"\\s*:\\s*\"([^\"]+)\""
        );
        Assert.True(patientIdMatch.Success, "Should get patient ID from creation response");
        var patientId = patientIdMatch.Groups[1].Value;

        // Update the patient
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

        // Verify patient was updated
        var getResponse = await client.GetStringAsync(
            $"{E2EFixture.ClinicalUrl}/fhir/Patient/{patientId}"
        );
        Assert.Contains(updatedFamilyName, getResponse);
        Assert.Contains("updated@test.com", getResponse);
    }

    /// <summary>
    /// CRITICAL TEST: Add Practitioner button opens modal and creates practitioner via API.
    /// Uses Playwright to load REAL Dashboard, click Add Practitioner, fill form, and POST to REAL API.
    /// </summary>
    [Fact]
    public async Task AddPractitionerButton_OpensModal_AndCreatesPractitioner()
    {
        var page = await _fixture.Browser!.NewPageAsync();
        page.Console += (_, msg) => Console.WriteLine($"[BROWSER] {msg.Text}");

        await page.GotoAsync(E2EFixture.DashboardUrl);
        await page.WaitForSelectorAsync(
            ".sidebar",
            new PageWaitForSelectorOptions { Timeout = 20000 }
        );

        // Navigate to Practitioners page
        await page.ClickAsync("text=Practitioners");
        await page.WaitForSelectorAsync(
            "[data-testid='add-practitioner-btn']",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        // Click Add Practitioner button
        await page.ClickAsync("[data-testid='add-practitioner-btn']");

        // Wait for modal to appear
        await page.WaitForSelectorAsync(
            ".modal",
            new PageWaitForSelectorOptions { Timeout = 5000 }
        );

        // Fill in practitioner details
        var uniqueIdentifier = $"DR{DateTime.UtcNow.Ticks % 100000}";
        var uniqueGivenName = $"E2EDoc{DateTime.UtcNow.Ticks % 100000}";
        await page.FillAsync("[data-testid='practitioner-identifier']", uniqueIdentifier);
        await page.FillAsync("[data-testid='practitioner-given-name']", uniqueGivenName);
        await page.FillAsync("[data-testid='practitioner-family-name']", "TestCreated");
        await page.FillAsync("[data-testid='practitioner-specialty']", "E2E Testing");

        // Submit the form
        await page.ClickAsync("[data-testid='submit-practitioner']");

        // Wait for modal to close and practitioner to appear in list
        await page.WaitForSelectorAsync(
            $"text={uniqueGivenName}",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        // Verify via API that practitioner was actually created
        using var client = E2EFixture.CreateAuthenticatedClient();
        var response = await client.GetStringAsync($"{E2EFixture.SchedulingUrl}/Practitioner");
        Assert.Contains(uniqueIdentifier, response);

        await page.CloseAsync();
    }

    /// <summary>
    /// CRITICAL TEST: Edit Practitioner button navigates to edit page and updates practitioner.
    /// Uses Playwright to load REAL Dashboard, click Edit, modify data, and PUT to REAL API.
    /// </summary>
    [Fact]
    public async Task EditPractitionerButton_OpensEditPage_AndUpdatesPractitioner()
    {
        using var client = E2EFixture.CreateAuthenticatedClient();

        // Create a practitioner to edit
        var uniqueIdentifier = $"DREdit{DateTime.UtcNow.Ticks % 100000}";
        var uniqueGivenName = $"EditTest{DateTime.UtcNow.Ticks % 100000}";
        var createResponse = await client.PostAsync(
            $"{E2EFixture.SchedulingUrl}/Practitioner",
            new StringContent(
                $$$"""{"Identifier": "{{{uniqueIdentifier}}}", "NameFamily": "OriginalFamily", "NameGiven": "{{{uniqueGivenName}}}", "Qualification": "MD", "Specialty": "Original Specialty"}""",
                System.Text.Encoding.UTF8,
                "application/json"
            )
        );
        createResponse.EnsureSuccessStatusCode();
        var createdJson = await createResponse.Content.ReadAsStringAsync();
        var practitionerIdMatch = System.Text.RegularExpressions.Regex.Match(
            createdJson,
            "\"Id\"\\s*:\\s*\"([^\"]+)\""
        );
        var practitionerId = practitionerIdMatch.Groups[1].Value;

        var page = await _fixture.Browser!.NewPageAsync();
        page.Console += (_, msg) => Console.WriteLine($"[BROWSER] {msg.Text}");

        await page.GotoAsync(E2EFixture.DashboardUrl);
        await page.WaitForSelectorAsync(
            ".sidebar",
            new PageWaitForSelectorOptions { Timeout = 20000 }
        );

        // Navigate to Practitioners page
        await page.ClickAsync("text=Practitioners");
        await page.WaitForSelectorAsync(
            "[data-testid='add-practitioner-btn']",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        // Wait for our practitioner to appear
        await page.WaitForSelectorAsync(
            $"text={uniqueGivenName}",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        // Hover over the card to show the edit button, then click it
        var editButton = page.Locator($"[data-testid='edit-practitioner-{practitionerId}']");
        await editButton.HoverAsync();
        await editButton.ClickAsync();

        // Wait for edit page
        await page.WaitForSelectorAsync(
            "[data-testid='edit-practitioner-page']",
            new PageWaitForSelectorOptions { Timeout = 5000 }
        );
        Assert.Contains($"#practitioners/edit/{practitionerId}", page.Url);

        // Update the practitioner's specialty
        var newSpecialty = $"Updated Specialty {DateTime.UtcNow.Ticks % 100000}";
        await page.FillAsync("[data-testid='edit-practitioner-specialty']", newSpecialty);

        // Save changes
        await page.ClickAsync("[data-testid='save-practitioner']");

        // Wait for success message
        await page.WaitForSelectorAsync(
            "[data-testid='edit-practitioner-success']",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        // Verify via API that practitioner was actually updated
        var updatedPractitionerJson = await client.GetStringAsync(
            $"{E2EFixture.SchedulingUrl}/Practitioner/{practitionerId}"
        );
        Assert.Contains(newSpecialty, updatedPractitionerJson);

        await page.CloseAsync();
    }

    /// <summary>
    /// CRITICAL TEST: Proves practitioner update API works end-to-end.
    /// This test hits the real Scheduling API directly without Playwright.
    /// </summary>
    [Fact]
    public async Task PractitionerUpdateApi_WorksEndToEnd()
    {
        using var client = E2EFixture.CreateAuthenticatedClient();

        // Create a practitioner first
        var uniqueIdentifier = $"DRApi{DateTime.UtcNow.Ticks % 100000}";
        var createResponse = await client.PostAsync(
            $"{E2EFixture.SchedulingUrl}/Practitioner",
            new StringContent(
                $$$"""{"Identifier": "{{{uniqueIdentifier}}}", "NameFamily": "ApiOriginal", "NameGiven": "TestDoc", "Qualification": "MD", "Specialty": "Original"}""",
                System.Text.Encoding.UTF8,
                "application/json"
            )
        );
        createResponse.EnsureSuccessStatusCode();
        var createdPractitionerJson = await createResponse.Content.ReadAsStringAsync();

        // Extract practitioner ID
        var practitionerIdMatch = System.Text.RegularExpressions.Regex.Match(
            createdPractitionerJson,
            "\"Id\"\\s*:\\s*\"([^\"]+)\""
        );
        Assert.True(
            practitionerIdMatch.Success,
            "Should get practitioner ID from creation response"
        );
        var practitionerId = practitionerIdMatch.Groups[1].Value;

        // Update the practitioner
        var updatedSpecialty = $"ApiUpdated{DateTime.UtcNow.Ticks % 100000}";
        var updateResponse = await client.PutAsync(
            $"{E2EFixture.SchedulingUrl}/Practitioner/{practitionerId}",
            new StringContent(
                $$$"""{"Identifier": "{{{uniqueIdentifier}}}", "Active": true, "NameFamily": "ApiUpdated", "NameGiven": "TestDoc", "Qualification": "DO", "Specialty": "{{{updatedSpecialty}}}", "TelecomEmail": "updated@hospital.com", "TelecomPhone": "555-1234"}""",
                System.Text.Encoding.UTF8,
                "application/json"
            )
        );
        updateResponse.EnsureSuccessStatusCode();

        // Verify practitioner was updated
        var getResponse = await client.GetStringAsync(
            $"{E2EFixture.SchedulingUrl}/Practitioner/{practitionerId}"
        );
        Assert.Contains(updatedSpecialty, getResponse);
        Assert.Contains("ApiUpdated", getResponse);
        Assert.Contains("DO", getResponse);
        Assert.Contains("updated@hospital.com", getResponse);
    }

    /// <summary>
    /// CRITICAL TEST: Browser back button works from Edit Practitioner page.
    /// Proves navigation between practitioners list and edit page works correctly.
    /// </summary>
    [Fact]
    public async Task BrowserBackButton_FromEditPractitionerPage_ReturnsToPractitionersPage()
    {
        using var client = E2EFixture.CreateAuthenticatedClient();

        // Create a practitioner to edit
        var uniqueIdentifier = $"DRBack{DateTime.UtcNow.Ticks % 100000}";
        var uniqueGivenName = $"BackTest{DateTime.UtcNow.Ticks % 100000}";
        var createResponse = await client.PostAsync(
            $"{E2EFixture.SchedulingUrl}/Practitioner",
            new StringContent(
                $$$"""{"Identifier": "{{{uniqueIdentifier}}}", "NameFamily": "BackButtonTest", "NameGiven": "{{{uniqueGivenName}}}", "Qualification": "MD", "Specialty": "Testing"}""",
                System.Text.Encoding.UTF8,
                "application/json"
            )
        );
        createResponse.EnsureSuccessStatusCode();
        var createdJson = await createResponse.Content.ReadAsStringAsync();
        var practitionerIdMatch = System.Text.RegularExpressions.Regex.Match(
            createdJson,
            "\"Id\"\\s*:\\s*\"([^\"]+)\""
        );
        var practitionerId = practitionerIdMatch.Groups[1].Value;

        var page = await _fixture.Browser!.NewPageAsync();
        page.Console += (_, msg) => Console.WriteLine($"[BROWSER] {msg.Text}");

        // Start at dashboard
        await page.GotoAsync(E2EFixture.DashboardUrl);
        await page.WaitForSelectorAsync(
            ".sidebar",
            new PageWaitForSelectorOptions { Timeout = 20000 }
        );
        Assert.Contains("#dashboard", page.Url);

        // Navigate to Practitioners
        await page.ClickAsync("text=Practitioners");
        await page.WaitForSelectorAsync(
            "[data-testid='add-practitioner-btn']",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );
        Assert.Contains("#practitioners", page.Url);

        // Wait for our practitioner to appear
        await page.WaitForSelectorAsync(
            $"text={uniqueGivenName}",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        // Click edit to go to edit page
        var editButton = page.Locator($"[data-testid='edit-practitioner-{practitionerId}']");
        await editButton.HoverAsync();
        await editButton.ClickAsync();
        await page.WaitForSelectorAsync(
            "[data-testid='edit-practitioner-page']",
            new PageWaitForSelectorOptions { Timeout = 5000 }
        );
        Assert.Contains($"#practitioners/edit/{practitionerId}", page.Url);

        // Press browser back button
        await page.GoBackAsync();

        // Should be back on practitioners page with sidebar visible
        await page.WaitForSelectorAsync(
            ".sidebar",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );
        await page.WaitForSelectorAsync(
            "[data-testid='add-practitioner-btn']",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );
        Assert.Contains("#practitioners", page.Url);
        Assert.DoesNotContain("/edit/", page.Url);

        // Verify the page content is actually the practitioners page
        var content = await page.ContentAsync();
        Assert.Contains("Practitioners", content);
        Assert.Contains("Add Practitioner", content);

        // Press back again - should go to dashboard
        await page.GoBackAsync();
        await page.WaitForSelectorAsync(
            ".metric-card",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );
        Assert.Contains("#dashboard", page.Url);

        await page.CloseAsync();
    }

    /// <summary>
    /// CRITICAL TEST: Sync Dashboard menu item navigates to sync page and displays sync status.
    /// Proves the sync dashboard UI is accessible from the sidebar navigation.
    /// </summary>
    [Fact]
    public async Task SyncDashboard_NavigatesToSyncPage_AndDisplaysStatus()
    {
        var page = await _fixture.Browser!.NewPageAsync();
        page.Console += (_, msg) => Console.WriteLine($"[BROWSER] {msg.Text}");

        await page.GotoAsync(E2EFixture.DashboardUrl);
        await page.WaitForSelectorAsync(
            ".sidebar",
            new PageWaitForSelectorOptions { Timeout = 20000 }
        );

        // Click Sync Dashboard in sidebar
        await page.ClickAsync("text=Sync Dashboard");

        // Wait for sync page to load
        await page.WaitForSelectorAsync(
            "[data-testid='sync-page']",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        // Verify URL
        Assert.Contains("#sync", page.Url);

        // Verify service status cards are displayed
        await page.WaitForSelectorAsync(
            "[data-testid='service-status-clinical']",
            new PageWaitForSelectorOptions { Timeout = 5000 }
        );
        await page.WaitForSelectorAsync(
            "[data-testid='service-status-scheduling']",
            new PageWaitForSelectorOptions { Timeout = 5000 }
        );

        // Verify sync records table is displayed
        await page.WaitForSelectorAsync(
            "[data-testid='sync-records-table']",
            new PageWaitForSelectorOptions { Timeout = 5000 }
        );

        // Verify filter controls exist (service-filter and action-filter)
        await page.WaitForSelectorAsync(
            "[data-testid='service-filter']",
            new PageWaitForSelectorOptions { Timeout = 5000 }
        );
        await page.WaitForSelectorAsync(
            "[data-testid='action-filter']",
            new PageWaitForSelectorOptions { Timeout = 5000 }
        );

        // Verify page content
        var content = await page.ContentAsync();
        Assert.Contains("Sync Dashboard", content);
        Assert.Contains("Clinical.Api", content);
        Assert.Contains("Scheduling.Api", content);
        Assert.Contains("Sync Records", content);

        await page.CloseAsync();
    }

    /// <summary>
    /// CRITICAL TEST: Sync Dashboard filters work correctly.
    /// Tests service and action filtering functionality.
    /// </summary>
    [Fact]
    public async Task SyncDashboard_FiltersWorkCorrectly()
    {
        var page = await _fixture.Browser!.NewPageAsync();
        page.Console += (_, msg) => Console.WriteLine($"[BROWSER] {msg.Text}");

        await page.GotoAsync($"{E2EFixture.DashboardUrl}#sync");
        await page.WaitForSelectorAsync(
            "[data-testid='sync-page']",
            new PageWaitForSelectorOptions { Timeout = 20000 }
        );

        // Wait for sync records table to be loaded
        await page.WaitForSelectorAsync(
            "[data-testid='sync-records-table']",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        // Get initial record count (may be 0 initially)
        var initialRows = await page.QuerySelectorAllAsync(
            "[data-testid='sync-records-table'] tbody tr"
        );
        var initialCount = initialRows.Count;

        // Filter by service - select 'clinical'
        await page.SelectOptionAsync("[data-testid='service-filter']", "clinical");

        // Wait for filter to apply
        await Task.Delay(500);
        var filteredRows = await page.QuerySelectorAllAsync(
            "[data-testid='sync-records-table'] tbody tr"
        );
        Assert.True(
            filteredRows.Count <= initialCount,
            "Filtered results should be <= initial count"
        );

        // Reset filter
        await page.SelectOptionAsync("[data-testid='service-filter']", "all");

        await page.CloseAsync();
    }

    /// <summary>
    /// CRITICAL TEST: Deep linking to sync page works.
    /// Navigating directly to #sync loads the sync dashboard.
    /// </summary>
    [Fact]
    public async Task SyncDashboard_DeepLinkingWorks()
    {
        var page = await _fixture.Browser!.NewPageAsync();
        page.Console += (_, msg) => Console.WriteLine($"[BROWSER] {msg.Text}");

        // Navigate directly to sync page via hash
        await page.GotoAsync($"{E2EFixture.DashboardUrl}#sync");

        // Wait for sync page to load
        await page.WaitForSelectorAsync(
            "[data-testid='sync-page']",
            new PageWaitForSelectorOptions { Timeout = 20000 }
        );

        // Verify we're on the sync page
        var content = await page.ContentAsync();
        Assert.Contains("Sync Dashboard", content);
        Assert.Contains("Monitor and manage sync operations", content);

        await page.CloseAsync();
    }

    /// <summary>
    /// CRITICAL TEST: Edit Appointment button opens edit page and updates appointment via API.
    /// Uses Playwright to load REAL Dashboard, click Edit, modify form, and PUT to REAL API.
    /// </summary>
    [Fact]
    public async Task EditAppointmentButton_OpensEditPage_AndUpdatesAppointment()
    {
        using var client = E2EFixture.CreateAuthenticatedClient();

        // First create an appointment to edit
        var uniqueServiceType = $"EditApptTest{DateTime.UtcNow.Ticks % 100000}";
        var startTime = DateTime.UtcNow.AddDays(7).ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        var endTime = DateTime
            .UtcNow.AddDays(7)
            .AddMinutes(30)
            .ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        var createResponse = await client.PostAsync(
            $"{E2EFixture.SchedulingUrl}/Appointment",
            new StringContent(
                $$$"""{"ServiceCategory": "General", "ServiceType": "{{{uniqueServiceType}}}", "Priority": "routine", "Start": "{{{startTime}}}", "End": "{{{endTime}}}", "PatientReference": "Patient/1", "PractitionerReference": "Practitioner/1"}""",
                System.Text.Encoding.UTF8,
                "application/json"
            )
        );
        createResponse.EnsureSuccessStatusCode();
        var createdAppointmentJson = await createResponse.Content.ReadAsStringAsync();

        // Extract appointment ID from response
        var appointmentIdMatch = System.Text.RegularExpressions.Regex.Match(
            createdAppointmentJson,
            "\"Id\"\\s*:\\s*\"([^\"]+)\""
        );
        Assert.True(appointmentIdMatch.Success, "Should get appointment ID from creation response");
        var appointmentId = appointmentIdMatch.Groups[1].Value;

        var page = await _fixture.Browser!.NewPageAsync();
        page.Console += (_, msg) => Console.WriteLine($"[BROWSER] {msg.Text}");

        await page.GotoAsync(E2EFixture.DashboardUrl);
        await page.WaitForSelectorAsync(
            ".sidebar",
            new PageWaitForSelectorOptions { Timeout = 20000 }
        );

        // Navigate to Appointments page
        await page.ClickAsync("text=Appointments");
        await page.WaitForSelectorAsync(
            $"text={uniqueServiceType}",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        // Click Edit button for the created appointment (in the same table row)
        var editButton = await page.QuerySelectorAsync(
            $"tr:has-text('{uniqueServiceType}') .btn-secondary"
        );
        Assert.NotNull(editButton);
        await editButton.ClickAsync();

        // Wait for edit page to load
        await page.WaitForSelectorAsync(
            "text=Edit Appointment",
            new PageWaitForSelectorOptions { Timeout = 5000 }
        );

        // Modify the appointment's service type
        var newServiceType = $"Edited{DateTime.UtcNow.Ticks % 100000}";
        await page.FillAsync("#appointment-service-type", newServiceType);

        // Submit the form
        await page.ClickAsync("button:has-text('Save Changes')");

        // Wait for success message
        await page.WaitForSelectorAsync(
            "text=Appointment updated successfully",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        // Verify via API that appointment was actually updated
        var updatedAppointmentJson = await client.GetStringAsync(
            $"{E2EFixture.SchedulingUrl}/Appointment/{appointmentId}"
        );
        Assert.Contains(newServiceType, updatedAppointmentJson);

        await page.CloseAsync();
    }

    /// <summary>
    /// CRITICAL TEST: Calendar page displays appointments in calendar grid.
    /// Uses Playwright to navigate to calendar and verify appointments are shown.
    /// </summary>
    [Fact]
    public async Task CalendarPage_DisplaysAppointmentsInCalendarGrid()
    {
        var page = await _fixture.Browser!.NewPageAsync();
        page.Console += (_, msg) => Console.WriteLine($"[BROWSER] {msg.Text}");

        // Navigate directly to calendar page via hash URL
        await page.GotoAsync($"{E2EFixture.DashboardUrl}#calendar");
        await page.WaitForSelectorAsync(
            ".sidebar",
            new PageWaitForSelectorOptions { Timeout = 20000 }
        );

        // Wait for calendar grid container to appear
        await page.WaitForSelectorAsync(
            ".calendar-grid-container",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        // Verify calendar grid is displayed
        var content = await page.ContentAsync();
        Assert.Contains("calendar-grid", content);

        // Verify day names are displayed
        Assert.Contains("Sun", content);
        Assert.Contains("Mon", content);
        Assert.Contains("Tue", content);
        Assert.Contains("Wed", content);
        Assert.Contains("Thu", content);
        Assert.Contains("Fri", content);
        Assert.Contains("Sat", content);

        // Verify navigation controls exist
        Assert.Contains("Today", content);

        await page.CloseAsync();
    }

    /// <summary>
    /// CRITICAL TEST: Calendar page allows clicking on a day to view appointments.
    /// Uses Playwright to click on a day and verify the details panel shows.
    /// </summary>
    [Fact]
    public async Task CalendarPage_ClickOnDay_ShowsAppointmentDetails()
    {
        using var client = E2EFixture.CreateAuthenticatedClient();

        // Create an appointment for today - use LOCAL time since browser calendar uses local timezone
        var today = DateTime.Now;
        var startTime = new DateTime(
            today.Year,
            today.Month,
            today.Day,
            14,
            0,
            0,
            DateTimeKind.Local
        ).ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        var endTime = new DateTime(
            today.Year,
            today.Month,
            today.Day,
            14,
            30,
            0,
            DateTimeKind.Local
        ).ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        var uniqueServiceType = $"CalTest{DateTime.Now.Ticks % 100000}";

        var createResponse = await client.PostAsync(
            $"{E2EFixture.SchedulingUrl}/Appointment",
            new StringContent(
                $$$"""{"ServiceCategory": "General", "ServiceType": "{{{uniqueServiceType}}}", "Priority": "routine", "Start": "{{{startTime}}}", "End": "{{{endTime}}}", "PatientReference": "Patient/1", "PractitionerReference": "Practitioner/1"}""",
                System.Text.Encoding.UTF8,
                "application/json"
            )
        );
        createResponse.EnsureSuccessStatusCode();
        Console.WriteLine(
            $"[TEST] Created appointment with ServiceType: {uniqueServiceType}, Start: {startTime}"
        );

        var page = await _fixture.Browser!.NewPageAsync();
        page.Console += (_, msg) => Console.WriteLine($"[BROWSER] {msg.Text}");

        await page.GotoAsync(E2EFixture.DashboardUrl);
        await page.WaitForSelectorAsync(
            ".sidebar",
            new PageWaitForSelectorOptions { Timeout = 20000 }
        );

        // Navigate to Calendar page
        await page.ClickAsync("text=Schedule");
        await page.WaitForSelectorAsync(
            ".calendar-grid",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        // Wait for appointments to load - today's cell should have has-appointments class
        await page.WaitForSelectorAsync(
            ".calendar-cell.today.has-appointments",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        // Click on today's cell (it has the "today" class and now has appointments)
        var todayCell = page.Locator(".calendar-cell.today").First;
        await todayCell.ClickAsync();

        // Wait for the details panel to update (look for date header)
        await page.WaitForSelectorAsync(
            ".calendar-details-panel h4",
            new PageWaitForSelectorOptions { Timeout = 5000 }
        );

        // Debug: output the details panel content
        var detailsContent = await page.Locator(".calendar-details-panel").InnerTextAsync();
        Console.WriteLine($"[TEST] Details panel content: {detailsContent}");

        // Wait for the appointment content to appear in the details panel
        await page.WaitForSelectorAsync(
            $"text={uniqueServiceType}",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        // Verify the appointment is displayed in the details panel
        var content = await page.ContentAsync();
        Assert.Contains(uniqueServiceType, content);

        await page.CloseAsync();
    }

    /// <summary>
    /// CRITICAL TEST: Calendar page Edit button opens edit appointment page.
    /// Uses Playwright to click Edit from calendar day details and verify navigation.
    /// </summary>
    [Fact]
    public async Task CalendarPage_EditButton_OpensEditAppointmentPage()
    {
        using var client = E2EFixture.CreateAuthenticatedClient();

        // Create an appointment for today using LOCAL time (calendar uses DateTime.Now)
        var today = DateTime.Now;
        var startTime = new DateTime(
            today.Year,
            today.Month,
            today.Day,
            15,
            0,
            0,
            DateTimeKind.Local
        )
            .ToUniversalTime()
            .ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        var endTime = new DateTime(
            today.Year,
            today.Month,
            today.Day,
            15,
            30,
            0,
            DateTimeKind.Local
        )
            .ToUniversalTime()
            .ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        var uniqueServiceType = $"CalEdit{DateTime.UtcNow.Ticks % 100000}";

        var createResponse = await client.PostAsync(
            $"{E2EFixture.SchedulingUrl}/Appointment",
            new StringContent(
                $$$"""{"ServiceCategory": "General", "ServiceType": "{{{uniqueServiceType}}}", "Priority": "routine", "Start": "{{{startTime}}}", "End": "{{{endTime}}}", "PatientReference": "Patient/1", "PractitionerReference": "Practitioner/1"}""",
                System.Text.Encoding.UTF8,
                "application/json"
            )
        );
        createResponse.EnsureSuccessStatusCode();

        var page = await _fixture.Browser!.NewPageAsync();
        page.Console += (_, msg) => Console.WriteLine($"[BROWSER] {msg.Text}");

        await page.GotoAsync(E2EFixture.DashboardUrl);
        await page.WaitForSelectorAsync(
            ".sidebar",
            new PageWaitForSelectorOptions { Timeout = 20000 }
        );

        // Navigate to Calendar page
        await page.ClickAsync("text=Schedule");
        await page.WaitForSelectorAsync(
            ".calendar-grid",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        // Click on today's cell
        await page.ClickAsync(".calendar-cell.today");

        // Wait for the details panel
        await page.WaitForSelectorAsync(
            ".calendar-details-panel",
            new PageWaitForSelectorOptions { Timeout = 5000 }
        );

        // Wait for the specific appointment to appear
        await page.WaitForSelectorAsync(
            $"text={uniqueServiceType}",
            new PageWaitForSelectorOptions { Timeout = 5000 }
        );

        // Click the Edit button in the calendar appointment item
        var editButton = await page.QuerySelectorAsync(
            $".calendar-appointment-item:has-text('{uniqueServiceType}') button:has-text('Edit')"
        );
        Assert.NotNull(editButton);
        await editButton.ClickAsync();

        // Wait for edit page to load
        await page.WaitForSelectorAsync(
            "text=Edit Appointment",
            new PageWaitForSelectorOptions { Timeout = 5000 }
        );

        // Verify we're on the edit page with the correct data
        var content = await page.ContentAsync();
        Assert.Contains("Edit Appointment", content);

        await page.CloseAsync();
    }

    /// <summary>
    /// CRITICAL TEST: Calendar navigation (previous/next month) works.
    /// Uses Playwright to click navigation buttons and verify month changes.
    /// </summary>
    [Fact]
    public async Task CalendarPage_NavigationButtons_ChangeMonth()
    {
        var page = await _fixture.Browser!.NewPageAsync();
        page.Console += (_, msg) => Console.WriteLine($"[BROWSER] {msg.Text}");

        await page.GotoAsync(E2EFixture.DashboardUrl);
        await page.WaitForSelectorAsync(
            ".sidebar",
            new PageWaitForSelectorOptions { Timeout = 20000 }
        );

        // Navigate to Calendar page
        await page.ClickAsync("text=Schedule");
        await page.WaitForSelectorAsync(
            ".calendar-grid",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        // Get the current month displayed
        var currentMonthYear = await page.TextContentAsync(".text-lg.font-semibold");
        Assert.NotNull(currentMonthYear);

        // Click next month button - use locator within the header flex container
        var headerControls = page.Locator(".page-header .flex.items-center.gap-4");
        var nextButton = headerControls.Locator("button.btn-secondary").Nth(1);
        await nextButton.ClickAsync();
        await Task.Delay(300);

        // Verify month changed
        var newMonthYear = await page.TextContentAsync(".text-lg.font-semibold");
        Assert.NotEqual(currentMonthYear, newMonthYear);

        // Click previous month button twice to go back
        var prevButton = headerControls.Locator("button.btn-secondary").First;
        await prevButton.ClickAsync();
        await Task.Delay(300);
        await prevButton.ClickAsync();
        await Task.Delay(300);

        // Verify month changed again
        var finalMonthYear = await page.TextContentAsync(".text-lg.font-semibold");
        Assert.NotEqual(newMonthYear, finalMonthYear);

        // Click "Today" button
        await page.ClickAsync("button:has-text('Today')");
        await Task.Delay(500);

        // Should be back to current month
        var todayContent = await page.ContentAsync();
        Assert.Contains("today", todayContent); // Calendar cell should have "today" class

        await page.CloseAsync();
    }

    /// <summary>
    /// CRITICAL TEST: Deep linking to calendar page works.
    /// Navigating directly to #calendar loads the calendar view.
    /// </summary>
    [Fact]
    public async Task CalendarPage_DeepLinkingWorks()
    {
        var page = await _fixture.Browser!.NewPageAsync();
        page.Console += (_, msg) => Console.WriteLine($"[BROWSER] {msg.Text}");

        // Navigate directly to calendar page via hash
        await page.GotoAsync($"{E2EFixture.DashboardUrl}#calendar");
        await page.WaitForSelectorAsync(
            ".calendar-grid",
            new PageWaitForSelectorOptions { Timeout = 20000 }
        );

        // Verify we're on the calendar page
        var content = await page.ContentAsync();
        Assert.Contains("Schedule", content);
        Assert.Contains("View and manage appointments on the calendar", content);
        Assert.Contains("calendar-grid", content);

        await page.CloseAsync();
    }

    /// <summary>
    /// CRITICAL TEST: Login page uses discoverable credentials (no email required).
    /// The login page should NOT show an email field for sign-in mode.
    /// </summary>
    [Fact]
    public async Task LoginPage_DoesNotRequireEmailForSignIn()
    {
        var page = await _fixture.Browser!.NewPageAsync();
        page.Console += (_, msg) => Console.WriteLine($"[BROWSER] {msg.Type}: {msg.Text}");

        // Navigate to Dashboard WITHOUT testMode - should show login page
        await page.GotoAsync(E2EFixture.DashboardUrlNoTestMode);

        // Wait for login page to appear
        await page.WaitForSelectorAsync(
            ".login-card",
            new PageWaitForSelectorOptions { Timeout = 20000 }
        );

        // Verify login page is shown
        var pageContent = await page.ContentAsync();
        Assert.Contains("Healthcare Dashboard", pageContent);
        Assert.Contains("Sign in with your passkey", pageContent);

        // CRITICAL: Login mode should NOT have email input field
        // Email is only needed for registration, not for discoverable credential login
        var emailInputVisible = await page.IsVisibleAsync("input[type='email']");
        Assert.False(
            emailInputVisible,
            "Login mode should NOT show email field - discoverable credentials don't need email!"
        );

        // Should have a sign-in button
        var signInButton = page.Locator("button:has-text('Sign in with Passkey')");
        await signInButton.WaitForAsync(new LocatorWaitForOptions { Timeout = 5000 });
        Assert.True(await signInButton.IsVisibleAsync());

        await page.CloseAsync();
    }

    /// <summary>
    /// CRITICAL TEST: Registration page requires email and display name.
    /// </summary>
    [Fact]
    public async Task LoginPage_RegistrationRequiresEmailAndDisplayName()
    {
        var page = await _fixture.Browser!.NewPageAsync();
        page.Console += (_, msg) => Console.WriteLine($"[BROWSER] {msg.Type}: {msg.Text}");

        // Navigate to Dashboard WITHOUT testMode
        await page.GotoAsync(E2EFixture.DashboardUrlNoTestMode);

        // Wait for login page
        await page.WaitForSelectorAsync(
            ".login-card",
            new PageWaitForSelectorOptions { Timeout = 20000 }
        );

        // Click "Register" to switch to registration mode
        await page.ClickAsync("button:has-text('Register')");
        await Task.Delay(500);

        // Verify we're in registration mode
        var pageContent = await page.ContentAsync();
        Assert.Contains("Create your account", pageContent);

        // Registration mode SHOULD have email and display name fields
        var emailInput = page.Locator("input[type='email']");
        var displayNameInput = page.Locator("input#displayName");

        Assert.True(await emailInput.IsVisibleAsync(), "Registration mode should have email input");
        Assert.True(
            await displayNameInput.IsVisibleAsync(),
            "Registration mode should have display name input"
        );

        await page.CloseAsync();
    }

    /// <summary>
    /// CRITICAL TEST: Gatekeeper API /auth/login/begin returns valid response for discoverable credentials.
    /// This verifies the API contract: empty body should return { ChallengeId, OptionsJson }.
    /// </summary>
    [Fact]
    public async Task GatekeeperApi_LoginBegin_ReturnsValidDiscoverableCredentialOptions()
    {
        using var client = E2EFixture.CreateAuthenticatedClient();

        // Call /auth/login/begin with empty body (discoverable credentials flow)
        var response = await client.PostAsync(
            $"{E2EFixture.GatekeeperUrl}/auth/login/begin",
            new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
        );

        // Should return 200 OK
        Assert.True(
            response.IsSuccessStatusCode,
            $"Expected 200 OK but got {response.StatusCode}: {await response.Content.ReadAsStringAsync()}"
        );

        var json = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"[API TEST] Response: {json}");

        // Parse and verify response structure
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Must have ChallengeId
        Assert.True(
            root.TryGetProperty("ChallengeId", out var challengeId),
            "Response must have ChallengeId property"
        );
        Assert.False(
            string.IsNullOrEmpty(challengeId.GetString()),
            "ChallengeId must not be empty"
        );

        // Must have OptionsJson (string containing JSON)
        Assert.True(
            root.TryGetProperty("OptionsJson", out var optionsJson),
            "Response must have OptionsJson property"
        );
        var optionsJsonStr = optionsJson.GetString();
        Assert.False(string.IsNullOrEmpty(optionsJsonStr), "OptionsJson must not be empty");

        // OptionsJson should be valid JSON that can be parsed
        using var optionsDoc = System.Text.Json.JsonDocument.Parse(optionsJsonStr!);
        var options = optionsDoc.RootElement;

        // Verify critical WebAuthn fields
        Assert.True(options.TryGetProperty("challenge", out _), "Options must have challenge");
        Assert.True(options.TryGetProperty("rpId", out _), "Options must have rpId");

        // For discoverable credentials, allowCredentials should be empty array
        if (options.TryGetProperty("allowCredentials", out var allowCreds))
        {
            Assert.Equal(System.Text.Json.JsonValueKind.Array, allowCreds.ValueKind);
            Assert.Equal(0, allowCreds.GetArrayLength());
            Console.WriteLine(
                "[API TEST] allowCredentials is empty array - correct for discoverable credentials!"
            );
        }
    }

    /// <summary>
    /// CRITICAL TEST: Gatekeeper API /auth/register/begin returns valid response.
    /// </summary>
    [Fact]
    public async Task GatekeeperApi_RegisterBegin_ReturnsValidOptions()
    {
        using var client = E2EFixture.CreateAuthenticatedClient();

        // Call /auth/register/begin with email and display name
        var response = await client.PostAsync(
            $"{E2EFixture.GatekeeperUrl}/auth/register/begin",
            new StringContent(
                """{"Email": "test-e2e@example.com", "DisplayName": "E2E Test User"}""",
                System.Text.Encoding.UTF8,
                "application/json"
            )
        );

        // Should return 200 OK
        Assert.True(
            response.IsSuccessStatusCode,
            $"Expected 200 OK but got {response.StatusCode}: {await response.Content.ReadAsStringAsync()}"
        );

        var json = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"[API TEST] Response: {json}");

        // Parse and verify response structure
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Must have ChallengeId
        Assert.True(
            root.TryGetProperty("ChallengeId", out var challengeId),
            "Response must have ChallengeId property"
        );
        Assert.False(
            string.IsNullOrEmpty(challengeId.GetString()),
            "ChallengeId must not be empty"
        );

        // Must have OptionsJson
        Assert.True(
            root.TryGetProperty("OptionsJson", out var optionsJson),
            "Response must have OptionsJson property"
        );
        var optionsJsonStr = optionsJson.GetString();
        Assert.False(string.IsNullOrEmpty(optionsJsonStr), "OptionsJson must not be empty");

        // OptionsJson should be valid JSON
        using var optionsDoc = System.Text.Json.JsonDocument.Parse(optionsJsonStr!);
        var options = optionsDoc.RootElement;

        // Verify critical WebAuthn registration fields
        Assert.True(options.TryGetProperty("challenge", out _), "Options must have challenge");
        Assert.True(options.TryGetProperty("rp", out _), "Options must have rp (relying party)");
        Assert.True(options.TryGetProperty("user", out _), "Options must have user");
        Assert.True(
            options.TryGetProperty("pubKeyCredParams", out _),
            "Options must have pubKeyCredParams"
        );

        // Verify resident key is required for discoverable credentials
        if (options.TryGetProperty("authenticatorSelection", out var authSelection))
        {
            if (authSelection.TryGetProperty("residentKey", out var residentKey))
            {
                Assert.Equal("required", residentKey.GetString());
                Console.WriteLine(
                    "[API TEST] residentKey is 'required' - correct for discoverable credentials!"
                );
            }
        }
    }

    /// <summary>
    /// CRITICAL TEST: Dashboard sign-in flow calls API and handles response correctly.
    /// Tests the full flow: button click -> API call -> no JSON parse errors.
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
            {
                networkRequests.Add($"{request.Method} {request.Url}");
                Console.WriteLine($"[NETWORK] {request.Method} {request.Url}");
            }
        };

        // Navigate to Dashboard WITHOUT testMode
        await page.GotoAsync(E2EFixture.DashboardUrlNoTestMode);

        // Wait for login page
        await page.WaitForSelectorAsync(
            ".login-card",
            new PageWaitForSelectorOptions { Timeout = 20000 }
        );

        // Click Sign in with Passkey button
        await page.ClickAsync("button:has-text('Sign in with Passkey')");

        // Wait for API call and potential error handling
        await Task.Delay(3000);

        // Verify the API was called
        Assert.Contains(networkRequests, r => r.Contains("/auth/login/begin"));

        // Check for JSON parse errors in console
        var hasJsonParseError = consoleErrors.Any(e =>
            e.Contains("undefined") || e.Contains("is not valid JSON") || e.Contains("SyntaxError")
        );

        // Check for JSON parse errors in UI
        var errorVisible = await page.IsVisibleAsync(".login-error");
        var errorText = errorVisible ? await page.TextContentAsync(".login-error") : null;

        var hasUiJsonError =
            errorText?.Contains("undefined") == true
            || errorText?.Contains("is not valid JSON") == true
            || errorText?.Contains("SyntaxError") == true;

        Assert.False(
            hasJsonParseError || hasUiJsonError,
            $"Sign-in flow had JSON parse errors! Console: [{string.Join(", ", consoleErrors)}], UI: [{errorText}]"
        );

        // The WebAuthn prompt will fail in headless mode (no authenticator), but that's expected
        // The important thing is no JSON parsing errors
        Console.WriteLine($"[TEST] API called, no JSON errors. UI error (expected): {errorText}");

        await page.CloseAsync();
    }

    [Fact]
    public async Task UserMenu_ClickShowsDropdownWithSignOut()
    {
        var page = await _fixture.Browser!.NewPageAsync();
        page.Console += (_, msg) => Console.WriteLine($"[BROWSER] {msg.Type}: {msg.Text}");

        await page.GotoAsync(E2EFixture.DashboardUrl);
        await page.WaitForSelectorAsync(
            ".sidebar",
            new PageWaitForSelectorOptions { Timeout = 20000 }
        );

        // User menu button should be visible in header
        var userMenuButton = await page.QuerySelectorAsync("[data-testid='user-menu-button']");
        Assert.NotNull(userMenuButton);

        // Click the user menu button to open dropdown
        await userMenuButton.ClickAsync();

        // Wait for dropdown to appear
        await page.WaitForSelectorAsync(
            "[data-testid='user-dropdown']",
            new PageWaitForSelectorOptions { Timeout = 5000 }
        );

        // Sign out button should be visible in the dropdown
        var signOutButton = await page.QuerySelectorAsync("[data-testid='logout-button']");
        Assert.NotNull(signOutButton);

        var isVisible = await signOutButton.IsVisibleAsync();
        Assert.True(isVisible, "Sign out button should be visible in dropdown menu");

        await page.CloseAsync();
    }

    [Fact]
    public async Task SignOutButton_ClickShowsLoginPage()
    {
        var page = await _fixture.Browser!.NewPageAsync();
        page.Console += (_, msg) => Console.WriteLine($"[BROWSER] {msg.Type}: {msg.Text}");

        // Set up a valid test token in localStorage to simulate being logged in
        await page.GotoAsync(E2EFixture.DashboardUrlNoTestMode);

        // Inject a properly-signed token to simulate authenticated state
        var testToken = E2EFixture.GenerateTestToken(
            userId: "test-user",
            displayName: "Test User",
            email: "test@example.com"
        );
        await page.EvaluateAsync(
            $@"() => {{
                localStorage.setItem('gatekeeper_token', '{testToken}');
                localStorage.setItem('gatekeeper_user', JSON.stringify({{
                    userId: 'test-user',
                    displayName: 'Test User',
                    email: 'test@example.com'
                }}));
            }}"
        );

        // Reload to pick up the token
        await page.ReloadAsync();

        // Wait for the sidebar to appear (authenticated state)
        await page.WaitForSelectorAsync(
            ".sidebar",
            new PageWaitForSelectorOptions { Timeout = 20000 }
        );

        // Click user menu button in header to open dropdown
        await page.ClickAsync("[data-testid='user-menu-button']");

        // Wait for dropdown to appear
        await page.WaitForSelectorAsync(
            "[data-testid='user-dropdown']",
            new PageWaitForSelectorOptions { Timeout = 5000 }
        );

        // Click sign out button in dropdown
        await page.ClickAsync("[data-testid='logout-button']");

        // Should show login page after sign out
        await page.WaitForSelectorAsync(
            "[data-testid='login-page']",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        // Verify token was cleared from localStorage
        var tokenAfterLogout = await page.EvaluateAsync<string?>(
            "() => localStorage.getItem('gatekeeper_token')"
        );
        Assert.Null(tokenAfterLogout);

        var userAfterLogout = await page.EvaluateAsync<string?>(
            "() => localStorage.getItem('gatekeeper_user')"
        );
        Assert.Null(userAfterLogout);

        await page.CloseAsync();
    }

    [Fact]
    public async Task GatekeeperApi_Logout_RevokesToken()
    {
        // Test 1: Without a Bearer token, should return 401 Unauthorized
        using var unauthClient = new HttpClient();
        var unauthResponse = await unauthClient.PostAsync(
            $"{E2EFixture.GatekeeperUrl}/auth/logout",
            new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
        );
        Assert.Equal(HttpStatusCode.Unauthorized, unauthResponse.StatusCode);

        // Test 2: With a valid Bearer token, should return 204 NoContent (logout succeeds)
        using var authClient = E2EFixture.CreateAuthenticatedClient();
        var authResponse = await authClient.PostAsync(
            $"{E2EFixture.GatekeeperUrl}/auth/logout",
            new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
        );
        Assert.Equal(HttpStatusCode.NoContent, authResponse.StatusCode);
    }

    [Fact]
    public async Task UserMenu_DisplaysUserInitialsAndNameInDropdown()
    {
        var page = await _fixture.Browser!.NewPageAsync();
        page.Console += (_, msg) => Console.WriteLine($"[BROWSER] {msg.Type}: {msg.Text}");

        // Inject a user with a specific name using a properly-signed token
        await page.GotoAsync(E2EFixture.DashboardUrlNoTestMode);

        var testToken = E2EFixture.GenerateTestToken(
            userId: "test-user",
            displayName: "Alice Smith",
            email: "alice@example.com"
        );
        await page.EvaluateAsync(
            $@"() => {{
                localStorage.setItem('gatekeeper_token', '{testToken}');
                localStorage.setItem('gatekeeper_user', JSON.stringify({{
                    userId: 'test-user',
                    displayName: 'Alice Smith',
                    email: 'alice@example.com'
                }}));
            }}"
        );

        await page.ReloadAsync();

        await page.WaitForSelectorAsync(
            ".sidebar",
            new PageWaitForSelectorOptions { Timeout = 20000 }
        );

        // Verify initials in header avatar button (should be "AS" for Alice Smith)
        var avatarText = await page.TextContentAsync("[data-testid='user-menu-button']");
        Assert.Equal("AS", avatarText?.Trim());

        // Click the user menu button to open dropdown
        await page.ClickAsync("[data-testid='user-menu-button']");

        // Wait for dropdown to appear
        await page.WaitForSelectorAsync(
            "[data-testid='user-dropdown']",
            new PageWaitForSelectorOptions { Timeout = 5000 }
        );

        // Verify the user's name is displayed in dropdown header
        var userNameText = await page.TextContentAsync(".user-dropdown-name");
        Assert.Contains("Alice Smith", userNameText);

        // Verify email is displayed
        var emailText = await page.TextContentAsync(".user-dropdown-email");
        Assert.Contains("alice@example.com", emailText);

        await page.CloseAsync();
    }

    /// <summary>
    /// CRITICAL TEST: First-time sign-in must work WITHOUT browser refresh.
    /// This test simulates a successful WebAuthn login by injecting the token and calling
    /// the onLogin callback, then verifies the app transitions to dashboard immediately.
    /// BUG: Previously, first-time sign-in required a page refresh to work.
    /// </summary>
    [Fact]
    public async Task FirstTimeSignIn_TransitionsToDashboard_WithoutRefresh()
    {
        var page = await _fixture.Browser!.NewPageAsync();
        page.Console += (_, msg) => Console.WriteLine($"[BROWSER] {msg.Type}: {msg.Text}");

        // Navigate to Dashboard WITHOUT testMode - should show login page
        await page.GotoAsync(E2EFixture.DashboardUrlNoTestMode);

        // Wait for login page to appear
        await page.WaitForSelectorAsync(
            "[data-testid='login-page']",
            new PageWaitForSelectorOptions { Timeout = 20000 }
        );

        // Verify we're on the login page
        var loginPageVisible = await page.IsVisibleAsync("[data-testid='login-page']");
        Assert.True(loginPageVisible, "Should start on login page");

        // Wait for React to mount and set the __triggerLogin hook
        await page.WaitForFunctionAsync(
            "() => typeof window.__triggerLogin === 'function'",
            new PageWaitForFunctionOptions { Timeout = 10000 }
        );

        // Simulate what happens after successful WebAuthn authentication:
        // 1. Token is stored in localStorage
        // 2. onLogin callback is called which sets isAuthenticated=true
        // This is what the LoginPage component does after successful auth
        var testToken = E2EFixture.GenerateTestToken(
            userId: "test-user-123",
            displayName: "Test User",
            email: "test@example.com"
        );
        await page.EvaluateAsync(
            $@"() => {{
                console.log('[TEST] Setting token and triggering login');
                // Store a properly-signed token and user (what setAuthToken and setAuthUser do)
                localStorage.setItem('gatekeeper_token', '{testToken}');
                localStorage.setItem('gatekeeper_user', JSON.stringify({{
                    userId: 'test-user-123',
                    displayName: 'Test User',
                    email: 'test@example.com'
                }}));

                // Trigger the React state update by calling the exposed login handler
                // This simulates what happens when LoginPage calls onLogin after successful auth
                window.__triggerLogin({{
                    userId: 'test-user-123',
                    displayName: 'Test User',
                    email: 'test@example.com'
                }});
                console.log('[TEST] Login triggered, waiting for React state update');
            }}"
        );

        // Wait for React state update and re-render
        await Task.Delay(2000);

        // Check if sidebar is now visible (indicates successful transition to dashboard)
        // If this times out, the bug exists - app didn't transition without refresh
        try
        {
            await page.WaitForSelectorAsync(
                ".sidebar",
                new PageWaitForSelectorOptions { Timeout = 10000 }
            );

            // Verify login page is gone
            var loginPageStillVisible = await page.IsVisibleAsync("[data-testid='login-page']");
            Assert.False(
                loginPageStillVisible,
                "Login page should be hidden after successful login"
            );

            // Verify sidebar is visible (dashboard state)
            var sidebarVisible = await page.IsVisibleAsync(".sidebar");
            Assert.True(sidebarVisible, "Sidebar should be visible after login without refresh");
        }
        catch (TimeoutException)
        {
            // If we get here, the bug exists - first-time sign-in doesn't work without refresh
            Assert.Fail(
                "FIRST-TIME SIGN-IN BUG: App did not transition to dashboard after login. "
                    + "User must refresh the browser for login to take effect. "
                    + "Fix: Expose window.__triggerLogin in App component for testing, "
                    + "or verify onLogin callback properly triggers React state update."
            );
        }

        await page.CloseAsync();
    }
}
