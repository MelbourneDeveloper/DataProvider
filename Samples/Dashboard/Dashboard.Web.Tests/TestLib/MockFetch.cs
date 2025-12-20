namespace Dashboard.Tests.TestLib
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using H5;

    /// <summary>
    /// Mock fetch function factory for testing API calls.
    /// Intercepts HTTP requests and returns predefined responses.
    /// </summary>
    public static class MockFetch
    {
        /// <summary>
        /// Creates a mock fetch function that returns predefined responses.
        /// </summary>
        public static Func<string, object, Task<object>> Create(
            Dictionary<string, object> responses
        ) =>
            (url, options) =>
            {
                var path = ExtractPath(url);

                if (responses.TryGetValue(path, out var response))
                {
                    return CreateSuccessResponse(response);
                }

                // Return 404 for unknown paths
                return CreateErrorResponse(404, "Not Found");
            };

        /// <summary>
        /// Creates a mock fetch that tracks all calls made.
        /// </summary>
        public static MockFetchWithHistory CreateWithHistory(Dictionary<string, object> responses)
        {
            var history = new List<FetchCall>();

            Func<string, object, Task<object>> fetch = (url, options) =>
            {
                var path = ExtractPath(url);
                history.Add(
                    new FetchCall
                    {
                        Url = url,
                        Path = path,
                        Options = options,
                    }
                );

                if (responses.TryGetValue(path, out var response))
                {
                    return CreateSuccessResponse(response);
                }

                return CreateErrorResponse(404, "Not Found");
            };

            return new MockFetchWithHistory { Fetch = fetch, History = history };
        }

        /// <summary>
        /// Creates a mock fetch that delays responses.
        /// </summary>
        public static Func<string, object, Task<object>> CreateWithDelay(
            Dictionary<string, object> responses,
            int delayMs
        ) =>
            async (url, options) =>
            {
                await Task.Delay(delayMs);

                var path = ExtractPath(url);

                if (responses.TryGetValue(path, out var response))
                {
                    return await CreateSuccessResponse(response);
                }

                return await CreateErrorResponse(404, "Not Found");
            };

        /// <summary>
        /// Creates a mock fetch that fails for specific paths.
        /// </summary>
        public static Func<string, object, Task<object>> CreateWithErrors(
            Dictionary<string, object> responses,
            Dictionary<string, int> errors
        ) =>
            (url, options) =>
            {
                var path = ExtractPath(url);

                if (errors.TryGetValue(path, out var statusCode))
                {
                    return CreateErrorResponse(statusCode, "Error");
                }

                if (responses.TryGetValue(path, out var response))
                {
                    return CreateSuccessResponse(response);
                }

                return CreateErrorResponse(404, "Not Found");
            };

        /// <summary>
        /// Installs mock fetch globally on window.
        /// </summary>
        public static void Install(Func<string, object, Task<object>> mockFetch) =>
            Script.Set("window", "fetch", mockFetch);

        /// <summary>
        /// Restores the original fetch function.
        /// </summary>
        public static void Restore() =>
            Script.Call<object>("window.fetch = window.originalFetch || window.fetch");

        private static string ExtractPath(string url)
        {
            // Extract path from full URL
            // e.g., "http://localhost:5000/fhir/Patient" -> "/fhir/Patient"
            // Parse manually since H5 Uri doesn't have AbsolutePath
            var protocolEnd = url.IndexOf("://");
            if (protocolEnd < 0)
                return url;
            var hostStart = protocolEnd + 3;
            var pathStart = url.IndexOf("/", hostStart);
            if (pathStart < 0)
                return "/";
            return url.Substring(pathStart);
        }

        private static Task<object> CreateSuccessResponse(object data)
        {
            var response = Script.Call<object>(
                "Promise.resolve",
                new
                {
                    ok = true,
                    status = 200,
                    json = (Func<Task<object>>)(() => Task.FromResult(data)),
                    text = (Func<Task<string>>)(
                        () => Task.FromResult(Script.Call<string>("JSON.stringify", data))
                    ),
                }
            );
            return Script.Call<Task<object>>("Promise.resolve", response);
        }

        private static Task<object> CreateErrorResponse(int status, string message)
        {
            var response = new
            {
                ok = false,
                status = status,
                statusText = message,
                json = (Func<Task<object>>)(() => Task.FromResult<object>(new { error = message })),
                text = (Func<Task<string>>)(() => Task.FromResult(message)),
            };
            return Script.Call<Task<object>>("Promise.resolve", response);
        }
    }

    /// <summary>
    /// Mock fetch with call history tracking.
    /// </summary>
    public class MockFetchWithHistory
    {
        /// <summary>
        /// The mock fetch function.
        /// </summary>
        public Func<string, object, Task<object>> Fetch { get; set; }

        /// <summary>
        /// List of all fetch calls made.
        /// </summary>
        public List<FetchCall> History { get; set; }

        /// <summary>
        /// Clears the call history.
        /// </summary>
        public void ClearHistory() => History.Clear();

        /// <summary>
        /// Checks if a specific path was called.
        /// </summary>
        public bool WasCalled(string path) => History.Exists(c => c.Path == path);

        /// <summary>
        /// Gets the number of times a path was called.
        /// </summary>
        public int CallCount(string path) => History.FindAll(c => c.Path == path).Count;
    }

    /// <summary>
    /// Record of a fetch call.
    /// </summary>
    public class FetchCall
    {
        /// <summary>
        /// Full URL that was fetched.
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// Extracted path from URL.
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// Options passed to fetch.
        /// </summary>
        public object Options { get; set; }
    }
}
