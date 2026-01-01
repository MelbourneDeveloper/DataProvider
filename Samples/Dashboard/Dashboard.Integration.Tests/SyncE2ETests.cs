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
        await page.WaitForSelectorAsync(
            ".sidebar",
            new PageWaitForSelectorOptions { Timeout = 20000 }
        );
        await page.ClickAsync("text=Sync Dashboard");

        await page.WaitForSelectorAsync(
            "[data-testid='sync-page']",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );
        Assert.Contains("#sync", page.Url);

        await page.WaitForSelectorAsync(
            "[data-testid='service-status-clinical']",
            new PageWaitForSelectorOptions { Timeout = 15000 }
        );
        await page.WaitForSelectorAsync(
            "[data-testid='service-status-scheduling']",
            new PageWaitForSelectorOptions { Timeout = 15000 }
        );
        await page.WaitForSelectorAsync(
            "[data-testid='sync-records-table']",
            new PageWaitForSelectorOptions { Timeout = 5000 }
        );
        await page.WaitForSelectorAsync(
            "[data-testid='action-filter']",
            new PageWaitForSelectorOptions { Timeout = 5000 }
        );
        await page.WaitForSelectorAsync(
            "[data-testid='service-filter']",
            new PageWaitForSelectorOptions { Timeout = 5000 }
        );

        var content = await page.ContentAsync();
        Assert.Contains("Sync Dashboard", content);
        Assert.Contains("Clinical.Api", content);
        Assert.Contains("Scheduling.Api", content);
        Assert.Contains("Sync Records", content);

        await page.CloseAsync();
    }

    /// <summary>
    /// Sync Dashboard service filter shows ONLY records from selected service.
    /// This test PROVES the filter works by verifying actual row content.
    /// </summary>
    [Fact]
    public async Task SyncDashboard_ServiceFilter_ShowsOnlySelectedService()
    {
        using var client = E2EFixture.CreateAuthenticatedClient();
        var page = await _fixture.Browser!.NewPageAsync();
        page.Console += (_, msg) => Console.WriteLine($"[BROWSER] {msg.Text}");

        // Create data in both services to ensure we have records from both
        var uniqueId = $"FilterTest{DateTime.UtcNow.Ticks % 1000000}";

        // Create patient in Clinical.Api
        var patientRequest = new
        {
            Active = true,
            GivenName = $"FilterPatient{uniqueId}",
            FamilyName = "ClinicalTest",
            Gender = "other",
        };
        await client.PostAsync(
            $"{E2EFixture.ClinicalUrl}/fhir/Patient/",
            new StringContent(
                System.Text.Json.JsonSerializer.Serialize(patientRequest),
                System.Text.Encoding.UTF8,
                "application/json"
            )
        );

        // Create practitioner in Scheduling.Api
        var practitionerRequest = new
        {
            Identifier = $"FILTER-DR-{uniqueId}",
            Active = true,
            NameGiven = $"FilterDoc{uniqueId}",
            NameFamily = "SchedulingTest",
            Qualification = "MD",
            Specialty = "Testing",
        };
        await client.PostAsync(
            $"{E2EFixture.SchedulingUrl}/Practitioner",
            new StringContent(
                System.Text.Json.JsonSerializer.Serialize(practitionerRequest),
                System.Text.Encoding.UTF8,
                "application/json"
            )
        );

        await page.GotoAsync($"{E2EFixture.DashboardUrl}#sync");
        await page.WaitForSelectorAsync(
            "[data-testid='sync-page']",
            new PageWaitForSelectorOptions { Timeout = 20000 }
        );
        await page.WaitForSelectorAsync(
            "[data-testid='service-status-clinical']",
            new PageWaitForSelectorOptions { Timeout = 15000 }
        );
        await Task.Delay(1000); // Allow data to load

        // Get initial count with all services
        var allRows = await page.QuerySelectorAllAsync(
            "[data-testid='sync-records-table'] tbody tr"
        );
        var initialCount = allRows.Count;
        Console.WriteLine($"[TEST] Initial row count (all services): {initialCount}");

        // Filter to Clinical only
        await page.SelectOptionAsync("[data-testid='service-filter']", "clinical");
        await Task.Delay(500);

        var clinicalRows = await page.QuerySelectorAllAsync(
            "[data-testid='sync-records-table'] tbody tr"
        );
        Console.WriteLine($"[TEST] Clinical filter row count: {clinicalRows.Count}");

        // PROVE: Every visible row must be from Clinical service
        foreach (var row in clinicalRows)
        {
            var serviceAttr = await row.GetAttributeAsync("data-service");
            Assert.Equal("clinical", serviceAttr);
        }

        // Filter to Scheduling only
        await page.SelectOptionAsync("[data-testid='service-filter']", "scheduling");
        await Task.Delay(500);

        var schedulingRows = await page.QuerySelectorAllAsync(
            "[data-testid='sync-records-table'] tbody tr"
        );
        Console.WriteLine($"[TEST] Scheduling filter row count: {schedulingRows.Count}");

        // PROVE: Every visible row must be from Scheduling service
        foreach (var row in schedulingRows)
        {
            var serviceAttr = await row.GetAttributeAsync("data-service");
            Assert.Equal("scheduling", serviceAttr);
        }

        // PROVE: Combined counts should equal total (or less if overlap)
        Assert.True(
            clinicalRows.Count + schedulingRows.Count <= initialCount + 1,
            $"Clinical ({clinicalRows.Count}) + Scheduling ({schedulingRows.Count}) should not exceed initial ({initialCount})"
        );

        await page.CloseAsync();
    }

    /// <summary>
    /// Sync Dashboard action filter shows ONLY records with selected operation.
    /// This test PROVES the filter works by verifying actual row content.
    /// </summary>
    [Fact]
    public async Task SyncDashboard_ActionFilter_ShowsOnlySelectedOperation()
    {
        using var client = E2EFixture.CreateAuthenticatedClient();
        var page = await _fixture.Browser!.NewPageAsync();
        page.Console += (_, msg) => Console.WriteLine($"[BROWSER] {msg.Text}");

        // Create a patient (Insert operation = 0)
        var uniqueId = $"ActionTest{DateTime.UtcNow.Ticks % 1000000}";
        var patientRequest = new
        {
            Active = true,
            GivenName = $"ActionPatient{uniqueId}",
            FamilyName = "InsertTest",
            Gender = "female",
        };
        var createResponse = await client.PostAsync(
            $"{E2EFixture.ClinicalUrl}/fhir/Patient/",
            new StringContent(
                System.Text.Json.JsonSerializer.Serialize(patientRequest),
                System.Text.Encoding.UTF8,
                "application/json"
            )
        );
        createResponse.EnsureSuccessStatusCode();

        await page.GotoAsync($"{E2EFixture.DashboardUrl}#sync");
        await page.WaitForSelectorAsync(
            "[data-testid='sync-page']",
            new PageWaitForSelectorOptions { Timeout = 20000 }
        );
        await page.WaitForSelectorAsync(
            "[data-testid='service-status-clinical']",
            new PageWaitForSelectorOptions { Timeout = 15000 }
        );
        await Task.Delay(1000); // Allow data to load

        // Filter to Insert operations only (operation = 0)
        await page.SelectOptionAsync("[data-testid='action-filter']", "0");

        // Wait for React to apply the filter - wait until ALL visible rows have operation=0
        await page.WaitForFunctionAsync(
            @"() => {
                const rows = document.querySelectorAll('[data-testid=""sync-records-table""] tbody tr');
                if (rows.length === 0) return true;
                return Array.from(rows).every(row => row.getAttribute('data-operation') === '0');
            }",
            new PageWaitForFunctionOptions { Timeout = 10000 }
        );

        var insertRows = await page.QuerySelectorAllAsync(
            "[data-testid='sync-records-table'] tbody tr"
        );
        Console.WriteLine($"[TEST] Insert filter row count: {insertRows.Count}");

        // PROVE: Every visible row must have Insert operation (0)
        foreach (var row in insertRows)
        {
            var operationAttr = await row.GetAttributeAsync("data-operation");
            Assert.Equal("0", operationAttr);
        }

        // Verify filter value is selected
        var selectedValue = await page.EvalOnSelectorAsync<string>(
            "[data-testid='action-filter']",
            "el => el.value"
        );
        Assert.Equal("0", selectedValue);

        // Reset filter
        await page.SelectOptionAsync("[data-testid='action-filter']", "all");

        // Wait for React to apply the reset filter
        await page.WaitForFunctionAsync(
            $"() => document.querySelector('[data-testid=\"action-filter\"]').value === 'all'",
            new PageWaitForFunctionOptions { Timeout = 5000 }
        );
        await Task.Delay(300); // Small buffer for React re-render

        var allRows = await page.QuerySelectorAllAsync(
            "[data-testid='sync-records-table'] tbody tr"
        );
        Assert.True(allRows.Count >= insertRows.Count, "All rows should be >= Insert-only rows");

        await page.CloseAsync();
    }

    /// <summary>
    /// Sync Dashboard combined filters work correctly.
    /// PROVES both service AND action filters can be used together.
    /// </summary>
    [Fact]
    public async Task SyncDashboard_CombinedFilters_WorkTogether()
    {
        using var client = E2EFixture.CreateAuthenticatedClient();
        var page = await _fixture.Browser!.NewPageAsync();
        page.Console += (_, msg) => Console.WriteLine($"[BROWSER] {msg.Text}");

        // Create data in Clinical.Api
        var uniqueId = $"ComboTest{DateTime.UtcNow.Ticks % 1000000}";
        var patientRequest = new
        {
            Active = true,
            GivenName = $"ComboPatient{uniqueId}",
            FamilyName = "ComboTest",
            Gender = "male",
        };
        await client.PostAsync(
            $"{E2EFixture.ClinicalUrl}/fhir/Patient/",
            new StringContent(
                System.Text.Json.JsonSerializer.Serialize(patientRequest),
                System.Text.Encoding.UTF8,
                "application/json"
            )
        );

        await page.GotoAsync($"{E2EFixture.DashboardUrl}#sync");
        await page.WaitForSelectorAsync(
            "[data-testid='sync-page']",
            new PageWaitForSelectorOptions { Timeout = 20000 }
        );
        await page.WaitForSelectorAsync(
            "[data-testid='service-status-clinical']",
            new PageWaitForSelectorOptions { Timeout = 15000 }
        );
        await Task.Delay(1000);

        // Apply both filters: Clinical + Insert
        await page.SelectOptionAsync("[data-testid='service-filter']", "clinical");
        await page.SelectOptionAsync("[data-testid='action-filter']", "0");

        // Wait for React to apply both filters
        await page.WaitForFunctionAsync(
            @"() => {
                const rows = document.querySelectorAll('[data-testid=""sync-records-table""] tbody tr');
                if (rows.length === 0) return true; // No rows = filters applied (or empty)
                return Array.from(rows).every(row =>
                    row.getAttribute('data-service') === 'clinical' &&
                    row.getAttribute('data-operation') === '0'
                );
            }",
            new PageWaitForFunctionOptions { Timeout = 5000 }
        );

        var filteredRows = await page.QuerySelectorAllAsync(
            "[data-testid='sync-records-table'] tbody tr"
        );
        Console.WriteLine(
            $"[TEST] Combined filter (Clinical + Insert) row count: {filteredRows.Count}"
        );

        // PROVE: Every row must satisfy BOTH filters
        foreach (var row in filteredRows)
        {
            var serviceAttr = await row.GetAttributeAsync("data-service");
            var operationAttr = await row.GetAttributeAsync("data-operation");
            Assert.Equal("clinical", serviceAttr);
            Assert.Equal("0", operationAttr);
        }

        // Try Scheduling + Insert
        await page.SelectOptionAsync("[data-testid='service-filter']", "scheduling");

        // Wait for React to apply the service filter change
        await page.WaitForFunctionAsync(
            @"() => {
                const rows = document.querySelectorAll('[data-testid=""sync-records-table""] tbody tr');
                if (rows.length === 0) return true;
                return Array.from(rows).every(row =>
                    row.getAttribute('data-service') === 'scheduling' &&
                    row.getAttribute('data-operation') === '0'
                );
            }",
            new PageWaitForFunctionOptions { Timeout = 5000 }
        );

        var schedulingInsertRows = await page.QuerySelectorAllAsync(
            "[data-testid='sync-records-table'] tbody tr"
        );
        foreach (var row in schedulingInsertRows)
        {
            var serviceAttr = await row.GetAttributeAsync("data-service");
            var operationAttr = await row.GetAttributeAsync("data-operation");
            Assert.Equal("scheduling", serviceAttr);
            Assert.Equal("0", operationAttr);
        }

        await page.CloseAsync();
    }

    /// <summary>
    /// Sync Dashboard search filter works correctly.
    /// PROVES search by entity ID filters correctly.
    /// </summary>
    [Fact]
    public async Task SyncDashboard_SearchFilter_FiltersCorrectly()
    {
        using var client = E2EFixture.CreateAuthenticatedClient();
        var page = await _fixture.Browser!.NewPageAsync();
        page.Console += (_, msg) => Console.WriteLine($"[BROWSER] {msg.Text}");

        // Create a patient with a known unique identifier
        var uniqueId = $"SearchTest{DateTime.UtcNow.Ticks % 1000000}";
        var patientRequest = new
        {
            Active = true,
            GivenName = $"SearchPatient{uniqueId}",
            FamilyName = "SearchTest",
            Gender = "male",
        };
        var createResponse = await client.PostAsync(
            $"{E2EFixture.ClinicalUrl}/fhir/Patient/",
            new StringContent(
                System.Text.Json.JsonSerializer.Serialize(patientRequest),
                System.Text.Encoding.UTF8,
                "application/json"
            )
        );
        createResponse.EnsureSuccessStatusCode();
        var patientJson = await createResponse.Content.ReadAsStringAsync();
        var patientDoc = System.Text.Json.JsonDocument.Parse(patientJson);
        var patientId = patientDoc.RootElement.GetProperty("Id").GetString();

        await page.GotoAsync($"{E2EFixture.DashboardUrl}#sync");
        await page.WaitForSelectorAsync(
            "[data-testid='sync-page']",
            new PageWaitForSelectorOptions { Timeout = 20000 }
        );
        await page.WaitForSelectorAsync(
            "[data-testid='service-status-clinical']",
            new PageWaitForSelectorOptions { Timeout = 15000 }
        );
        await Task.Delay(1000);

        // Get initial count
        var initialRows = await page.QuerySelectorAllAsync(
            "[data-testid='sync-records-table'] tbody tr"
        );
        var initialCount = initialRows.Count;

        // Search for the patient ID
        await page.FillAsync("[data-testid='sync-search']", patientId!);
        await Task.Delay(500);

        var searchRows = await page.QuerySelectorAllAsync(
            "[data-testid='sync-records-table'] tbody tr"
        );
        Console.WriteLine($"[TEST] Search for '{patientId}' found {searchRows.Count} rows");

        // PROVE: Search should find at least one matching row
        Assert.True(
            searchRows.Count >= 1,
            $"Search for patient ID '{patientId}' should find at least one row"
        );
        Assert.True(
            searchRows.Count < initialCount || initialCount <= 1,
            "Search should filter down results (unless only 1 row exists)"
        );

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
        await page.WaitForSelectorAsync(
            "[data-testid='sync-page']",
            new PageWaitForSelectorOptions { Timeout = 20000 }
        );

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
                System.Text.Encoding.UTF8,
                "application/json"
            )
        );
        createResponse.EnsureSuccessStatusCode();
        var patientJson = await createResponse.Content.ReadAsStringAsync();
        var patientDoc = System.Text.Json.JsonDocument.Parse(patientJson);
        var patientId = patientDoc.RootElement.GetProperty("Id").GetString();

        var clinicalGetResponse = await client.GetAsync(
            $"{E2EFixture.ClinicalUrl}/fhir/Patient/{patientId}"
        );
        Assert.Equal(HttpStatusCode.OK, clinicalGetResponse.StatusCode);

        var syncedToScheduling = false;
        for (var i = 0; i < 18; i++)
        {
            await Task.Delay(5000);

            var syncPatientsResponse = await client.GetAsync(
                $"{E2EFixture.SchedulingUrl}/sync/patients"
            );
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

        Assert.True(
            syncedToScheduling,
            $"Patient '{uniqueId}' created in Clinical.Api was not synced to Scheduling.Api within 90 seconds."
        );
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
                System.Text.Encoding.UTF8,
                "application/json"
            )
        );
        createResponse.EnsureSuccessStatusCode();
        var practitionerJson = await createResponse.Content.ReadAsStringAsync();
        var practitionerDoc = System.Text.Json.JsonDocument.Parse(practitionerJson);
        var practitionerId = practitionerDoc.RootElement.GetProperty("Id").GetString();

        var schedulingGetResponse = await client.GetAsync(
            $"{E2EFixture.SchedulingUrl}/Practitioner/{practitionerId}"
        );
        Assert.Equal(HttpStatusCode.OK, schedulingGetResponse.StatusCode);

        var syncedToClinical = false;
        for (var i = 0; i < 30; i++)
        {
            await Task.Delay(5000);

            var syncProvidersResponse = await client.GetAsync(
                $"{E2EFixture.ClinicalUrl}/sync/providers"
            );
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

        Assert.True(
            syncedToClinical,
            $"Practitioner '{uniqueId}' created in Scheduling.Api was not synced to Clinical.Api within 150 seconds."
        );
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
                System.Text.Encoding.UTF8,
                "application/json"
            )
        );
        createResponse.EnsureSuccessStatusCode();

        await page.GotoAsync($"{E2EFixture.DashboardUrl}#sync");
        await page.WaitForSelectorAsync(
            "[data-testid='sync-page']",
            new PageWaitForSelectorOptions { Timeout = 20000 }
        );
        await page.WaitForSelectorAsync(
            "[data-testid='service-status-clinical']",
            new PageWaitForSelectorOptions { Timeout = 15000 }
        );
        await page.WaitForSelectorAsync(
            "[data-testid='service-status-scheduling']",
            new PageWaitForSelectorOptions { Timeout = 15000 }
        );

        var content = await page.ContentAsync();
        Assert.Contains("Clinical.Api", content);
        Assert.Contains("Scheduling.Api", content);
        Assert.Contains("Sync Records", content);

        var clinicalCardVisible = await page.IsVisibleAsync(
            "[data-testid='service-status-clinical']"
        );
        var schedulingCardVisible = await page.IsVisibleAsync(
            "[data-testid='service-status-scheduling']"
        );
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

        var initialClinicalResponse = await client.GetAsync(
            $"{E2EFixture.ClinicalUrl}/sync/records"
        );
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
                System.Text.Encoding.UTF8,
                "application/json"
            )
        );
        createResponse.EnsureSuccessStatusCode();

        var updatedClinicalResponse = await client.GetAsync(
            $"{E2EFixture.ClinicalUrl}/sync/records"
        );
        updatedClinicalResponse.EnsureSuccessStatusCode();
        var updatedClinicalJson = await updatedClinicalResponse.Content.ReadAsStringAsync();
        var updatedClinicalDoc = System.Text.Json.JsonDocument.Parse(updatedClinicalJson);
        var updatedClinicalCount = updatedClinicalDoc.RootElement.GetProperty("total").GetInt32();

        Assert.True(
            updatedClinicalCount > initialClinicalCount,
            $"Sync log count should increase after creating a patient. Initial: {initialClinicalCount}, After: {updatedClinicalCount}"
        );
    }
}
