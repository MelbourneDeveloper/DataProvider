namespace Clinical.Api.Tests;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

/// <summary>
/// E2E tests for Condition FHIR endpoints - REAL database, NO mocks.
/// Each test creates its own isolated factory and database.
/// </summary>
public sealed class ConditionEndpointTests
{
    private static async Task<string> CreateTestPatientAsync(HttpClient client)
    {
        var patient = new
        {
            Active = true,
            GivenName = "Condition",
            FamilyName = "TestPatient",
            Gender = "female",
        };

        var response = await client.PostAsJsonAsync("/fhir/Patient/", patient);
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        return created.GetProperty("Id").GetString()!;
    }

    [Fact]
    public async Task GetConditionsByPatient_ReturnsEmptyList_WhenNoConditions()
    {
        using var factory = new ClinicalApiFactory();
        var client = factory.CreateClient();
        var patientId = await CreateTestPatientAsync(client);

        var response = await client.GetAsync($"/fhir/Patient/{patientId}/Condition/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Equal("[]", content);
    }

    [Fact]
    public async Task CreateCondition_ReturnsCreated_WithValidData()
    {
        using var factory = new ClinicalApiFactory();
        var client = factory.CreateClient();
        var patientId = await CreateTestPatientAsync(client);
        var request = new
        {
            ClinicalStatus = "active",
            VerificationStatus = "confirmed",
            Category = "problem-list-item",
            Severity = "moderate",
            CodeSystem = "http://hl7.org/fhir/sid/icd-10-cm",
            CodeValue = "J06.9",
            CodeDisplay = "Acute upper respiratory infection, unspecified",
            OnsetDateTime = "2024-01-10T00:00:00Z",
            NoteText = "Patient presents with cold symptoms",
        };

        var response = await client.PostAsJsonAsync(
            $"/fhir/Patient/{patientId}/Condition/",
            request
        );

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var condition = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("active", condition.GetProperty("ClinicalStatus").GetString());
        Assert.Equal("J06.9", condition.GetProperty("CodeValue").GetString());
        Assert.Equal(patientId, condition.GetProperty("SubjectReference").GetString());
        Assert.NotNull(condition.GetProperty("Id").GetString());
    }

    [Fact]
    public async Task CreateCondition_WithAllClinicalStatuses()
    {
        using var factory = new ClinicalApiFactory();
        var client = factory.CreateClient();
        var statuses = new[]
        {
            "active",
            "recurrence",
            "relapse",
            "inactive",
            "remission",
            "resolved",
        };

        foreach (var status in statuses)
        {
            var patientId = await CreateTestPatientAsync(client);
            var request = new
            {
                ClinicalStatus = status,
                CodeSystem = "http://hl7.org/fhir/sid/icd-10-cm",
                CodeValue = "Z00.00",
                CodeDisplay = "General examination",
            };

            var response = await client.PostAsJsonAsync(
                $"/fhir/Patient/{patientId}/Condition/",
                request
            );

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var condition = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(status, condition.GetProperty("ClinicalStatus").GetString());
        }
    }

    [Fact]
    public async Task CreateCondition_WithAllSeverities()
    {
        using var factory = new ClinicalApiFactory();
        var client = factory.CreateClient();
        var severities = new[] { "mild", "moderate", "severe" };

        foreach (var severity in severities)
        {
            var patientId = await CreateTestPatientAsync(client);
            var request = new
            {
                ClinicalStatus = "active",
                Severity = severity,
                CodeSystem = "http://hl7.org/fhir/sid/icd-10-cm",
                CodeValue = "R51",
                CodeDisplay = "Headache",
            };

            var response = await client.PostAsJsonAsync(
                $"/fhir/Patient/{patientId}/Condition/",
                request
            );

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var condition = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(severity, condition.GetProperty("Severity").GetString());
        }
    }

    [Fact]
    public async Task CreateCondition_WithVerificationStatuses()
    {
        using var factory = new ClinicalApiFactory();
        var client = factory.CreateClient();
        var statuses = new[]
        {
            "unconfirmed",
            "provisional",
            "differential",
            "confirmed",
            "refuted",
        };

        foreach (var status in statuses)
        {
            var patientId = await CreateTestPatientAsync(client);
            var request = new
            {
                ClinicalStatus = "active",
                VerificationStatus = status,
                CodeSystem = "http://hl7.org/fhir/sid/icd-10-cm",
                CodeValue = "M54.5",
                CodeDisplay = "Low back pain",
            };

            var response = await client.PostAsJsonAsync(
                $"/fhir/Patient/{patientId}/Condition/",
                request
            );

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var condition = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(status, condition.GetProperty("VerificationStatus").GetString());
        }
    }

    [Fact]
    public async Task GetConditionsByPatient_ReturnsConditions_WhenExist()
    {
        using var factory = new ClinicalApiFactory();
        var client = factory.CreateClient();
        var patientId = await CreateTestPatientAsync(client);
        var request1 = new
        {
            ClinicalStatus = "active",
            CodeSystem = "http://hl7.org/fhir/sid/icd-10-cm",
            CodeValue = "E11.9",
            CodeDisplay = "Type 2 diabetes mellitus",
        };
        var request2 = new
        {
            ClinicalStatus = "resolved",
            CodeSystem = "http://hl7.org/fhir/sid/icd-10-cm",
            CodeValue = "J02.9",
            CodeDisplay = "Acute pharyngitis, unspecified",
        };

        await client.PostAsJsonAsync($"/fhir/Patient/{patientId}/Condition/", request1);
        await client.PostAsJsonAsync($"/fhir/Patient/{patientId}/Condition/", request2);

        var response = await client.GetAsync($"/fhir/Patient/{patientId}/Condition/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var conditions = await response.Content.ReadFromJsonAsync<JsonElement[]>();
        Assert.NotNull(conditions);
        Assert.True(conditions.Length >= 2);
    }

    [Fact]
    public async Task CreateCondition_SetsRecordedDate()
    {
        using var factory = new ClinicalApiFactory();
        var client = factory.CreateClient();
        var patientId = await CreateTestPatientAsync(client);
        var request = new
        {
            ClinicalStatus = "active",
            CodeSystem = "http://hl7.org/fhir/sid/icd-10-cm",
            CodeValue = "I10",
            CodeDisplay = "Essential hypertension",
        };

        var response = await client.PostAsJsonAsync(
            $"/fhir/Patient/{patientId}/Condition/",
            request
        );
        var condition = await response.Content.ReadFromJsonAsync<JsonElement>();

        var recordedDate = condition.GetProperty("RecordedDate").GetString();
        Assert.NotNull(recordedDate);
        Assert.Matches(@"\d{4}-\d{2}-\d{2}", recordedDate);
    }

    [Fact]
    public async Task CreateCondition_SetsVersionIdToOne()
    {
        using var factory = new ClinicalApiFactory();
        var client = factory.CreateClient();
        var patientId = await CreateTestPatientAsync(client);
        var request = new
        {
            ClinicalStatus = "active",
            CodeSystem = "http://hl7.org/fhir/sid/icd-10-cm",
            CodeValue = "K21.0",
            CodeDisplay = "GERD",
        };

        var response = await client.PostAsJsonAsync(
            $"/fhir/Patient/{patientId}/Condition/",
            request
        );
        var condition = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(1L, condition.GetProperty("VersionId").GetInt64());
    }

    [Fact]
    public async Task CreateCondition_WithEncounterReference()
    {
        using var factory = new ClinicalApiFactory();
        var client = factory.CreateClient();
        var patientId = await CreateTestPatientAsync(client);

        var encounterRequest = new
        {
            Status = "finished",
            Class = "ambulatory",
            PeriodStart = "2024-01-15T09:00:00Z",
        };
        var encounterResponse = await client.PostAsJsonAsync(
            $"/fhir/Patient/{patientId}/Encounter/",
            encounterRequest
        );
        var encounter = await encounterResponse.Content.ReadFromJsonAsync<JsonElement>();
        var encounterId = encounter.GetProperty("Id").GetString();

        var conditionRequest = new
        {
            ClinicalStatus = "active",
            CodeSystem = "http://hl7.org/fhir/sid/icd-10-cm",
            CodeValue = "J18.9",
            CodeDisplay = "Pneumonia, unspecified organism",
            EncounterReference = encounterId,
        };

        var response = await client.PostAsJsonAsync(
            $"/fhir/Patient/{patientId}/Condition/",
            conditionRequest
        );
        var condition = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(encounterId, condition.GetProperty("EncounterReference").GetString());
    }

    [Fact]
    public async Task CreateCondition_WithNotes()
    {
        using var factory = new ClinicalApiFactory();
        var client = factory.CreateClient();
        var patientId = await CreateTestPatientAsync(client);
        var request = new
        {
            ClinicalStatus = "active",
            CodeSystem = "http://hl7.org/fhir/sid/icd-10-cm",
            CodeValue = "F32.1",
            CodeDisplay = "Major depressive disorder, single episode, moderate",
            NoteText = "Patient started on SSRI therapy. Follow up in 4 weeks.",
        };

        var response = await client.PostAsJsonAsync(
            $"/fhir/Patient/{patientId}/Condition/",
            request
        );
        var condition = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            "Patient started on SSRI therapy. Follow up in 4 weeks.",
            condition.GetProperty("NoteText").GetString()
        );
    }
}
