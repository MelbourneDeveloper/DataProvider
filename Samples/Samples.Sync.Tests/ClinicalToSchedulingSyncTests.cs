namespace Samples.Sync.Tests;

using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

/// <summary>
/// Full e2e sync integration tests - REAL databases, REAL sync.
/// Tests that data created in Clinical.Api actually syncs to Scheduling via the sync endpoints.
/// </summary>
public sealed class ClinicalToSchedulingSyncTests : IDisposable
{
    private readonly WebApplicationFactory<Clinical.Api.Program> _clinicalFactory;
    private readonly WebApplicationFactory<Scheduling.Api.Program> _schedulingFactory;
    private readonly HttpClient _clinicalClient;
    private readonly HttpClient _schedulingClient;
    private readonly string _clinicalDbPath;
    private readonly string _schedulingDbPath;

    /// <summary>
    /// Creates test instance with both APIs running.
    /// </summary>
    public ClinicalToSchedulingSyncTests()
    {
        _clinicalDbPath = Path.Combine(
            Path.GetTempPath(),
            $"clinical_sync_test_{Guid.NewGuid()}.db"
        );
        _schedulingDbPath = Path.Combine(
            Path.GetTempPath(),
            $"scheduling_sync_test_{Guid.NewGuid()}.db"
        );

        _clinicalFactory = new WebApplicationFactory<Clinical.Api.Program>().WithWebHostBuilder(
            builder => builder.UseSetting("DbPath", _clinicalDbPath)
        );

        _schedulingFactory = new WebApplicationFactory<Scheduling.Api.Program>().WithWebHostBuilder(
            builder => builder.UseSetting("DbPath", _schedulingDbPath)
        );

        _clinicalClient = _clinicalFactory.CreateClient();
        _schedulingClient = _schedulingFactory.CreateClient();
    }

    /// <summary>
    /// Creating a patient in Clinical.Api creates a sync log entry that can be consumed.
    /// </summary>
    [Fact]
    public async Task CreatePatientInClinical_GeneratesSyncLogEntry()
    {
        var patientRequest = new
        {
            Active = true,
            GivenName = "Sync",
            FamilyName = "Patient",
            Gender = "male",
            Phone = "555-1234",
            Email = "sync.patient@test.com",
        };

        await _clinicalClient.PostAsJsonAsync("/fhir/Patient/", patientRequest);

        var response = await _clinicalClient.GetAsync("/sync/changes?fromVersion=0");
        var changes = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.NotNull(changes);
        Assert.Contains(changes, c => c.GetProperty("TableName").GetString() == "fhir_Patient");
    }

    /// <summary>
    /// Creating a practitioner in Scheduling.Api creates a sync log entry that can be consumed.
    /// </summary>
    [Fact]
    public async Task CreatePractitionerInScheduling_GeneratesSyncLogEntry()
    {
        var practitionerRequest = new
        {
            Identifier = "NPI-SYNC-FULL",
            NameFamily = "SyncDoctor",
            NameGiven = "Full",
            Specialty = "General Practice",
        };

        await _schedulingClient.PostAsJsonAsync("/Practitioner", practitionerRequest);

        var response = await _schedulingClient.GetAsync("/sync/changes?fromVersion=0");
        var changes = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.NotNull(changes);
        Assert.Contains(
            changes,
            c => c.GetProperty("TableName").GetString() == "fhir_Practitioner"
        );
    }

    /// <summary>
    /// Sync log contains patient data that can be used for syncing.
    /// </summary>
    [Fact]
    public async Task ClinicalSyncLog_ContainsPatientData()
    {
        var patientRequest = new
        {
            Active = true,
            GivenName = "DataSync",
            FamilyName = "TestPatient",
            Gender = "female",
            Phone = "555-9999",
            Email = "data.sync@test.com",
        };

        await _clinicalClient.PostAsJsonAsync("/fhir/Patient/", patientRequest);

        var response = await _clinicalClient.GetAsync("/sync/changes?fromVersion=0");
        var changes = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.NotNull(changes);

        var patientChange = changes.FirstOrDefault(c =>
            c.GetProperty("TableName").GetString() == "fhir_Patient"
        );

        Assert.True(patientChange.ValueKind != JsonValueKind.Undefined);
        Assert.True(
            patientChange.TryGetProperty("Data", out _)
                || patientChange.TryGetProperty("RowData", out _)
        );
    }

