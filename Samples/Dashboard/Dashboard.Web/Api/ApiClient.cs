namespace Dashboard.Api;

using System.Text.Json;
using Dashboard.Models;
using H5;
using static H5.Core.dom;

/// <summary>
/// HTTP API client for Clinical and Scheduling microservices.
/// </summary>
public static class ApiClient
{
    private static string _clinicalBaseUrl = "http://localhost:5000";
    private static string _schedulingBaseUrl = "http://localhost:5001";

    /// <summary>
    /// Sets the base URLs for the microservices.
    /// </summary>
    public static void Configure(string clinicalUrl, string schedulingUrl)
    {
        _clinicalBaseUrl = clinicalUrl;
        _schedulingBaseUrl = schedulingUrl;
    }

    // === CLINICAL API ===

    /// <summary>
    /// Fetches all patients from the Clinical API.
    /// </summary>
    public static async Task<Patient[]> GetPatientsAsync()
    {
        var response = await FetchAsync($"{_clinicalBaseUrl}/fhir/Patient");
        return ParseJson<Patient[]>(response);
    }

    /// <summary>
    /// Fetches a patient by ID from the Clinical API.
    /// </summary>
    public static async Task<Patient?> GetPatientAsync(string id)
    {
        var response = await FetchAsync($"{_clinicalBaseUrl}/fhir/Patient/{id}");
        return ParseJson<Patient>(response);
    }

    /// <summary>
    /// Searches patients by query string.
    /// </summary>
    public static async Task<Patient[]> SearchPatientsAsync(string query)
    {
        var response = await FetchAsync($"{_clinicalBaseUrl}/fhir/Patient/_search?q={EncodeUri(query)}");
        return ParseJson<Patient[]>(response);
    }

    /// <summary>
    /// Fetches encounters for a patient.
    /// </summary>
    public static async Task<Encounter[]> GetEncountersAsync(string patientId)
    {
        var response = await FetchAsync($"{_clinicalBaseUrl}/fhir/Patient/{patientId}/Encounter");
        return ParseJson<Encounter[]>(response);
    }

    /// <summary>
    /// Fetches conditions for a patient.
    /// </summary>
    public static async Task<Condition[]> GetConditionsAsync(string patientId)
    {
        var response = await FetchAsync($"{_clinicalBaseUrl}/fhir/Patient/{patientId}/Condition");
        return ParseJson<Condition[]>(response);
    }

    /// <summary>
    /// Fetches medications for a patient.
    /// </summary>
    public static async Task<MedicationRequest[]> GetMedicationsAsync(string patientId)
    {
        var response = await FetchAsync($"{_clinicalBaseUrl}/fhir/Patient/{patientId}/MedicationRequest");
        return ParseJson<MedicationRequest[]>(response);
    }

    // === SCHEDULING API ===

    /// <summary>
    /// Fetches all practitioners from the Scheduling API.
    /// </summary>
    public static async Task<Practitioner[]> GetPractitionersAsync()
    {
        var response = await FetchAsync($"{_schedulingBaseUrl}/Practitioner");
        return ParseJson<Practitioner[]>(response);
    }

    /// <summary>
    /// Fetches a practitioner by ID from the Scheduling API.
    /// </summary>
    public static async Task<Practitioner?> GetPractitionerAsync(string id)
    {
        var response = await FetchAsync($"{_schedulingBaseUrl}/Practitioner/{id}");
        return ParseJson<Practitioner>(response);
    }

    /// <summary>
    /// Searches practitioners by specialty.
    /// </summary>
    public static async Task<Practitioner[]> SearchPractitionersAsync(string specialty)
    {
        var response = await FetchAsync($"{_schedulingBaseUrl}/Practitioner/_search?specialty={EncodeUri(specialty)}");
        return ParseJson<Practitioner[]>(response);
    }

    /// <summary>
    /// Fetches all appointments from the Scheduling API.
    /// </summary>
    public static async Task<Appointment[]> GetAppointmentsAsync()
    {
        var response = await FetchAsync($"{_schedulingBaseUrl}/Appointment");
        return ParseJson<Appointment[]>(response);
    }

    /// <summary>
    /// Fetches an appointment by ID from the Scheduling API.
    /// </summary>
    public static async Task<Appointment?> GetAppointmentAsync(string id)
    {
        var response = await FetchAsync($"{_schedulingBaseUrl}/Appointment/{id}");
        return ParseJson<Appointment>(response);
    }

    /// <summary>
    /// Fetches appointments for a patient.
    /// </summary>
    public static async Task<Appointment[]> GetPatientAppointmentsAsync(string patientId)
    {
        var response = await FetchAsync($"{_schedulingBaseUrl}/Patient/{patientId}/Appointment");
        return ParseJson<Appointment[]>(response);
    }

    /// <summary>
    /// Fetches appointments for a practitioner.
    /// </summary>
    public static async Task<Appointment[]> GetPractitionerAppointmentsAsync(string practitionerId)
    {
        var response = await FetchAsync($"{_schedulingBaseUrl}/Practitioner/{practitionerId}/Appointment");
        return ParseJson<Appointment[]>(response);
    }

    // === HELPER METHODS ===

    private static async Task<string> FetchAsync(string url)
    {
        var response = await Script.Call<Task<Response>>("fetch", url, new
        {
            method = "GET",
            headers = new
            {
                Accept = "application/json"
            }
        });

        if (!response.Ok)
        {
            throw new Exception($"HTTP {response.Status}: {response.StatusText}");
        }

        return await response.Text();
    }

    private static async Task<string> PostAsync(string url, object data)
    {
        var response = await Script.Call<Task<Response>>("fetch", url, new
        {
            method = "POST",
            headers = new
            {
                Accept = "application/json",
                ContentType = "application/json"
            },
            body = Script.Call<string>("JSON.stringify", data)
        });

        if (!response.Ok)
        {
            throw new Exception($"HTTP {response.Status}: {response.StatusText}");
        }

        return await response.Text();
    }

    private static T ParseJson<T>(string json) =>
        Script.Call<T>("JSON.parse", json);

    private static string EncodeUri(string value) =>
        Script.Call<string>("encodeURIComponent", value);
}

/// <summary>
/// Fetch API Response type.
/// </summary>
[External]
[Name("Response")]
public class Response
{
    /// <summary>Whether the response was successful.</summary>
    public extern bool Ok { get; }

    /// <summary>HTTP status code.</summary>
    public extern int Status { get; }

    /// <summary>HTTP status text.</summary>
    public extern string StatusText { get; }

    /// <summary>Gets the response body as text.</summary>
    public extern Task<string> Text();

    /// <summary>Gets the response body as JSON.</summary>
    public extern Task<object> Json();
}
