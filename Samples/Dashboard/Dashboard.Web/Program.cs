namespace Dashboard
{
    using Dashboard.Api;
    using Dashboard.React;
    using H5;

    /// <summary>
    /// Application entry point.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Main entry point - called when H5 script loads.
        /// </summary>
        public static void Main()
        {
            // Configure API endpoints
            // Default to local development URLs
            var clinicalUrl = GetConfigValue("CLINICAL_API_URL", "http://localhost:5080");
            var schedulingUrl = GetConfigValue("SCHEDULING_API_URL", "http://localhost:5001");

            ApiClient.Configure(clinicalUrl, schedulingUrl);

            // Log startup
            Log("Healthcare Dashboard starting...");
            Log("Clinical API: " + clinicalUrl);
            Log("Scheduling API: " + schedulingUrl);

            // Hide loading screen
            HideLoadingScreen();

            // Render the React application
            ReactInterop.RenderApp(App.Render());

            Log("Dashboard initialized successfully!");
        }

        private static string GetConfigValue(string key, string defaultValue)
        {
            // Try to get from window config object if available
            var windowConfig = Script.Get<object>("window", "dashboardConfig");
            if (windowConfig != null)
            {
                var value = Script.Get<string>(windowConfig, key);
                if (!string.IsNullOrEmpty(value))
                {
                    return value;
                }
            }

            return defaultValue;
        }

        private static void HideLoadingScreen()
        {
            var loadingScreen = Script.Call<object>("document.getElementById", "loading-screen");
            if (loadingScreen != null)
            {
                Script.Write("loadingScreen.classList.add('hidden')");
            }
        }

        private static void Log(string message) =>
            Script.Call<object>("console.log", "[Dashboard] " + message);
    }
}