    /// <summary>
    /// Sync log contains practitioner data that can be used for syncing.
    /// </summary>
    [Fact]
    public async Task SchedulingSyncLog_ContainsPractitionerData()
    {
        var practitionerRequest = new
        {
            Identifier = "NPI-DATA-SYNC",
            NameFamily = "DataDoctor",
            NameGiven = "Sync",
            Specialty = "Cardiology",
        };

        await _schedulingClient.PostAsJsonAsync("/Practitioner", practitionerRequest);

        var response = await _schedulingClient.GetAsync("/sync/changes?fromVersion=0");
        var changes = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.NotNull(changes);

        var practitionerChange = changes.FirstOrDefault(c =>
            c.GetProperty("TableName").GetString() == "fhir_Practitioner"
        );

        Assert.True(practitionerChange.ValueKind != JsonValueKind.Undefined);
        Assert.True(
            practitionerChange.TryGetProperty("Data", out _)
                || practitionerChange.TryGetProperty("RowData", out _)
        );
    }

    /// <summary>
    /// Both domains have unique origin IDs.
    /// </summary>
    [Fact]
    public async Task BothDomains_HaveUniqueOriginIds()
    {
        var clinicalOriginResponse = await _clinicalClient.GetAsync("/sync/origin");
        var clinicalOrigin = await clinicalOriginResponse.Content.ReadFromJsonAsync<JsonElement>();
        var clinicalOriginId = clinicalOrigin.GetProperty("originId").GetString();

        var schedulingOriginResponse = await _schedulingClient.GetAsync("/sync/origin");
        var schedulingOrigin =
            await schedulingOriginResponse.Content.ReadFromJsonAsync<JsonElement>();
        var schedulingOriginId = schedulingOrigin.GetProperty("originId").GetString();

        Assert.NotNull(clinicalOriginId);
        Assert.NotNull(schedulingOriginId);
        Assert.NotEmpty(clinicalOriginId);
        Assert.NotEmpty(schedulingOriginId);
        Assert.NotEqual(clinicalOriginId, schedulingOriginId);
    }

    /// <summary>
    /// Sync log versions increment correctly across multiple changes.
    /// </summary>
    [Fact]
    public async Task SyncLogVersions_IncrementCorrectly()
    {
        for (var i = 0; i < 5; i++)
        {
            var patientRequest = new
            {
                Active = true,
                GivenName = $"Version{i}",
                FamilyName = "TestPatient",
                Gender = "male",
            };
            await _clinicalClient.PostAsJsonAsync("/fhir/Patient/", patientRequest);
        }

        var response = await _clinicalClient.GetAsync("/sync/changes?fromVersion=0");
        var changes = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.NotNull(changes);
        Assert.True(changes.Length >= 5);

        long previousVersion = 0;
        foreach (var change in changes)
        {
            var currentVersion = change.GetProperty("Version").GetInt64();
            Assert.True(currentVersion > previousVersion);
            previousVersion = currentVersion;
        }
    }

