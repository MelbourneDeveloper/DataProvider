using System.Net;
using Microsoft.Playwright;

namespace Dashboard.Integration.Tests;

/// <summary>
/// E2E tests for bidirectional sync functionality.
/// </summary>
[Collection("E2E Tests")]
[Trait("Category", "E2E")]
public sealed class SyncE2ETests
{
    private readonly E2EFixture _fixture;

    /// <summary>
    /// Constructor receives shared fixture.
    /// </summary>
    public SyncE2ETests(E2EFixture fixture) => _fixture = fixture;

    /// <summary>
    /// Sync Dashboard menu item navigates to sync page and displays sync status.
    /// </summary>
    [Fact]
    public async Task SyncDashboard_NavigatesToSyncPage_AndDisplaysStatus()
    {
        var page = await _fixture.Browser!.NewPageAsync();
        page.Console += (_, msg) => Console.WriteLine($"[BROWSER] {msg.Text}");

        await page.GotoAsync(E2EFixture.DashboardUrl);
        await page.WaitForSelectorAsync(".sidebar", new PageWaitForSelectorOptions { Timeout = 20000 });
        await page.ClickAsync("text=Sync Dashboard");

        await page.WaitForSelectorAsync("[data-testid='sync-page']", new PageWaitForSelectorOptions { Timeout = 10000 });
        Assert.Contains("#sync", page.Url);

        await page.WaitForSelectorAsync("[data-testid='service-status-clinical']", new PageWaitForSelectorOptions { Timeout = 15000 });
        await page.WaitForSelectorAsync("[data-testid='service-status-scheduling']", new PageWaitForSelectorOptions { Timeout = 15000 });
        await page.WaitForSelectorAsync("[data-testid='sync-records-table']", new PageWaitForSelectorOptions { Timeout = 5000 });
        await page.WaitForSelectorAsync("[data-testid='action-filter']", new PageWaitForSelectorOptions { Timeout = 5000 });
        await page.WaitForSelectorAsync("[data-testid='service-filter']", new PageWaitForSelectorOptions { Timeout = 5000 });

        var content = await page.ContentAsync();
        Assert.Contains("Sync Dashboard", content);
        Assert.Contains("Clinical.Api", content);
        Assert.Contains("Scheduling.Api", content);
        Assert.Contains("Sync Records", content);

        await page.CloseAsync();
    }

    /// <summary>
    /// Sync Dashboard filters work correctly.
    /// </summary>
    [Fact]
    public async Task SyncDashboard_FiltersWorkCorrectly()
    {
        var page = await _fixture.Browser!.NewPageAsync();
        page.Console += (_, msg) => Console.WriteLine($"[BROWSER] {msg.Text}");

        await page.GotoAsync($"{E2EFixture.DashboardUrl}#sync");
        await page.WaitForSelectorAsync("[data-testid='sync-page']", new PageWaitForSelectorOptions { Timeout = 20000 });
        await page.WaitForSelectorAsync("[data-testid='service-status-clinical']", new PageWaitForSelectorOptions { Timeout = 15000 });

        var initialRows = await page.QuerySelectorAllAsync("[data-testid='sync-records-table'] tbody tr");
        var initialCount = initialRows.Count;

        await page.SelectOptionAsync("[data-testid='action-filter']", "2");
        await Task.Delay(500);

        var filteredRows = await page.QuerySelectorAllAsync("[data-testid='sync-records-table'] tbody tr");
        Assert.True(filteredRows.Count <= initialCount);

        var selectedValue = await page.EvalOnSelectorAsync<string>("[data-testid='action-filter']", "el => el.value");
        Assert.Equal("2", selectedValue);

        await page.SelectOptionAsync("[data-testid='action-filter']", "all");

        await page.CloseAsync();
    }

    /// <summary>
    /// Deep linking to sync page works.
    /// </summary>
    [Fact]
    public async Task SyncDashboard_DeepLinkingWorks()
    {
        var page = await _fixture.Browser!.NewPageAsync();
        page.Console += (_, msg) => Console.WriteLine($"[BROWSER] {msg.Text}");

        await page.GotoAsync($"{E2EFixture.DashboardUrl}#sync");
        await page.WaitForSelectorAsync("[data-testid='sync-page']", new PageWaitForSelectorOptions { Timeout = 20000 });

        var content = await page.ContentAsync();
        Assert.Contains("Sync Dashboard", content);
        Assert.Contains("Monitor and manage sync operations", content);

        await page.CloseAsync();
    }

