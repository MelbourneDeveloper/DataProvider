namespace Clinical.Api.Tests;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

/// <summary>
/// E2E tests for MedicationRequest FHIR endpoints - REAL database, NO mocks.
/// Each test creates its own isolated factory and database.
/// </summary>
public sealed class MedicationRequestEndpointTests
{
    private static async Task<string> CreateTestPatientAsync(HttpClient client)
    {
        var patient = new
        {
            Active = true,
            GivenName = "Medication",
            FamilyName = "TestPatient",
            Gender = "male",
        };

        var response = await client.PostAsJsonAsync("/fhir/Patient/", patient);
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        return created.GetProperty("Id").GetString()!;
    }

    [Fact]
    public async Task GetMedicationsByPatient_ReturnsEmptyList_WhenNoMedications()
    {
        using var factory = new ClinicalApiFactory();
        var client = factory.CreateClient();
        var patientId = await CreateTestPatientAsync(client);

        var response = await client.GetAsync($"/fhir/Patient/{patientId}/MedicationRequest/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Equal("[]", content);
    }

    [Fact]
    public async Task CreateMedicationRequest_ReturnsCreated_WithValidData()
    {
        using var factory = new ClinicalApiFactory();
        var client = factory.CreateClient();
        var patientId = await CreateTestPatientAsync(client);
        var request = new
        {
            Status = "active",
            Intent = "order",
            PractitionerId = "practitioner-456",
            MedicationCode = "197361",
            MedicationDisplay = "Lisinopril 10 MG Oral Tablet",
            DosageInstruction = "Take 1 tablet by mouth once daily",
            Quantity = 30.0,
            Unit = "tablet",
            Refills = 3,
        };

        var response = await client.PostAsJsonAsync(
            $"/fhir/Patient/{patientId}/MedicationRequest/",
            request
        );

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var medication = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("active", medication.GetProperty("Status").GetString());
        Assert.Equal("order", medication.GetProperty("Intent").GetString());
        Assert.Equal(
            "Lisinopril 10 MG Oral Tablet",
            medication.GetProperty("MedicationDisplay").GetString()
        );
        Assert.Equal(patientId, medication.GetProperty("PatientId").GetString());
        Assert.NotNull(medication.GetProperty("Id").GetString());
    }

    [Fact]
    public async Task CreateMedicationRequest_WithAllStatuses()
    {
        using var factory = new ClinicalApiFactory();
        var client = factory.CreateClient();
        var statuses = new[] { "active", "on-hold", "cancelled", "completed", "stopped", "draft" };

        foreach (var status in statuses)
        {
            var patientId = await CreateTestPatientAsync(client);
            var request = new
            {
                Status = status,
                Intent = "order",
                PractitionerId = "practitioner-789",
                MedicationCode = "123456",
                MedicationDisplay = "Test Medication",
                Refills = 0,
            };

            var response = await client.PostAsJsonAsync(
                $"/fhir/Patient/{patientId}/MedicationRequest/",
                request
            );

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var medication = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(status, medication.GetProperty("Status").GetString());
        }
    }

    [Fact]
    public async Task CreateMedicationRequest_WithAllIntents()
    {
        using var factory = new ClinicalApiFactory();
        var client = factory.CreateClient();
        var intents = new[]
        {
            "proposal",
            "plan",
            "order",
            "original-order",
            "reflex-order",
            "filler-order",
            "instance-order",
            "option",
        };

        foreach (var intent in intents)
        {
            var patientId = await CreateTestPatientAsync(client);
            var request = new
            {
                Status = "active",
                Intent = intent,
                PractitionerId = "practitioner-abc",
                MedicationCode = "654321",
                MedicationDisplay = "Test Med",
                Refills = 1,
            };

            var response = await client.PostAsJsonAsync(
                $"/fhir/Patient/{patientId}/MedicationRequest/",
                request
            );

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var medication = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(intent, medication.GetProperty("Intent").GetString());
        }
    }

    [Fact]
    public async Task GetMedicationsByPatient_ReturnsMedications_WhenExist()
    {
        using var factory = new ClinicalApiFactory();
        var client = factory.CreateClient();
        var patientId = await CreateTestPatientAsync(client);
        var request1 = new
        {
            Status = "active",
            Intent = "order",
            PractitionerId = "doc-1",
            MedicationCode = "311354",
            MedicationDisplay = "Metformin 500 MG Oral Tablet",
            Refills = 5,
        };
        var request2 = new
        {
            Status = "active",
            Intent = "order",
            PractitionerId = "doc-1",
            MedicationCode = "197361",
            MedicationDisplay = "Lisinopril 10 MG Oral Tablet",
            Refills = 3,
        };

        await client.PostAsJsonAsync($"/fhir/Patient/{patientId}/MedicationRequest/", request1);
        await client.PostAsJsonAsync($"/fhir/Patient/{patientId}/MedicationRequest/", request2);

        var response = await client.GetAsync($"/fhir/Patient/{patientId}/MedicationRequest/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var medications = await response.Content.ReadFromJsonAsync<JsonElement[]>();
        Assert.NotNull(medications);
        Assert.True(medications.Length >= 2);
    }

    [Fact]
    public async Task CreateMedicationRequest_SetsVersionIdToOne()
    {
        using var factory = new ClinicalApiFactory();
        var client = factory.CreateClient();
        var patientId = await CreateTestPatientAsync(client);
        var request = new
        {
            Status = "active",
            Intent = "order",
            PractitionerId = "doc-2",
            MedicationCode = "849727",
            MedicationDisplay = "Atorvastatin 20 MG Oral Tablet",
            Refills = 6,
        };

        var response = await client.PostAsJsonAsync(
            $"/fhir/Patient/{patientId}/MedicationRequest/",
            request
        );
        var medication = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(1L, medication.GetProperty("VersionId").GetInt64());
    }

    [Fact]
    public async Task CreateMedicationRequest_SetsAuthoredOn()
    {
        using var factory = new ClinicalApiFactory();
        var client = factory.CreateClient();
        var patientId = await CreateTestPatientAsync(client);
        var request = new
        {
            Status = "active",
            Intent = "order",
            PractitionerId = "doc-3",
            MedicationCode = "310429",
            MedicationDisplay = "Amlodipine 5 MG Oral Tablet",
            Refills = 3,
        };

        var response = await client.PostAsJsonAsync(
            $"/fhir/Patient/{patientId}/MedicationRequest/",
            request
        );
        var medication = await response.Content.ReadFromJsonAsync<JsonElement>();

        var authoredOn = medication.GetProperty("AuthoredOn").GetString();
        Assert.NotNull(authoredOn);
        Assert.Matches(@"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z", authoredOn);
    }

    [Fact]
    public async Task CreateMedicationRequest_WithQuantityAndUnit()
    {
        using var factory = new ClinicalApiFactory();
        var client = factory.CreateClient();
        var patientId = await CreateTestPatientAsync(client);
        var request = new
        {
            Status = "active",
            Intent = "order",
            PractitionerId = "doc-4",
            MedicationCode = "1049621",
            MedicationDisplay = "Omeprazole 20 MG Delayed Release Oral Capsule",
            Quantity = 90.0,
            Unit = "capsule",
            Refills = 2,
        };

        var response = await client.PostAsJsonAsync(
            $"/fhir/Patient/{patientId}/MedicationRequest/",
            request
        );
        var medication = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(90.0, medication.GetProperty("Quantity").GetDouble());
        Assert.Equal("capsule", medication.GetProperty("Unit").GetString());
    }

    [Fact]
    public async Task CreateMedicationRequest_WithDosageInstruction()
    {
        using var factory = new ClinicalApiFactory();
        var client = factory.CreateClient();
        var patientId = await CreateTestPatientAsync(client);
        var request = new
        {
            Status = "active",
            Intent = "order",
            PractitionerId = "doc-5",
            MedicationCode = "1049621",
            MedicationDisplay = "Omeprazole 20 MG Capsule",
            DosageInstruction = "Take 1 capsule by mouth 30 minutes before breakfast",
            Refills = 2,
        };

        var response = await client.PostAsJsonAsync(
            $"/fhir/Patient/{patientId}/MedicationRequest/",
            request
        );
        var medication = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            "Take 1 capsule by mouth 30 minutes before breakfast",
            medication.GetProperty("DosageInstruction").GetString()
        );
    }

    [Fact]
    public async Task CreateMedicationRequest_WithEncounterId()
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

        var medicationRequest = new
        {
            Status = "active",
            Intent = "order",
            PractitionerId = "doc-6",
            EncounterId = encounterId,
            MedicationCode = "308136",
            MedicationDisplay = "Amoxicillin 500 MG Oral Capsule",
            DosageInstruction = "Take 1 capsule by mouth three times daily for 10 days",
            Quantity = 30.0,
            Unit = "capsule",
            Refills = 0,
        };

        var response = await client.PostAsJsonAsync(
            $"/fhir/Patient/{patientId}/MedicationRequest/",
            medicationRequest
        );
        var medication = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(encounterId, medication.GetProperty("EncounterId").GetString());
    }

    [Fact]
    public async Task CreateMedicationRequest_WithZeroRefills()
    {
        using var factory = new ClinicalApiFactory();
        var client = factory.CreateClient();
        var patientId = await CreateTestPatientAsync(client);
        var request = new
        {
            Status = "active",
            Intent = "order",
            PractitionerId = "doc-7",
            MedicationCode = "562251",
            MedicationDisplay = "Prednisone 10 MG Oral Tablet",
            DosageInstruction = "Taper as directed",
            Refills = 0,
        };

        var response = await client.PostAsJsonAsync(
            $"/fhir/Patient/{patientId}/MedicationRequest/",
            request
        );
        var medication = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(0, medication.GetProperty("Refills").GetInt32());
    }
}
