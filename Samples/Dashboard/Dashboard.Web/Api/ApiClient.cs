using System;
using System.Threading.Tasks;
using Dashboard.Models;
using H5;
using static H5.Core.dom;

namespace Dashboard.Api
{
    /// <summary>
    /// HTTP API client for Clinical and Scheduling microservices.
    /// </summary>
    public static class ApiClient
    {
        private static string _clinicalBaseUrl = "http://localhost:5080";
        private static string _schedulingBaseUrl = "http://localhost:5001";
        private static string _clinicalToken = "";
        private static string _schedulingToken = "";

        /// <summary>
        /// Sets the base URLs for the microservices.
        /// </summary>
        public static void Configure(string clinicalUrl, string schedulingUrl)
        {
            _clinicalBaseUrl = clinicalUrl;
            _schedulingBaseUrl = schedulingUrl;
        }

        /// <summary>
        /// Sets the authentication tokens for the microservices.
        /// </summary>
        public static void SetTokens(string clinicalToken, string schedulingToken)
        {
            _clinicalToken = clinicalToken;
            _schedulingToken = schedulingToken;
        }

        // === CLINICAL API ===

        /// <summary>
        /// Fetches all patients from the Clinical API.
        /// </summary>
        public static async Task<Patient[]> GetPatientsAsync()
        {
            var response = await FetchClinicalAsync(_clinicalBaseUrl + "/fhir/Patient");
            return ParseJson<Patient[]>(response);
        }

        /// <summary>
        /// Fetches a patient by ID from the Clinical API.
        /// </summary>
        public static async Task<Patient> GetPatientAsync(string id)
        {
            var response = await FetchClinicalAsync(_clinicalBaseUrl + "/fhir/Patient/" + id);
            return ParseJson<Patient>(response);
        }

        /// <summary>
        /// Searches patients by query string.
        /// </summary>
        public static async Task<Patient[]> SearchPatientsAsync(string query)
        {
            var response = await FetchClinicalAsync(
                _clinicalBaseUrl + "/fhir/Patient/_search?q=" + EncodeUri(query)
            );
            return ParseJson<Patient[]>(response);
        }

        /// <summary>
        /// Fetches encounters for a patient.
        /// </summary>
        public static async Task<Encounter[]> GetEncountersAsync(string patientId)
        {
            var response = await FetchClinicalAsync(
                _clinicalBaseUrl + "/fhir/Patient/" + patientId + "/Encounter"
            );
            return ParseJson<Encounter[]>(response);
        }

        /// <summary>
        /// Fetches conditions for a patient.
        /// </summary>
        public static async Task<Condition[]> GetConditionsAsync(string patientId)
        {
            var response = await FetchClinicalAsync(
                _clinicalBaseUrl + "/fhir/Patient/" + patientId + "/Condition"
            );
            return ParseJson<Condition[]>(response);
        }

        /// <summary>
        /// Fetches medications for a patient.
        /// </summary>
        public static async Task<MedicationRequest[]> GetMedicationsAsync(string patientId)
        {
            var response = await FetchClinicalAsync(
                _clinicalBaseUrl + "/fhir/Patient/" + patientId + "/MedicationRequest"
            );
            return ParseJson<MedicationRequest[]>(response);
        }

        /// <summary>
        /// Creates a new patient.
        /// </summary>
        public static async Task<Patient> CreatePatientAsync(Patient patient)
        {
            var response = await PostClinicalAsync(_clinicalBaseUrl + "/fhir/Patient/", patient);
            return ParseJson<Patient>(response);
        }

        /// <summary>
        /// Updates an existing patient.
        /// </summary>
        public static async Task<Patient> UpdatePatientAsync(string id, Patient patient)
        {
            var response = await PutClinicalAsync(_clinicalBaseUrl + "/fhir/Patient/" + id, patient);
            return ParseJson<Patient>(response);
        }

        // === SCHEDULING API ===

        /// <summary>
        /// Fetches all practitioners from the Scheduling API.
        /// </summary>
        public static async Task<Practitioner[]> GetPractitionersAsync()
        {
            var response = await FetchSchedulingAsync(_schedulingBaseUrl + "/Practitioner");
            return ParseJson<Practitioner[]>(response);
        }

        /// <summary>
        /// Fetches a practitioner by ID from the Scheduling API.
        /// </summary>
        public static async Task<Practitioner> GetPractitionerAsync(string id)
        {
            var response = await FetchSchedulingAsync(_schedulingBaseUrl + "/Practitioner/" + id);
            return ParseJson<Practitioner>(response);
        }