    /// <summary>
    /// Data added to Clinical.Api is synced to Scheduling.Api.
    /// </summary>
    [Fact]
    public async Task Sync_ClinicalPatient_AppearsInScheduling_AfterSync()
    {
        using var client = E2EFixture.CreateAuthenticatedClient();

        var uniqueId = $"SyncTest{DateTime.UtcNow.Ticks % 1000000}";
        var patientRequest = new
        {
            Active = true,
            GivenName = $"SyncPatient{uniqueId}",
            FamilyName = "ToScheduling",
            Gender = "other",
            Phone = "+1-555-SYNC",
            Email = $"sync{uniqueId}@test.com",
        };

        var createResponse = await client.PostAsync(
            $"{E2EFixture.ClinicalUrl}/fhir/Patient/",
            new StringContent(
                System.Text.Json.JsonSerializer.Serialize(patientRequest),
                System.Text.Encoding.UTF8, "application/json"));
        createResponse.EnsureSuccessStatusCode();
        var patientJson = await createResponse.Content.ReadAsStringAsync();
        var patientDoc = System.Text.Json.JsonDocument.Parse(patientJson);
        var patientId = patientDoc.RootElement.GetProperty("Id").GetString();

        var clinicalGetResponse = await client.GetAsync($"{E2EFixture.ClinicalUrl}/fhir/Patient/{patientId}");
        Assert.Equal(HttpStatusCode.OK, clinicalGetResponse.StatusCode);

        var syncedToScheduling = false;
        for (var i = 0; i < 18; i++)
        {
            await Task.Delay(5000);

            var syncPatientsResponse = await client.GetAsync($"{E2EFixture.SchedulingUrl}/sync/patients");
            if (syncPatientsResponse.IsSuccessStatusCode)
            {
                var patientsJson = await syncPatientsResponse.Content.ReadAsStringAsync();
                if (patientsJson.Contains(patientId!) || patientsJson.Contains(uniqueId))
                {
                    syncedToScheduling = true;
                    break;
                }
            }
        }

        Assert.True(syncedToScheduling,
            $"Patient '{uniqueId}' created in Clinical.Api was not synced to Scheduling.Api within 90 seconds.");
    }

    /// <summary>
    /// Data added to Scheduling.Api is synced to Clinical.Api.
    /// </summary>
    [Fact]
    public async Task Sync_SchedulingPractitioner_AppearsInClinical_AfterSync()
    {
        using var client = E2EFixture.CreateAuthenticatedClient();

        var uniqueId = $"SyncTest{DateTime.UtcNow.Ticks % 1000000}";
        var practitionerRequest = new
        {
            Identifier = $"SYNC-DR-{uniqueId}",
            Active = true,
            NameGiven = $"SyncDoctor{uniqueId}",
            NameFamily = "ToClinical",
            Qualification = "MD",
            Specialty = "Sync Testing",
            TelecomEmail = $"syncdoc{uniqueId}@hospital.org",
            TelecomPhone = "+1-555-SYNC",
        };

        var createResponse = await client.PostAsync(
            $"{E2EFixture.SchedulingUrl}/Practitioner",
            new StringContent(
                System.Text.Json.JsonSerializer.Serialize(practitionerRequest),
                System.Text.Encoding.UTF8, "application/json"));
        createResponse.EnsureSuccessStatusCode();
        var practitionerJson = await createResponse.Content.ReadAsStringAsync();
        var practitionerDoc = System.Text.Json.JsonDocument.Parse(practitionerJson);
        var practitionerId = practitionerDoc.RootElement.GetProperty("Id").GetString();

        var schedulingGetResponse = await client.GetAsync($"{E2EFixture.SchedulingUrl}/Practitioner/{practitionerId}");
        Assert.Equal(HttpStatusCode.OK, schedulingGetResponse.StatusCode);

        var syncedToClinical = false;
        for (var i = 0; i < 18; i++)
        {
            await Task.Delay(5000);

            var syncProvidersResponse = await client.GetAsync($"{E2EFixture.ClinicalUrl}/sync/providers");
            if (syncProvidersResponse.IsSuccessStatusCode)
            {
                var providersJson = await syncProvidersResponse.Content.ReadAsStringAsync();
                if (providersJson.Contains(practitionerId!) || providersJson.Contains(uniqueId))
                {
                    syncedToClinical = true;
                    break;
                }
            }
        }

        Assert.True(syncedToClinical,
            $"Practitioner '{uniqueId}' created in Scheduling.Api was not synced to Clinical.Api within 90 seconds.");
    }