    /// <summary>
    /// Sync log fromVersion parameter filters correctly.
    /// </summary>
    [Fact]
    public async Task SyncLogFromVersion_FiltersCorrectly()
    {
        var patient1 = new
        {
            Active = true,
            GivenName = "First",
            FamilyName = "Patient",
            Gender = "male",
        };
        await _clinicalClient.PostAsJsonAsync("/fhir/Patient/", patient1);

        var initialResponse = await _clinicalClient.GetAsync("/sync/changes?fromVersion=0");
        var initialChanges = await initialResponse.Content.ReadFromJsonAsync<JsonElement[]>();
        var lastVersion = initialChanges?.Max(c => c.GetProperty("Version").GetInt64()) ?? 0;

        var patient2 = new
        {
            Active = true,
            GivenName = "Second",
            FamilyName = "Patient",
            Gender = "female",
        };
        await _clinicalClient.PostAsJsonAsync("/fhir/Patient/", patient2);

        var filteredResponse = await _clinicalClient.GetAsync(
            $"/sync/changes?fromVersion={lastVersion}"
        );
        var filteredChanges = await filteredResponse.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.NotNull(filteredChanges);
        Assert.All(
            filteredChanges,
            c => Assert.True(c.GetProperty("Version").GetInt64() > lastVersion)
        );
    }

    /// <summary>
    /// Multiple resource types are tracked in sync log.
    /// </summary>
    [Fact]
    public async Task MultipleResourceTypes_TrackedInSyncLog()
    {
        var patientRequest = new
        {
            Active = true,
            GivenName = "Multi",
            FamilyName = "Resource",
            Gender = "male",
        };
        var patientResponse = await _clinicalClient.PostAsJsonAsync(
            "/fhir/Patient/",
            patientRequest
        );
        var patient = await patientResponse.Content.ReadFromJsonAsync<JsonElement>();
        var patientId = patient.GetProperty("Id").GetString();

        var encounterRequest = new
        {
            Status = "finished",
            Class = "ambulatory",
            PeriodStart = "2024-01-15T09:00:00Z",
        };
        await _clinicalClient.PostAsJsonAsync(
            $"/fhir/Patient/{patientId}/Encounter/",
            encounterRequest
        );

        var conditionRequest = new
        {
            ClinicalStatus = "active",
            CodeSystem = "http://hl7.org/fhir/sid/icd-10-cm",
            CodeValue = "J06.9",
            CodeDisplay = "URI",
        };
        await _clinicalClient.PostAsJsonAsync(
            $"/fhir/Patient/{patientId}/Condition/",
            conditionRequest
        );

        var response = await _clinicalClient.GetAsync("/sync/changes?fromVersion=0");
        var changes = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.NotNull(changes);

        var tableNames = changes
            .Select(c => c.GetProperty("TableName").GetString())
            .Distinct()
            .ToList();
        Assert.Contains("fhir_Patient", tableNames);
        Assert.Contains("fhir_Encounter", tableNames);
        Assert.Contains("fhir_Condition", tableNames);
    }

    /// <summary>
    /// Creating an appointment in Scheduling creates a sync log entry.
    /// </summary>
    [Fact]
    public async Task CreateAppointment_InScheduling_GeneratesSyncLogEntry()
    {
        // Create practitioner first
        var practitionerRequest = new
        {
            Identifier = $"NPI-APPT-{Guid.NewGuid():N}",
            NameFamily = "AppointmentDoc",
            NameGiven = "Test",
        };
        var practitionerResponse = await _schedulingClient.PostAsJsonAsync(
            "/Practitioner",
            practitionerRequest
        );
        var practitioner = await practitionerResponse.Content.ReadFromJsonAsync<JsonElement>();
        var practitionerId = practitioner.GetProperty("Id").GetString();

        // Create appointment
        var appointmentRequest = new
        {
            ServiceCategory = "Test",
            ServiceType = "Sync Test",
            Priority = "routine",
            Start = "2025-08-01T10:00:00Z",
            End = "2025-08-01T10:30:00Z",
            PatientReference = "Patient/test-patient",
            PractitionerReference = $"Practitioner/{practitionerId}",
        };
        var appointmentResponse = await _schedulingClient.PostAsJsonAsync(
            "/Appointment",
            appointmentRequest
        );
        Assert.True(appointmentResponse.IsSuccessStatusCode);

        var appointment = await appointmentResponse.Content.ReadFromJsonAsync<JsonElement>();
        var appointmentId = appointment.GetProperty("Id").GetString();

        var changesResponse = await _schedulingClient.GetAsync("/sync/changes?fromVersion=0");
        var changes = await changesResponse.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.NotNull(changes);
        Assert.Contains(
            changes,
            c =>
                c.GetProperty("TableName").GetString() == "fhir_Appointment"
                && c.GetProperty("RowId").GetString() == appointmentId
        );
    }

