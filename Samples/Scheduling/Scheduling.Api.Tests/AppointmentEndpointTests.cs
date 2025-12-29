using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Scheduling.Api.Tests;

/// <summary>
/// E2E tests for Appointment FHIR endpoints - REAL database, NO mocks.
/// Each test creates its own isolated factory and database.
/// </summary>
public sealed class AppointmentEndpointTests
{
    private static readonly string AuthToken = TestTokenHelper.GenerateSchedulerToken();

    private static HttpClient CreateAuthenticatedClient(SchedulingApiFactory factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            AuthToken
        );
        return client;
    }

    private static async Task<string> CreateTestPractitionerAsync(HttpClient client)
    {
        var request = new
        {
            Identifier = $"NPI-{Guid.NewGuid():N}",
            NameFamily = "TestDoctor",
            NameGiven = "Appointment",
            Specialty = "General Practice",
        };

        var response = await client.PostAsJsonAsync("/Practitioner", request);
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        return created.GetProperty("Id").GetString()!;
    }

    [Fact]
    public async Task GetUpcomingAppointments_ReturnsEmptyList_WhenNoAppointments()
    {
        using var factory = new SchedulingApiFactory();
        var client = CreateAuthenticatedClient(factory);

        var response = await client.GetAsync("/Appointment");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Equal("[]", content);
    }

    [Fact]
    public async Task CreateAppointment_ReturnsCreated_WithValidData()
    {
        using var factory = new SchedulingApiFactory();
        var client = CreateAuthenticatedClient(factory);
        var practitionerId = await CreateTestPractitionerAsync(client);
        var request = new
        {
            ServiceCategory = "General Practice",
            ServiceType = "Consultation",
            ReasonCode = "Annual checkup",
            Priority = "routine",
            Description = "Annual wellness visit",
            Start = "2025-06-15T09:00:00Z",
            End = "2025-06-15T09:30:00Z",
            PatientReference = "Patient/patient-123",
            PractitionerReference = $"Practitioner/{practitionerId}",
            Comment = "Please bring insurance card",
        };

        var response = await client.PostAsJsonAsync("/Appointment", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var appointment = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("booked", appointment.GetProperty("Status").GetString());
        Assert.Equal("routine", appointment.GetProperty("Priority").GetString());
        Assert.Equal(30, appointment.GetProperty("MinutesDuration").GetInt32());
        Assert.NotNull(appointment.GetProperty("Id").GetString());
    }

    [Fact]
    public async Task CreateAppointment_CalculatesDuration()
    {
        using var factory = new SchedulingApiFactory();
        var client = CreateAuthenticatedClient(factory);
        var practitionerId = await CreateTestPractitionerAsync(client);
        var request = new
        {
            ServiceCategory = "Specialty",
            ServiceType = "Extended Consultation",
            Priority = "routine",
            Start = "2025-06-15T10:00:00Z",
            End = "2025-06-15T11:00:00Z",
            PatientReference = "Patient/patient-456",
            PractitionerReference = $"Practitioner/{practitionerId}",
        };

        var response = await client.PostAsJsonAsync("/Appointment", request);
        var appointment = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(60, appointment.GetProperty("MinutesDuration").GetInt32());
    }

    [Fact]
    public async Task CreateAppointment_WithAllPriorities()
    {
        using var factory = new SchedulingApiFactory();
        var client = CreateAuthenticatedClient(factory);
        var priorities = new[] { "routine", "urgent", "asap", "stat" };

        foreach (var priority in priorities)
        {
            var practitionerId = await CreateTestPractitionerAsync(client);
            var request = new
            {
                ServiceCategory = "Test",
                ServiceType = "Priority Test",
                Priority = priority,
                Start = "2025-06-15T14:00:00Z",
                End = "2025-06-15T14:30:00Z",
                PatientReference = "Patient/patient-priority",
                PractitionerReference = $"Practitioner/{practitionerId}",
            };

            var response = await client.PostAsJsonAsync("/Appointment", request);

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var appointment = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(priority, appointment.GetProperty("Priority").GetString());
        }
    }

    [Fact]
    public async Task GetAppointmentById_ReturnsAppointment_WhenExists()
    {
        using var factory = new SchedulingApiFactory();
        var client = CreateAuthenticatedClient(factory);
        var practitionerId = await CreateTestPractitionerAsync(client);
        var createRequest = new
        {
            ServiceCategory = "Test",
            ServiceType = "GetById Test",
            Priority = "routine",
            Start = "2025-06-16T09:00:00Z",
            End = "2025-06-16T09:30:00Z",
            PatientReference = "Patient/patient-getbyid",
            PractitionerReference = $"Practitioner/{practitionerId}",
        };

        var createResponse = await client.PostAsJsonAsync("/Appointment", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var appointmentId = created.GetProperty("Id").GetString();

        var response = await client.GetAsync($"/Appointment/{appointmentId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var appointment = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("booked", appointment.GetProperty("Status").GetString());
    }

    [Fact]
    public async Task GetAppointmentById_ReturnsNotFound_WhenNotExists()
    {
        using var factory = new SchedulingApiFactory();
        var client = CreateAuthenticatedClient(factory);

        var response = await client.GetAsync("/Appointment/nonexistent-id-12345");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateAppointmentStatus_UpdatesStatus()
    {
        using var factory = new SchedulingApiFactory();
        var client = CreateAuthenticatedClient(factory);
        var practitionerId = await CreateTestPractitionerAsync(client);
        var createRequest = new
        {
            ServiceCategory = "Test",
            ServiceType = "Status Update Test",
            Priority = "routine",
            Start = "2025-06-17T10:00:00Z",
            End = "2025-06-17T10:30:00Z",
            PatientReference = "Patient/patient-status",
            PractitionerReference = $"Practitioner/{practitionerId}",
        };

        var createResponse = await client.PostAsJsonAsync("/Appointment", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var appointmentId = created.GetProperty("Id").GetString();

        var response = await client.PatchAsync(
            $"/Appointment/{appointmentId}/status?status=arrived",
            null
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("arrived", result.GetProperty("status").GetString());
    }

    [Fact]
    public async Task UpdateAppointmentStatus_ReturnsNotFound_WhenNotExists()
    {
        using var factory = new SchedulingApiFactory();
        var client = CreateAuthenticatedClient(factory);

        var response = await client.PatchAsync(
            "/Appointment/nonexistent-id/status?status=cancelled",
            null
        );

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetAppointmentsByPatient_ReturnsPatientAppointments()
    {
        using var factory = new SchedulingApiFactory();
        var client = CreateAuthenticatedClient(factory);
        var practitionerId = await CreateTestPractitionerAsync(client);
        var patientId = Guid.NewGuid().ToString();
        var request = new
        {
            ServiceCategory = "Test",
            ServiceType = "Patient Query Test",
            Priority = "routine",
            Start = "2025-06-18T11:00:00Z",
            End = "2025-06-18T11:30:00Z",
            PatientReference = $"Patient/{patientId}",
            PractitionerReference = $"Practitioner/{practitionerId}",
        };

        await client.PostAsJsonAsync("/Appointment", request);

        var response = await client.GetAsync($"/Patient/{patientId}/Appointment");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var appointments = await response.Content.ReadFromJsonAsync<JsonElement[]>();
        Assert.NotNull(appointments);
        Assert.True(appointments.Length >= 1);
    }

    [Fact]
    public async Task GetAppointmentsByPractitioner_ReturnsPractitionerAppointments()
    {
        using var factory = new SchedulingApiFactory();
        var client = CreateAuthenticatedClient(factory);
        var practitionerId = await CreateTestPractitionerAsync(client);
        var request = new
        {
            ServiceCategory = "Test",
            ServiceType = "Practitioner Query Test",
            Priority = "routine",
            Start = "2025-06-19T14:00:00Z",
            End = "2025-06-19T14:30:00Z",
            PatientReference = "Patient/patient-doc-query",
            PractitionerReference = $"Practitioner/{practitionerId}",
        };

        await client.PostAsJsonAsync("/Appointment", request);

        var response = await client.GetAsync($"/Practitioner/{practitionerId}/Appointment");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var appointments = await response.Content.ReadFromJsonAsync<JsonElement[]>();
        Assert.NotNull(appointments);
        Assert.True(appointments.Length >= 1);
    }

    [Fact]
    public async Task CreateAppointment_SetsCreatedTimestamp()
    {
        using var factory = new SchedulingApiFactory();
        var client = CreateAuthenticatedClient(factory);
        var practitionerId = await CreateTestPractitionerAsync(client);
        var request = new
        {
            ServiceCategory = "Test",
            ServiceType = "Timestamp Test",
            Priority = "routine",
            Start = "2025-06-20T15:00:00Z",
            End = "2025-06-20T15:30:00Z",
            PatientReference = "Patient/patient-timestamp",
            PractitionerReference = $"Practitioner/{practitionerId}",
        };

        var response = await client.PostAsJsonAsync("/Appointment", request);
        var appointment = await response.Content.ReadFromJsonAsync<JsonElement>();

        var created = appointment.GetProperty("Created").GetString();
        Assert.NotNull(created);
        Assert.Matches(@"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z", created);
    }

    [Fact]
    public async Task CreateAppointment_WithComment()
    {
        using var factory = new SchedulingApiFactory();
        var client = CreateAuthenticatedClient(factory);
        var practitionerId = await CreateTestPractitionerAsync(client);
        var request = new
        {
            ServiceCategory = "Test",
            ServiceType = "Comment Test",
            Priority = "routine",
            Start = "2025-06-21T09:00:00Z",
            End = "2025-06-21T09:30:00Z",
            PatientReference = "Patient/patient-comment",
            PractitionerReference = $"Practitioner/{practitionerId}",
            Comment = "Patient has mobility issues, needs wheelchair accessible room",
        };

        var response = await client.PostAsJsonAsync("/Appointment", request);
        var appointment = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            "Patient has mobility issues, needs wheelchair accessible room",
            appointment.GetProperty("Comment").GetString()
        );
    }

    [Fact]
    public async Task CreateAppointment_GeneratesUniqueIds()
    {
        using var factory = new SchedulingApiFactory();
        var client = CreateAuthenticatedClient(factory);
        var practitionerId = await CreateTestPractitionerAsync(client);
        var request = new
        {
            ServiceCategory = "Test",
            ServiceType = "Unique ID Test",
            Priority = "routine",
            Start = "2025-06-22T10:00:00Z",
            End = "2025-06-22T10:30:00Z",
            PatientReference = "Patient/patient-unique",
            PractitionerReference = $"Practitioner/{practitionerId}",
        };

        var response1 = await client.PostAsJsonAsync("/Appointment", request);
        var response2 = await client.PostAsJsonAsync("/Appointment", request);

        var appointment1 = await response1.Content.ReadFromJsonAsync<JsonElement>();
        var appointment2 = await response2.Content.ReadFromJsonAsync<JsonElement>();

        Assert.NotEqual(
            appointment1.GetProperty("Id").GetString(),
            appointment2.GetProperty("Id").GetString()
        );
    }
}