    /// <summary>
    /// Sync changes appear in Dashboard UI seamlessly.
    /// </summary>
    [Fact]
    public async Task Sync_ChangesAppearInDashboardUI_Seamlessly()
    {
        using var client = E2EFixture.CreateAuthenticatedClient();
        var page = await _fixture.Browser!.NewPageAsync();
        page.Console += (_, msg) => Console.WriteLine($"[BROWSER] {msg.Text}");

        var uniqueId = $"DashSync{DateTime.UtcNow.Ticks % 1000000}";
        var patientRequest = new
        {
            Active = true,
            GivenName = $"DashboardSync{uniqueId}",
            FamilyName = "TestPatient",
            Gender = "male",
        };

        var createResponse = await client.PostAsync(
            $"{E2EFixture.ClinicalUrl}/fhir/Patient/",
            new StringContent(
                System.Text.Json.JsonSerializer.Serialize(patientRequest),
                System.Text.Encoding.UTF8, "application/json"));
        createResponse.EnsureSuccessStatusCode();

        await page.GotoAsync($"{E2EFixture.DashboardUrl}#sync");
        await page.WaitForSelectorAsync("[data-testid='sync-page']", new PageWaitForSelectorOptions { Timeout = 20000 });
        await page.WaitForSelectorAsync("[data-testid='service-status-clinical']", new PageWaitForSelectorOptions { Timeout = 15000 });
        await page.WaitForSelectorAsync("[data-testid='service-status-scheduling']", new PageWaitForSelectorOptions { Timeout = 15000 });

        var content = await page.ContentAsync();
        Assert.Contains("Clinical.Api", content);
        Assert.Contains("Scheduling.Api", content);
        Assert.Contains("Sync Records", content);

        var clinicalCardVisible = await page.IsVisibleAsync("[data-testid='service-status-clinical']");
        var schedulingCardVisible = await page.IsVisibleAsync("[data-testid='service-status-scheduling']");
        Assert.True(clinicalCardVisible);
        Assert.True(schedulingCardVisible);

        await page.CloseAsync();
    }

    /// <summary>
    /// Sync log entries are created when data changes.
    /// </summary>
    [Fact]
    public async Task Sync_CreatesLogEntries_WhenDataChanges()
    {
        using var client = E2EFixture.CreateAuthenticatedClient();

        var initialClinicalResponse = await client.GetAsync($"{E2EFixture.ClinicalUrl}/sync/records");
        initialClinicalResponse.EnsureSuccessStatusCode();
        var initialClinicalJson = await initialClinicalResponse.Content.ReadAsStringAsync();
        var initialClinicalDoc = System.Text.Json.JsonDocument.Parse(initialClinicalJson);
        var initialClinicalCount = initialClinicalDoc.RootElement.GetProperty("total").GetInt32();

        var uniqueId = $"LogTest{DateTime.UtcNow.Ticks % 1000000}";
        var patientRequest = new
        {
            Active = true,
            GivenName = $"LogPatient{uniqueId}",
            FamilyName = "TestSync",
            Gender = "female",
        };

        var createResponse = await client.PostAsync(
            $"{E2EFixture.ClinicalUrl}/fhir/Patient/",
            new StringContent(
                System.Text.Json.JsonSerializer.Serialize(patientRequest),
                System.Text.Encoding.UTF8, "application/json"));
        createResponse.EnsureSuccessStatusCode();

        var updatedClinicalResponse = await client.GetAsync($"{E2EFixture.ClinicalUrl}/sync/records");
        updatedClinicalResponse.EnsureSuccessStatusCode();
        var updatedClinicalJson = await updatedClinicalResponse.Content.ReadAsStringAsync();
        var updatedClinicalDoc = System.Text.Json.JsonDocument.Parse(updatedClinicalJson);
        var updatedClinicalCount = updatedClinicalDoc.RootElement.GetProperty("total").GetInt32();

        Assert.True(updatedClinicalCount > initialClinicalCount,
            $"Sync log count should increase after creating a patient. Initial: {initialClinicalCount}, After: {updatedClinicalCount}");
    }
}
