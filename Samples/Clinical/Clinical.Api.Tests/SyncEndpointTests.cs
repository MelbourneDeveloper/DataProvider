namespace Clinical.Api.Tests;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

/// <summary>
/// E2E tests for Sync endpoints - REAL database, NO mocks.
/// Tests sync log generation and origin tracking.
/// Each test creates its own isolated factory and database.
/// </summary>
public sealed class SyncEndpointTests
{
    [Fact]
    public async Task GetSyncOrigin_ReturnsOriginId()
    {
        using var factory = new ClinicalApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/sync/origin");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        var originId = result.GetProperty("originId").GetString();
        Assert.NotNull(originId);
        Assert.NotEmpty(originId);
    }

    [Fact]
    public async Task GetSyncChanges_ReturnsEmptyList_WhenNoChanges()
    {
        using var factory = new ClinicalApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/sync/changes?fromVersion=999999");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Equal("[]", content);
    }

    [Fact]
    public async Task GetSyncChanges_ReturnChanges_AfterPatientCreated()
    {
        using var factory = new ClinicalApiFactory();
        var client = factory.CreateClient();
        var patientRequest = new
        {
            Active = true,
            GivenName = "Sync",
            FamilyName = "TestPatient",
            Gender = "male",
        };

        await client.PostAsJsonAsync("/fhir/Patient/", patientRequest);

        var response = await client.GetAsync("/sync/changes?fromVersion=0");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var changes = await response.Content.ReadFromJsonAsync<JsonElement[]>();
        Assert.NotNull(changes);
        Assert.True(changes.Length > 0);
    }

    [Fact]
    public async Task GetSyncChanges_RespectsLimitParameter()
    {
        using var factory = new ClinicalApiFactory();
        var client = factory.CreateClient();
        for (var i = 0; i < 5; i++)
        {
            var patientRequest = new
            {
                Active = true,
                GivenName = $"SyncLimit{i}",
                FamilyName = "TestPatient",
                Gender = "other",
            };
            await client.PostAsJsonAsync("/fhir/Patient/", patientRequest);
        }

        var response = await client.GetAsync("/sync/changes?fromVersion=0&limit=2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var changes = await response.Content.ReadFromJsonAsync<JsonElement[]>();
        Assert.NotNull(changes);
        Assert.True(changes.Length <= 2);
    }

    [Fact]
    public async Task GetSyncChanges_ContainsTableName()
    {
        using var factory = new ClinicalApiFactory();
        var client = factory.CreateClient();
        var patientRequest = new
        {
            Active = true,
            GivenName = "SyncTable",
            FamilyName = "TestPatient",
            Gender = "male",
        };
        await client.PostAsJsonAsync("/fhir/Patient/", patientRequest);

        var response = await client.GetAsync("/sync/changes?fromVersion=0");
        var changes = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.NotNull(changes);
        Assert.Contains(changes, c => c.GetProperty("TableName").GetString() == "fhir_Patient");
    }

    [Fact]
    public async Task GetSyncChanges_ContainsOperation()
    {
        using var factory = new ClinicalApiFactory();
        var client = factory.CreateClient();
        var patientRequest = new
        {
            Active = true,
            GivenName = "SyncOp",
            FamilyName = "TestPatient",
            Gender = "female",
        };
        await client.PostAsJsonAsync("/fhir/Patient/", patientRequest);

        var response = await client.GetAsync("/sync/changes?fromVersion=0");
        var changes = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.NotNull(changes);
        Assert.Contains(
            changes,
            c =>
            {
                // Operation is serialized as integer (0=Insert, 1=Update, 2=Delete)
                var opValue = c.GetProperty("Operation").GetInt32();
                return opValue >= 0 && opValue <= 2;
            }
        );
    }

    [Fact]
    public async Task GetSyncChanges_TracksEncounterChanges()
    {
        using var factory = new ClinicalApiFactory();
        var client = factory.CreateClient();
        var patientRequest = new
        {
            Active = true,
            GivenName = "SyncEncounter",
            FamilyName = "TestPatient",
            Gender = "male",
        };
        var patientResponse = await client.PostAsJsonAsync("/fhir/Patient/", patientRequest);
        var patient = await patientResponse.Content.ReadFromJsonAsync<JsonElement>();
        var patientId = patient.GetProperty("Id").GetString();

        var encounterRequest = new
        {
            Status = "planned",
            Class = "ambulatory",
            PeriodStart = "2024-02-01T10:00:00Z",
        };
        await client.PostAsJsonAsync($"/fhir/Patient/{patientId}/Encounter/", encounterRequest);

        var response = await client.GetAsync("/sync/changes?fromVersion=0");
        var changes = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.NotNull(changes);
        Assert.Contains(changes, c => c.GetProperty("TableName").GetString() == "fhir_Encounter");
    }

    [Fact]
    public async Task GetSyncChanges_TracksConditionChanges()
    {
        using var factory = new ClinicalApiFactory();
        var client = factory.CreateClient();
        var patientRequest = new
        {
            Active = true,
            GivenName = "SyncCondition",
            FamilyName = "TestPatient",
            Gender = "female",
        };
        var patientResponse = await client.PostAsJsonAsync("/fhir/Patient/", patientRequest);
        var patient = await patientResponse.Content.ReadFromJsonAsync<JsonElement>();
        var patientId = patient.GetProperty("Id").GetString();

        var conditionRequest = new
        {
            ClinicalStatus = "active",
            CodeSystem = "http://hl7.org/fhir/sid/icd-10-cm",
            CodeValue = "J06.9",
            CodeDisplay = "URI",
        };
        await client.PostAsJsonAsync($"/fhir/Patient/{patientId}/Condition/", conditionRequest);

        var response = await client.GetAsync("/sync/changes?fromVersion=0");
        var changes = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.NotNull(changes);
        Assert.Contains(changes, c => c.GetProperty("TableName").GetString() == "fhir_Condition");
    }

    [Fact]
    public async Task GetSyncChanges_TracksMedicationRequestChanges()
    {
        using var factory = new ClinicalApiFactory();
        var client = factory.CreateClient();
        var patientRequest = new
        {
            Active = true,
            GivenName = "SyncMedication",
            FamilyName = "TestPatient",
            Gender = "male",
        };
        var patientResponse = await client.PostAsJsonAsync("/fhir/Patient/", patientRequest);
        var patient = await patientResponse.Content.ReadFromJsonAsync<JsonElement>();
        var patientId = patient.GetProperty("Id").GetString();

        var medicationRequest = new
        {
            Status = "active",
            Intent = "order",
            PractitionerId = "doc-sync",
            MedicationCode = "123",
            MedicationDisplay = "Test Med",
            Refills = 0,
        };
        await client.PostAsJsonAsync(
            $"/fhir/Patient/{patientId}/MedicationRequest/",
            medicationRequest
        );

        var response = await client.GetAsync("/sync/changes?fromVersion=0");
        var changes = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.NotNull(changes);
        Assert.Contains(
            changes,
            c => c.GetProperty("TableName").GetString() == "fhir_MedicationRequest"
        );
    }
}
