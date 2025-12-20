namespace Scheduling.Api.Tests;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

/// <summary>
/// E2E tests for Practitioner FHIR endpoints - REAL database, NO mocks.
/// Each test creates its own isolated factory and database.
/// </summary>
public sealed class PractitionerEndpointTests
{
    #region CORS Tests - Dashboard Integration

    [Fact]
    public async Task GetPractitioners_WithDashboardOrigin_ReturnsCorsHeaders()
    {
        // This test verifies the Dashboard (localhost:5173) can hit the Scheduling API
        // If this fails, the browser will block the request with a CORS error
        using var factory = new SchedulingApiFactory();
        var client = factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/Practitioner");
        request.Headers.Add("Origin", "http://localhost:5173");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // CRITICAL: These headers MUST be present for browser requests to work
        Assert.True(
            response.Headers.Contains("Access-Control-Allow-Origin"),
            "Missing Access-Control-Allow-Origin header - Dashboard cannot fetch from Scheduling API!"
        );

        var allowOrigin = response.Headers.GetValues("Access-Control-Allow-Origin").FirstOrDefault();
        Assert.True(
            allowOrigin == "http://localhost:5173" || allowOrigin == "*",
            $"Access-Control-Allow-Origin must allow Dashboard origin. Got: {allowOrigin}"
        );
    }

    [Fact]
    public async Task PreflightRequest_WithDashboardOrigin_ReturnsCorrectCorsHeaders()
    {
        // Browsers send OPTIONS preflight before actual requests with custom headers
        using var factory = new SchedulingApiFactory();
        var client = factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Options, "/Practitioner");
        request.Headers.Add("Origin", "http://localhost:5173");
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add("Access-Control-Request-Headers", "Content-Type");

        var response = await client.SendAsync(request);

        // Preflight should return 200 or 204
        Assert.True(
            response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.NoContent,
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
        // Appointments endpoint also needs CORS for Dashboard
        using var factory = new SchedulingApiFactory();
        var client = factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/Appointment");
        request.Headers.Add("Origin", "http://localhost:5173");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(
            response.Headers.Contains("Access-Control-Allow-Origin"),
            "Missing Access-Control-Allow-Origin on /Appointment - Dashboard cannot fetch appointments!"
        );
    }

    #endregion

    [Fact]
    public async Task GetAllPractitioners_ReturnsEmptyList_WhenNoPractitioners()
    {
        using var factory = new SchedulingApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/Practitioner");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Equal("[]", content);
    }

    [Fact]
    public async Task CreatePractitioner_ReturnsCreated_WithValidData()
    {
        using var factory = new SchedulingApiFactory();
        var client = factory.CreateClient();

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

        var response = await client.PostAsJsonAsync("/Practitioner", request);

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
        using var factory = new SchedulingApiFactory();
        var client = factory.CreateClient();

        var createRequest = new
        {
            Identifier = "NPI-GetById",
            NameFamily = "Johnson",
            NameGiven = "Jane",
            Specialty = "Pediatrics",
        };

        var createResponse = await client.PostAsJsonAsync("/Practitioner", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var practitionerId = created.GetProperty("Id").GetString();

        var response = await client.GetAsync($"/Practitioner/{practitionerId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var practitioner = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Johnson", practitioner.GetProperty("NameFamily").GetString());
        Assert.Equal("Jane", practitioner.GetProperty("NameGiven").GetString());
    }

    [Fact]
    public async Task GetPractitionerById_ReturnsNotFound_WhenNotExists()
    {
        using var factory = new SchedulingApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/Practitioner/nonexistent-id-12345");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SearchPractitionersBySpecialty_FindsPractitioners()
    {
        using var factory = new SchedulingApiFactory();
        var client = factory.CreateClient();

        var request = new
        {
            Identifier = "NPI-Search",
            NameFamily = "Williams",
            NameGiven = "Robert",
            Specialty = "Orthopedics",
        };

        await client.PostAsJsonAsync("/Practitioner", request);

        var response = await client.GetAsync("/Practitioner/_search?specialty=Orthopedics");

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
        using var factory = new SchedulingApiFactory();
        var client = factory.CreateClient();

        var request = new
        {
            Identifier = "NPI-All",
            NameFamily = "Brown",
            NameGiven = "Sarah",
            Specialty = "Dermatology",
        };

        await client.PostAsJsonAsync("/Practitioner", request);

        var response = await client.GetAsync("/Practitioner/_search");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var practitioners = await response.Content.ReadFromJsonAsync<JsonElement[]>();
        Assert.NotNull(practitioners);
        Assert.True(practitioners.Length >= 1);
    }

    [Fact]
    public async Task CreatePractitioner_SetsActiveToTrue()
    {
        using var factory = new SchedulingApiFactory();
        var client = factory.CreateClient();

        var request = new
        {
            Identifier = "NPI-Active",
            NameFamily = "Davis",
            NameGiven = "Michael",
        };

        var response = await client.PostAsJsonAsync("/Practitioner", request);
        var practitioner = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(practitioner.GetProperty("Active").GetBoolean());
    }

    [Fact]
    public async Task CreatePractitioner_GeneratesUniqueIds()
    {
        using var factory = new SchedulingApiFactory();
        var client = factory.CreateClient();

        var request = new
        {
            Identifier = "NPI-UniqueId",
            NameFamily = "Wilson",
            NameGiven = "Emily",
        };

        var response1 = await client.PostAsJsonAsync("/Practitioner", request);
        var response2 = await client.PostAsJsonAsync("/Practitioner", request);

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
        using var factory = new SchedulingApiFactory();
        var client = factory.CreateClient();

        var request = new
        {
            Identifier = "NPI-Qual",
            NameFamily = "Taylor",
            NameGiven = "Chris",
            Qualification = "MD, PhD, FACC",
        };

        var response = await client.PostAsJsonAsync("/Practitioner", request);
        var practitioner = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("MD, PhD, FACC", practitioner.GetProperty("Qualification").GetString());
    }

    [Fact]
    public async Task CreatePractitioner_WithContactInfo()
    {
        using var factory = new SchedulingApiFactory();
        var client = factory.CreateClient();

        var request = new
        {
            Identifier = "NPI-Contact",
            NameFamily = "Anderson",
            NameGiven = "Lisa",
            TelecomEmail = "lisa.anderson@clinic.com",
            TelecomPhone = "555-9876",
        };

        var response = await client.PostAsJsonAsync("/Practitioner", request);
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
        using var factory = new SchedulingApiFactory();
        var client = factory.CreateClient();

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

        await client.PostAsJsonAsync("/Practitioner", request1);
        await client.PostAsJsonAsync("/Practitioner", request2);

        var response = await client.GetAsync("/Practitioner");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var practitioners = await response.Content.ReadFromJsonAsync<JsonElement[]>();
        Assert.NotNull(practitioners);
        Assert.True(practitioners.Length >= 2);
    }
}
