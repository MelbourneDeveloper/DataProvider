using System;
using System.Threading.Tasks;
using H5;
using static H5.Core.dom;

namespace Reporting.React.Api
{
    /// <summary>
    /// Client for the Reporting API.
    /// </summary>
    public static class ReportApiClient
    {
        private static string _baseUrl = "";

        /// <summary>
        /// Configures the API base URL.
        /// </summary>
        public static void Configure(string baseUrl)
        {
            _baseUrl = baseUrl;
        }

        /// <summary>
        /// Fetches the list of available reports.
        /// </summary>
        public static async Task<object[]> GetReportsAsync()
        {
            var response = await FetchAsync(_baseUrl + "/api/reports");
            return ParseJson<object[]>(response);
        }

        /// <summary>
        /// Fetches a report definition (metadata + layout).
        /// </summary>
        public static async Task<object> GetReportAsync(string reportId)
        {
            var response = await FetchAsync(_baseUrl + "/api/reports/" + reportId);
            return ParseJson<object>(response);
        }

        /// <summary>
        /// Executes a report with parameters and returns data.
        /// </summary>
        public static async Task<object> ExecuteReportAsync(string reportId, object parameters)
        {
            var body = new { parameters = parameters, format = "json" };
            var response = await PostAsync(
                _baseUrl + "/api/reports/" + reportId + "/execute",
                body
            );
            return ParseJson<object>(response);
        }

        private static async Task<string> FetchAsync(string url)
        {
            var response = await Script.Call<Task<FetchResponse>>(
                "fetch",
                url,
                new { method = "GET", headers = new { Accept = "application/json" } }
            );

            if (!response.Ok)
            {
                throw new Exception("HTTP " + response.Status);
            }

            return await response.Text();
        }

        private static async Task<string> PostAsync(string url, object data)
        {
            var response = await Script.Call<Task<FetchResponse>>(
                "fetch",
                url,
                new
                {
                    method = "POST",
                    headers = new { Accept = "application/json", ContentType = "application/json" },
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
    }

    /// <summary>
    /// Fetch API Response type.
    /// </summary>
    [External]
    [Name("Response")]
    public class FetchResponse
    {
        /// <summary>Whether the response was successful.</summary>
        public extern bool Ok { get; }

        /// <summary>HTTP status code.</summary>
        public extern int Status { get; }

        /// <summary>Gets the response body as text.</summary>
        public extern Task<string> Text();
    }
}