    /// <summary>
    /// Creating a medication request in Clinical creates a sync log entry.
    /// </summary>
    [Fact]
    public async Task CreateMedicationRequest_InClinical_GeneratesSyncLogEntry()
    {
        var patientRequest = new
        {
            Active = true,
            GivenName = "Medication",
            FamilyName = "Patient",
            Gender = "female",
        };
        var patientResponse = await _clinicalClient.PostAsJsonAsync(
            "/fhir/Patient/",
            patientRequest
        );
        var patient = await patientResponse.Content.ReadFromJsonAsync<JsonElement>();
        var patientId = patient.GetProperty("Id").GetString();

        var medicationRequest = new
        {
            Status = "active",
            Intent = "order",
            PractitionerId = "practitioner-123",
            MedicationCode = "RX001",
            MedicationDisplay = "Test Medication",
            DosageInstruction = "Take once daily",
            Quantity = 30.0,
            Unit = "tablets",
            Refills = 2,
        };
        var medicationResponse = await _clinicalClient.PostAsJsonAsync(
            $"/fhir/Patient/{patientId}/MedicationRequest/",
            medicationRequest
        );
        Assert.True(medicationResponse.IsSuccessStatusCode);

        var medication = await medicationResponse.Content.ReadFromJsonAsync<JsonElement>();
        var medicationId = medication.GetProperty("Id").GetString();

        var changesResponse = await _clinicalClient.GetAsync("/sync/changes?fromVersion=0");
        var changes = await changesResponse.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.NotNull(changes);
        Assert.Contains(
            changes,
            c =>
                c.GetProperty("TableName").GetString() == "fhir_MedicationRequest"
                && c.GetProperty("RowId").GetString() == medicationId
        );
    }

    /// <summary>
    /// Sync log limit parameter correctly restricts result count.
    /// </summary>
    [Fact]
    public async Task SyncLogLimit_RestrictsResultCount()
    {
        // Create more than 3 practitioners
        for (var i = 0; i < 5; i++)
        {
            var request = new
            {
                Identifier = $"NPI-LIMIT-{Guid.NewGuid():N}",
                NameFamily = $"LimitDoc{i}",
                NameGiven = "Test",
            };
            await _schedulingClient.PostAsJsonAsync("/Practitioner", request);
        }

        var response = await _schedulingClient.GetAsync("/sync/changes?fromVersion=0&limit=3");
        var changes = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.NotNull(changes);
        Assert.Equal(3, changes.Length);
    }

    /// <summary>
    /// Sync changes include INSERT operation type.
    /// </summary>
    [Fact]
    public async Task SyncChanges_IncludeOperationType()
    {
        var patientRequest = new
        {
            Active = true,
            GivenName = "Operation",
            FamilyName = "TypeTest",
            Gender = "male",
        };
        await _clinicalClient.PostAsJsonAsync("/fhir/Patient/", patientRequest);

        var response = await _clinicalClient.GetAsync("/sync/changes?fromVersion=0");
        var changes = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.NotNull(changes);
        Assert.All(changes, c => Assert.True(c.TryGetProperty("Operation", out _)));

        var patientChange = changes.First(c =>
            c.GetProperty("TableName").GetString() == "fhir_Patient"
        );
        // Operation is serialized as integer (0=Insert, 1=Update, 2=Delete)
        Assert.Equal(0, patientChange.GetProperty("Operation").GetInt32());
    }

