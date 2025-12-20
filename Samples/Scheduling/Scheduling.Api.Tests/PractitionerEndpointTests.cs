namespace Scheduling.Api.Tests;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

/// <summary>
/// E2E tests for Practitioner FHIR endpoints - REAL database, NO mocks.
/// Uses shared factory for all tests - starts once, runs all tests, shuts down.
/// </summary>
public sealed class PractitionerEndpointTests : IClassFixture<SchedulingApiFactory>
{
    private readonly HttpClient _client;

    /// <summary>
    /// Initializes a new instance of the <see cref="PractitionerEndpointTests"/> class.
    /// </summary>
    /// <param name="factory">Shared factory instance.</param>
    public PractitionerEndpointTests(SchedulingApiFactory factory) =>
        _client = factory.CreateClient();

    #region CORS Tests - Dashboard Integration

    [Fact]
    public async Task GetPractitioners_WithDashboardOrigin_ReturnsCorsHeaders()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/Practitioner");
        request.Headers.Add("Origin", "http://localhost:5173");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(
            response.Headers.Contains("Access-Control-Allow-Origin"),
            "Missing Access-Control-Allow-Origin header - Dashboard cannot fetch from Scheduling API!"
        );

        var allowOrigin = response
            .Headers.GetValues("Access-Control-Allow-Origin")
            .FirstOrDefault();
        Assert.True(
            allowOrigin == "http://localhost:5173" || allowOrigin == "*",
            $"Access-Control-Allow-Origin must allow Dashboard origin. Got: {allowOrigin}"
        );
    }

    [Fact]
    public async Task PreflightRequest_WithDashboardOrigin_ReturnsCorrectCorsHeaders()
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/Practitioner");
        request.Headers.Add("Origin", "http://localhost:5173");
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add("Access-Control-Request-Headers", "Content-Type");

        var response = await _client.SendAsync(request);

        Assert.True(
            response.StatusCode == HttpStatusCode.OK
                || response.StatusCode == HttpStatusCode.NoContent,
            $"Preflight request failed with {response.StatusCode}"
        );

        Assert.True(
            response.Headers.Contains("Access-Control-Allow-Origin"),
            "Missing Access-Control-Allow-Origin on preflight - Dashboard cannot make requests!"
        );

        Assert.True(
            response.Headers.Contains("Access-Control-Allow-Methods"),
            "Missing Access-Control-Allow-Methods on preflight"
        );
    }

    [Fact]
    public async Task GetAppointments_WithDashboardOrigin_ReturnsCorsHeaders()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/Appointment");
        request.Headers.Add("Origin", "http://localhost:5173");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(
            response.Headers.Contains("Access-Control-Allow-Origin"),
            "Missing Access-Control-Allow-Origin on /Appointment - Dashboard cannot fetch appointments!"
        );
    }

    #endregion

    [Fact]
    public async Task GetAllPractitioners_ReturnsOk()
    {
        var response = await _client.GetAsync("/Practitioner");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
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
