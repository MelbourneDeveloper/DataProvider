namespace Clinical.Api.Tests;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

/// <summary>
/// E2E tests for Patient FHIR endpoints - REAL database, NO mocks.
/// Uses shared factory for all tests - starts once, runs all tests, shuts down.
/// </summary>
public sealed class PatientEndpointTests : IClassFixture<ClinicalApiFactory>
{
    private readonly HttpClient _client;

    /// <summary>
    /// Initializes a new instance of the <see cref="PatientEndpointTests"/> class.
    /// </summary>
    /// <param name="factory">Shared factory instance.</param>
    public PatientEndpointTests(ClinicalApiFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task GetPatients_ReturnsOk()
    {
        var response = await _client.GetAsync("/fhir/Patient/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CreatePatient_ReturnsCreated_WithValidData()
    {
        var request = new
        {
            Active = true,
            GivenName = "John",
            FamilyName = "Doe",
            BirthDate = "1990-01-15",
            Gender = "male",
            Phone = "555-1234",
            Email = "john.doe@test.com",
            AddressLine = "123 Main St",
            City = "Springfield",
            State = "IL",
            PostalCode = "62701",
            Country = "USA",
        };

        var response = await _client.PostAsJsonAsync("/fhir/Patient/", request);
        var content = await response.Content.ReadAsStringAsync();

        Assert.True(
            response.IsSuccessStatusCode,
            $"Expected success. Got {response.StatusCode}: {content}"
        );
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var json = JsonSerializer.Deserialize<JsonElement>(content);
        Assert.True(
            json.TryGetProperty("GivenName", out var givenName),
            $"Missing GivenName in: {content}"
        );
        Assert.Equal("John", givenName.GetString());
        Assert.True(
            json.TryGetProperty("FamilyName", out var familyName),
            $"Missing FamilyName in: {content}"
        );
        Assert.Equal("Doe", familyName.GetString());
        Assert.True(json.TryGetProperty("Gender", out var gender), $"Missing Gender in: {content}");
        Assert.Equal("male", gender.GetString());
        Assert.True(json.TryGetProperty("Id", out var id), $"Missing Id in: {content}");
        Assert.NotNull(id.GetString());
    }

    [Fact]
    public async Task GetPatientById_ReturnsPatient_WhenExists()
    {
        var createRequest = new
        {
            Active = true,
            GivenName = "Jane",
            FamilyName = "Smith",
            BirthDate = "1985-06-20",
            Gender = "female",
        };

        var createResponse = await _client.PostAsJsonAsync("/fhir/Patient/", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var patientId = created.GetProperty("Id").GetString();

        var response = await _client.GetAsync($"/fhir/Patient/{patientId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var patient = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Jane", patient.GetProperty("GivenName").GetString());
        Assert.Equal("Smith", patient.GetProperty("FamilyName").GetString());
    }

    [Fact]
    public async Task GetPatientById_ReturnsNotFound_WhenNotExists()
    {
        var response = await _client.GetAsync("/fhir/Patient/nonexistent-id-12345");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SearchPatients_FindsPatientsByName()
    {
        var request = new
        {
            Active = true,
            GivenName = "SearchTest",
            FamilyName = "UniqueLastName",
            Gender = "other",
        };

        await _client.PostAsJsonAsync("/fhir/Patient/", request);

        var response = await _client.GetAsync("/fhir/Patient/_search?q=UniqueLastName");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var patients = await response.Content.ReadFromJsonAsync<JsonElement[]>();
        Assert.NotNull(patients);
        Assert.Contains(patients, p => p.GetProperty("FamilyName").GetString() == "UniqueLastName");
    }

    [Fact]
    public async Task GetPatients_FiltersByActiveStatus()
    {
        var activePatient = new
        {
            Active = true,
            GivenName = "Active",
            FamilyName = "PatientFilter",
            Gender = "male",
        };

        var inactivePatient = new
        {
            Active = false,
            GivenName = "Inactive",
            FamilyName = "PatientFilter",
            Gender = "female",
        };

        await _client.PostAsJsonAsync("/fhir/Patient/", activePatient);
        await _client.PostAsJsonAsync("/fhir/Patient/", inactivePatient);

        var activeResponse = await _client.GetAsync("/fhir/Patient/?active=true");
        Assert.Equal(HttpStatusCode.OK, activeResponse.StatusCode);
        var activePatients = await activeResponse.Content.ReadFromJsonAsync<JsonElement[]>();
        Assert.NotNull(activePatients);
        Assert.All(activePatients, p => Assert.Equal(1L, p.GetProperty("Active").GetInt64()));
    }

    [Fact]
    public async Task GetPatients_FiltersByFamilyName()
    {
        var patient = new
        {
            Active = true,
            GivenName = "FilterTest",
            FamilyName = "FilterFamilyName",
            Gender = "unknown",
        };

        await _client.PostAsJsonAsync("/fhir/Patient/", patient);

        var response = await _client.GetAsync("/fhir/Patient/?familyName=FilterFamilyName");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var patients = await response.Content.ReadFromJsonAsync<JsonElement[]>();
        Assert.NotNull(patients);
        Assert.Contains(
            patients,
            p => p.GetProperty("FamilyName").GetString() == "FilterFamilyName"
        );
    }

    [Fact]
    public async Task GetPatients_FiltersByGivenName()
    {
        var patient = new
        {
            Active = true,
            GivenName = "UniqueGivenName",
            FamilyName = "TestFamily",
            Gender = "male",
        };

        await _client.PostAsJsonAsync("/fhir/Patient/", patient);

        var response = await _client.GetAsync("/fhir/Patient/?givenName=UniqueGivenName");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var patients = await response.Content.ReadFromJsonAsync<JsonElement[]>();
        Assert.NotNull(patients);
        Assert.Contains(patients, p => p.GetProperty("GivenName").GetString() == "UniqueGivenName");
    }

    [Fact]
    public async Task GetPatients_FiltersByGender()
    {
        var malePatient = new
        {
            Active = true,
            GivenName = "GenderTest",
            FamilyName = "Male",
            Gender = "male",
        };

        await _client.PostAsJsonAsync("/fhir/Patient/", malePatient);

        var response = await _client.GetAsync("/fhir/Patient/?gender=male");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var patients = await response.Content.ReadFromJsonAsync<JsonElement[]>();
        Assert.NotNull(patients);
        Assert.All(patients, p => Assert.Equal("male", p.GetProperty("Gender").GetString()));
    }

    [Fact]
    public async Task CreatePatient_GeneratesUniqueIds()
    {
        var request = new
        {
            Active = true,
            GivenName = "IdTest",
            FamilyName = "Patient",
            Gender = "other",
        };

        var response1 = await _client.PostAsJsonAsync("/fhir/Patient/", request);
        var response2 = await _client.PostAsJsonAsync("/fhir/Patient/", request);

        var patient1 = await response1.Content.ReadFromJsonAsync<JsonElement>();
        var patient2 = await response2.Content.ReadFromJsonAsync<JsonElement>();

        Assert.NotEqual(
            patient1.GetProperty("Id").GetString(),
            patient2.GetProperty("Id").GetString()
        );
    }

    [Fact]
    public async Task CreatePatient_SetsVersionIdToOne()
    {
        var request = new
        {
            Active = true,
            GivenName = "Version",
            FamilyName = "Test",
            Gender = "male",
        };

        var response = await _client.PostAsJsonAsync("/fhir/Patient/", request);
        var patient = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(1L, patient.GetProperty("VersionId").GetInt64());
    }

    [Fact]
    public async Task CreatePatient_SetsLastUpdatedTimestamp()
    {
        var request = new
        {
            Active = true,
            GivenName = "Timestamp",
            FamilyName = "Test",
            Gender = "female",
        };

        var response = await _client.PostAsJsonAsync("/fhir/Patient/", request);
        var patient = await response.Content.ReadFromJsonAsync<JsonElement>();

        var lastUpdated = patient.GetProperty("LastUpdated").GetString();
        Assert.NotNull(lastUpdated);
        Assert.Matches(@"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z", lastUpdated);
    }
}
