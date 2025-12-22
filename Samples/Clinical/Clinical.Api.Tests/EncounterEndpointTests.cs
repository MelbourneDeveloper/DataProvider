using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Clinical.Api.Tests;
/// <summary>
/// E2E tests for Encounter FHIR endpoints - REAL database, NO mocks.
/// Each test creates its own isolated factory and database.
/// </summary>
public sealed class EncounterEndpointTests
{
    private static readonly string AuthToken = TestTokenHelper.GenerateClinicianToken();

    private static HttpClient CreateAuthenticatedClient(ClinicalApiFactory factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            AuthToken
        );
        return client;
    }

    private static async Task<string> CreateTestPatientAsync(HttpClient client)
    {
        var patient = new
        {
            Active = true,
            GivenName = "Encounter",
            FamilyName = "TestPatient",
            Gender = "male",
        };

        var response = await client.PostAsJsonAsync("/fhir/Patient/", patient);
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        return created.GetProperty("Id").GetString()!;
    }

    [Fact]
    public async Task GetEncountersByPatient_ReturnsEmptyList_WhenNoEncounters()
    {
        using var factory = new ClinicalApiFactory();
        var client = CreateAuthenticatedClient(factory);
        var patientId = await CreateTestPatientAsync(client);

        var response = await client.GetAsync($"/fhir/Patient/{patientId}/Encounter/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Equal("[]", content);
    }

    [Fact]
    public async Task CreateEncounter_ReturnsCreated_WithValidData()
    {
        using var factory = new ClinicalApiFactory();
        var client = CreateAuthenticatedClient(factory);
        var patientId = await CreateTestPatientAsync(client);
        var request = new
        {
            Status = "planned",
            Class = "ambulatory",
            PractitionerId = "practitioner-123",
            ServiceType = "General Practice",
            ReasonCode = "Annual checkup",
            PeriodStart = "2024-01-15T09:00:00Z",
            PeriodEnd = "2024-01-15T09:30:00Z",
            Notes = "Routine visit",
        };

        var response = await client.PostAsJsonAsync(
            $"/fhir/Patient/{patientId}/Encounter/",
            request
        );

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var encounter = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("planned", encounter.GetProperty("Status").GetString());
        Assert.Equal("ambulatory", encounter.GetProperty("Class").GetString());
        Assert.Equal(patientId, encounter.GetProperty("PatientId").GetString());
        Assert.NotNull(encounter.GetProperty("Id").GetString());
    }

    [Fact]
    public async Task CreateEncounter_WithAllStatuses()
    {
        using var factory = new ClinicalApiFactory();
        var client = CreateAuthenticatedClient(factory);
        var statuses = new[]
        {
            "planned",
            "arrived",
            "triaged",
            "in-progress",
            "onleave",
            "finished",
            "cancelled",
        };

        foreach (var status in statuses)
        {
            var patientId = await CreateTestPatientAsync(client);
            var request = new
            {
                Status = status,
                Class = "ambulatory",
                PeriodStart = "2024-01-15T09:00:00Z",
            };

            var response = await client.PostAsJsonAsync(
                $"/fhir/Patient/{patientId}/Encounter/",
                request
            );

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var encounter = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(status, encounter.GetProperty("Status").GetString());
        }
    }

    [Fact]
    public async Task CreateEncounter_WithAllClasses()
    {
        using var factory = new ClinicalApiFactory();
        var client = CreateAuthenticatedClient(factory);
        var classes = new[] { "ambulatory", "emergency", "inpatient", "observation", "virtual" };

        foreach (var encounterClass in classes)
        {
            var patientId = await CreateTestPatientAsync(client);
            var request = new
            {
                Status = "planned",
                Class = encounterClass,
                PeriodStart = "2024-01-15T09:00:00Z",
            };

            var response = await client.PostAsJsonAsync(
                $"/fhir/Patient/{patientId}/Encounter/",
                request
            );

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var encounter = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(encounterClass, encounter.GetProperty("Class").GetString());
        }
    }

    [Fact]
    public async Task GetEncountersByPatient_ReturnsEncounters_WhenExist()
    {
        using var factory = new ClinicalApiFactory();
        var client = CreateAuthenticatedClient(factory);
        var patientId = await CreateTestPatientAsync(client);
        var request1 = new
        {
            Status = "planned",
            Class = "ambulatory",
            PeriodStart = "2024-01-15T09:00:00Z",
        };
        var request2 = new
        {
            Status = "finished",
            Class = "inpatient",
            PeriodStart = "2024-01-16T10:00:00Z",
        };

        await client.PostAsJsonAsync($"/fhir/Patient/{patientId}/Encounter/", request1);
        await client.PostAsJsonAsync($"/fhir/Patient/{patientId}/Encounter/", request2);

        var response = await client.GetAsync($"/fhir/Patient/{patientId}/Encounter/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var encounters = await response.Content.ReadFromJsonAsync<JsonElement[]>();
        Assert.NotNull(encounters);
        Assert.True(encounters.Length >= 2);
    }

    [Fact]
    public async Task CreateEncounter_SetsVersionIdToOne()
    {
        using var factory = new ClinicalApiFactory();
        var client = CreateAuthenticatedClient(factory);
        var patientId = await CreateTestPatientAsync(client);
        var request = new
        {
            Status = "planned",
            Class = "ambulatory",
            PeriodStart = "2024-01-15T09:00:00Z",
        };

        var response = await client.PostAsJsonAsync(
            $"/fhir/Patient/{patientId}/Encounter/",
            request
        );
        var encounter = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(1L, encounter.GetProperty("VersionId").GetInt64());
    }

    [Fact]
    public async Task CreateEncounter_SetsLastUpdatedTimestamp()
    {
        using var factory = new ClinicalApiFactory();
        var client = CreateAuthenticatedClient(factory);
        var patientId = await CreateTestPatientAsync(client);
        var request = new
        {
            Status = "planned",
            Class = "ambulatory",
            PeriodStart = "2024-01-15T09:00:00Z",
        };

        var response = await client.PostAsJsonAsync(
            $"/fhir/Patient/{patientId}/Encounter/",
            request
        );
        var encounter = await response.Content.ReadFromJsonAsync<JsonElement>();

        var lastUpdated = encounter.GetProperty("LastUpdated").GetString();
        Assert.NotNull(lastUpdated);
        Assert.Matches(@"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z", lastUpdated);
    }

    [Fact]
    public async Task CreateEncounter_WithNotes()
    {
        using var factory = new ClinicalApiFactory();
        var client = CreateAuthenticatedClient(factory);
        var patientId = await CreateTestPatientAsync(client);
        var request = new
        {
            Status = "planned",
            Class = "ambulatory",
            PeriodStart = "2024-01-15T09:00:00Z",
            Notes = "Patient reported mild headache. Follow up in 2 weeks.",
        };

        var response = await client.PostAsJsonAsync(
            $"/fhir/Patient/{patientId}/Encounter/",
            request
        );
        var encounter = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            "Patient reported mild headache. Follow up in 2 weeks.",
            encounter.GetProperty("Notes").GetString()
        );
    }

    [Fact]
    public async Task CreateEncounter_WithPeriodEndTime()
    {
        using var factory = new ClinicalApiFactory();
        var client = CreateAuthenticatedClient(factory);
        var patientId = await CreateTestPatientAsync(client);
        var request = new
        {
            Status = "finished",
            Class = "ambulatory",
            PeriodStart = "2024-01-15T09:00:00Z",
            PeriodEnd = "2024-01-15T09:45:00Z",
        };

        var response = await client.PostAsJsonAsync(
            $"/fhir/Patient/{patientId}/Encounter/",
            request
        );
        var encounter = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("2024-01-15T09:00:00Z", encounter.GetProperty("PeriodStart").GetString());
        Assert.Equal("2024-01-15T09:45:00Z", encounter.GetProperty("PeriodEnd").GetString());
    }
}
