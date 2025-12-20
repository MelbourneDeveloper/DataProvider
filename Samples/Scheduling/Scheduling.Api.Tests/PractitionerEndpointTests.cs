namespace Scheduling.Api.Tests;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

/// <summary>
/// E2E tests for Practitioner FHIR endpoints - REAL database, NO mocks.
/// </summary>
public sealed class PractitionerEndpointTests : IDisposable
{
    private readonly SchedulingApiFactory _factory;
    private readonly HttpClient _client;

    public PractitionerEndpointTests()
    {
        _factory = new SchedulingApiFactory();
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task GetAllPractitioners_ReturnsEmptyList_WhenNoPractitioners()
    {
        var response = await _client.GetAsync("/Practitioner");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Equal("[]", content);
    }

    [Fact]
    public async Task CreatePractitioner_ReturnsCreated_WithValidData()
    {
        var request = new
        {
            Identifier = "NPI-12345",
            NameFamily = "Smith",
            NameGiven = "John",
            Qualification = "MD",
            Specialty = "Cardiology",
            TelecomEmail = "dr.smith@hospital.com",
            TelecomPhone = "555-1234",
        };

        var response = await _client.PostAsJsonAsync("/Practitioner", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var practitioner = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Smith", practitioner.GetProperty("NameFamily").GetString());
        Assert.Equal("John", practitioner.GetProperty("NameGiven").GetString());
        Assert.Equal("Cardiology", practitioner.GetProperty("Specialty").GetString());
        Assert.NotNull(practitioner.GetProperty("Id").GetString());
    }

    [Fact]
    public async Task GetPractitionerById_ReturnsPractitioner_WhenExists()
    {
        var createRequest = new
        {
            Identifier = "NPI-GetById",
            NameFamily = "Johnson",
            NameGiven = "Jane",
            Specialty = "Pediatrics",
        };

        var createResponse = await _client.PostAsJsonAsync("/Practitioner", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var practitionerId = created.GetProperty("Id").GetString();

        var response = await _client.GetAsync($"/Practitioner/{practitionerId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var practitioner = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Johnson", practitioner.GetProperty("NameFamily").GetString());
        Assert.Equal("Jane", practitioner.GetProperty("NameGiven").GetString());
    }

    [Fact]
    public async Task GetPractitionerById_ReturnsNotFound_WhenNotExists()
    {
        var response = await _client.GetAsync("/Practitioner/nonexistent-id-12345");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SearchPractitionersBySpecialty_FindsPractitioners()
    {
        var request = new
        {
            Identifier = "NPI-Search",
            NameFamily = "Williams",
            NameGiven = "Robert",
            Specialty = "Orthopedics",
        };

        await _client.PostAsJsonAsync("/Practitioner", request);

        var response = await _client.GetAsync("/Practitioner/_search?specialty=Orthopedics");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var practitioners = await response.Content.ReadFromJsonAsync<JsonElement[]>();
        Assert.NotNull(practitioners);
        Assert.Contains(
            practitioners,
            p => p.GetProperty("Specialty").GetString() == "Orthopedics"
        );
    }

    [Fact]
    public async Task SearchPractitioners_WithoutSpecialty_ReturnsAll()
    {
        var request = new
        {
            Identifier = "NPI-All",
            NameFamily = "Brown",
            NameGiven = "Sarah",
            Specialty = "Dermatology",
        };

        await _client.PostAsJsonAsync("/Practitioner", request);

        var response = await _client.GetAsync("/Practitioner/_search");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var practitioners = await response.Content.ReadFromJsonAsync<JsonElement[]>();
        Assert.NotNull(practitioners);
        Assert.True(practitioners.Length >= 1);
    }

    [Fact]
    public async Task CreatePractitioner_SetsActiveToTrue()
    {
        var request = new
        {
            Identifier = "NPI-Active",
            NameFamily = "Davis",
            NameGiven = "Michael",
        };

        var response = await _client.PostAsJsonAsync("/Practitioner", request);
        var practitioner = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(practitioner.GetProperty("Active").GetBoolean());
    }

    [Fact]
    public async Task CreatePractitioner_GeneratesUniqueIds()
    {
        var request = new
        {
            Identifier = "NPI-UniqueId",
            NameFamily = "Wilson",
            NameGiven = "Emily",
        };

        var response1 = await _client.PostAsJsonAsync("/Practitioner", request);
        var response2 = await _client.PostAsJsonAsync("/Practitioner", request);

        var practitioner1 = await response1.Content.ReadFromJsonAsync<JsonElement>();
        var practitioner2 = await response2.Content.ReadFromJsonAsync<JsonElement>();

        Assert.NotEqual(
            practitioner1.GetProperty("Id").GetString(),
            practitioner2.GetProperty("Id").GetString()
        );
    }

    [Fact]
    public async Task CreatePractitioner_WithQualification()
    {
        var request = new
        {
            Identifier = "NPI-Qual",
            NameFamily = "Taylor",
            NameGiven = "Chris",
            Qualification = "MD, PhD, FACC",
        };

        var response = await _client.PostAsJsonAsync("/Practitioner", request);
        var practitioner = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("MD, PhD, FACC", practitioner.GetProperty("Qualification").GetString());
    }

    [Fact]
    public async Task CreatePractitioner_WithContactInfo()
    {
        var request = new
        {
            Identifier = "NPI-Contact",
            NameFamily = "Anderson",
            NameGiven = "Lisa",
            TelecomEmail = "lisa.anderson@clinic.com",
            TelecomPhone = "555-9876",
        };

        var response = await _client.PostAsJsonAsync("/Practitioner", request);
        var practitioner = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            "lisa.anderson@clinic.com",
            practitioner.GetProperty("TelecomEmail").GetString()
        );
        Assert.Equal("555-9876", practitioner.GetProperty("TelecomPhone").GetString());
    }

    [Fact]
    public async Task GetAllPractitioners_ReturnsPractitioners_WhenExist()
    {
        var request1 = new
        {
            Identifier = "NPI-All1",
            NameFamily = "Garcia",
            NameGiven = "Maria",
            Specialty = "Neurology",
        };
        var request2 = new
        {
            Identifier = "NPI-All2",
            NameFamily = "Martinez",
            NameGiven = "Carlos",
            Specialty = "Psychiatry",
        };

        await _client.PostAsJsonAsync("/Practitioner", request1);
        await _client.PostAsJsonAsync("/Practitioner", request2);

        var response = await _client.GetAsync("/Practitioner");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var practitioners = await response.Content.ReadFromJsonAsync<JsonElement[]>();
        Assert.NotNull(practitioners);
        Assert.True(practitioners.Length >= 2);
    }
}