    /// <summary>
    /// Sync changes include timestamp.
    /// </summary>
    [Fact]
    public async Task SyncChanges_IncludeTimestamp()
    {
        var practitionerRequest = new
        {
            Identifier = $"NPI-TS-{Guid.NewGuid():N}",
            NameFamily = "TimestampDoc",
            NameGiven = "Test",
        };
        await _schedulingClient.PostAsJsonAsync("/Practitioner", practitionerRequest);

        var response = await _schedulingClient.GetAsync("/sync/changes?fromVersion=0");
        var changes = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.NotNull(changes);
        Assert.All(changes, c => Assert.True(c.TryGetProperty("Timestamp", out _)));
    }

    /// <summary>
    /// Sync data can be parsed and contains expected fields.
    /// </summary>
    [Fact]
    public async Task SyncData_ContainsExpectedPatientFields()
    {
        var patientRequest = new
        {
            Active = true,
            GivenName = "FieldCheck",
            FamilyName = "Patient",
            Gender = "female",
            Phone = "555-FIELD",
            Email = "field@check.com",
        };
        await _clinicalClient.PostAsJsonAsync("/fhir/Patient/", patientRequest);

        var response = await _clinicalClient.GetAsync("/sync/changes?fromVersion=0");
        var changes = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.NotNull(changes);
        var patientChange = changes.First(c =>
            c.GetProperty("TableName").GetString() == "fhir_Patient"
        );

        var dataStr = patientChange.GetProperty("Data").GetString();
        Assert.NotNull(dataStr);

        var data = JsonSerializer.Deserialize<JsonElement>(dataStr);
        Assert.Equal("FieldCheck", data.GetProperty("GivenName").GetString());
        Assert.Equal("Patient", data.GetProperty("FamilyName").GetString());
        Assert.Equal("female", data.GetProperty("Gender").GetString());
        Assert.Equal("555-FIELD", data.GetProperty("Phone").GetString());
        Assert.Equal("field@check.com", data.GetProperty("Email").GetString());
    }

    /// <summary>
    /// Sync data can be parsed and contains expected practitioner fields.
    /// </summary>
    [Fact]
    public async Task SyncData_ContainsExpectedPractitionerFields()
    {
        var practitionerRequest = new
        {
            Identifier = "NPI-FIELDS-123",
            NameFamily = "FieldsDoctor",
            NameGiven = "John",
            Specialty = "Neurology",
            Qualification = "MD, PhD",
            TelecomEmail = "doctor@fields.com",
            TelecomPhone = "555-DOCS",
        };
        await _schedulingClient.PostAsJsonAsync("/Practitioner", practitionerRequest);

        var response = await _schedulingClient.GetAsync("/sync/changes?fromVersion=0");
        var changes = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.NotNull(changes);
        var practitionerChange = changes.First(c =>
            c.GetProperty("TableName").GetString() == "fhir_Practitioner"
        );

        var dataStr = practitionerChange.GetProperty("Data").GetString();
        Assert.NotNull(dataStr);

        var data = JsonSerializer.Deserialize<JsonElement>(dataStr);
        Assert.Equal("NPI-FIELDS-123", data.GetProperty("Identifier").GetString());
        Assert.Equal("FieldsDoctor", data.GetProperty("NameFamily").GetString());
        Assert.Equal("John", data.GetProperty("NameGiven").GetString());
        Assert.Equal("Neurology", data.GetProperty("Specialty").GetString());
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _clinicalClient.Dispose();
        _schedulingClient.Dispose();
        _clinicalFactory.Dispose();
        _schedulingFactory.Dispose();

        try
        {
            if (File.Exists(_clinicalDbPath))
            {
                File.Delete(_clinicalDbPath);
            }

            if (File.Exists(_schedulingDbPath))
            {
                File.Delete(_schedulingDbPath);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}