        /// <summary>
        /// Searches practitioners by specialty.
        /// </summary>
        public static async Task<Practitioner[]> SearchPractitionersAsync(string specialty)
        {
            var response = await FetchSchedulingAsync(
                _schedulingBaseUrl + "/Practitioner/_search?specialty=" + EncodeUri(specialty)
            );
            return ParseJson<Practitioner[]>(response);
        }

        /// <summary>
        /// Fetches all appointments from the Scheduling API.
        /// </summary>
        public static async Task<Appointment[]> GetAppointmentsAsync()
        {
            var response = await FetchSchedulingAsync(_schedulingBaseUrl + "/Appointment");
            return ParseJson<Appointment[]>(response);
        }

        /// <summary>
        /// Fetches an appointment by ID from the Scheduling API.
        /// </summary>
        public static async Task<Appointment> GetAppointmentAsync(string id)
        {
            var response = await FetchSchedulingAsync(_schedulingBaseUrl + "/Appointment/" + id);
            return ParseJson<Appointment>(response);
        }

        /// <summary>
        /// Updates an existing appointment.
        /// </summary>
        public static async Task<Appointment> UpdateAppointmentAsync(string id, object appointment)
        {
            var response = await PutSchedulingAsync(_schedulingBaseUrl + "/Appointment/" + id, appointment);
            return ParseJson<Appointment>(response);
        }

        /// <summary>
        /// Fetches appointments for a patient.
        /// </summary>
        public static async Task<Appointment[]> GetPatientAppointmentsAsync(string patientId)
        {
            var response = await FetchSchedulingAsync(
                _schedulingBaseUrl + "/Patient/" + patientId + "/Appointment"
            );
            return ParseJson<Appointment[]>(response);
        }

        /// <summary>
        /// Fetches appointments for a practitioner.
        /// </summary>
        public static async Task<Appointment[]> GetPractitionerAppointmentsAsync(
            string practitionerId
        )
        {
            var response = await FetchSchedulingAsync(
                _schedulingBaseUrl + "/Practitioner/" + practitionerId + "/Appointment"
            );
            return ParseJson<Appointment[]>(response);
        }

        // === HELPER METHODS ===

        private static async Task<string> FetchClinicalAsync(string url)
        {
            var response = await Script.Call<Task<Response>>(
                "fetch",
                url,
                new { method = "GET", headers = new { Accept = "application/json", Authorization = "Bearer " + _clinicalToken } }
            );

            if (!response.Ok)
            {
                throw new Exception("HTTP " + response.Status);
            }

            return await response.Text();
        }

        private static async Task<string> FetchSchedulingAsync(string url)
        {
            var response = await Script.Call<Task<Response>>(
                "fetch",
                url,
                new { method = "GET", headers = new { Accept = "application/json", Authorization = "Bearer " + _schedulingToken } }
            );

            if (!response.Ok)
            {
                throw new Exception("HTTP " + response.Status);
            }

            return await response.Text();
        }

        private static async Task<string> PostClinicalAsync(string url, object data)
        {
            var response = await Script.Call<Task<Response>>(
                "fetch",
                url,
                new
                {
                    method = "POST",
                    headers = new { Accept = "application/json", ContentType = "application/json", Authorization = "Bearer " + _clinicalToken },
                    body = Script.Call<string>("JSON.stringify", data),
                }
            );

            if (!response.Ok)
            {
                throw new Exception("HTTP " + response.Status);
            }

            return await response.Text();
        }

        private static async Task<string> PutClinicalAsync(string url, object data)
        {
            var response = await Script.Call<Task<Response>>(
                "fetch",
                url,
                new
                {
                    method = "PUT",
                    headers = new { Accept = "application/json", ContentType = "application/json", Authorization = "Bearer " + _clinicalToken },
                    body = Script.Call<string>("JSON.stringify", data),
                }
            );

            if (!response.Ok)
            {
                throw new Exception("HTTP " + response.Status);
            }

            return await response.Text();
        }

        private static async Task<string> PutSchedulingAsync(string url, object data)
        {
            var response = await Script.Call<Task<Response>>(
                "fetch",
                url,
                new
                {
                    method = "PUT",
                    headers = new { Accept = "application/json", ContentType = "application/json", Authorization = "Bearer " + _schedulingToken },
                    body = Script.Call<string>("JSON.stringify", data),
                }
            );

            if (!response.Ok)
            {
                throw new Exception("HTTP " + response.Status);
            }

            return await response.Text();
        }

        private static T ParseJson<T>(string json) => Script.Call<T>("JSON.parse", json);

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
}
