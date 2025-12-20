namespace Dashboard.Tests
{
    using Dashboard.Tests.TestLib;
    using Dashboard.Tests.Tests;
    using H5;

    /// <summary>
    /// Test entry point - runs all dashboard tests in the browser.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Main entry point.
        /// </summary>
        public static async void Main()
        {
            Log("🧪 Dashboard Test Suite Starting...");
            Log("========================================");

            // Store original fetch
            Script.Set("window", "originalFetch", Script.Get<object>("window", "fetch"));

            // Wait for React and Testing Library to be available
            await WaitForDependencies();

            // Register all tests
            Log("📝 Registering tests...");
            DashboardTests.RegisterAll();

            // Run all tests
            Log("🏃 Running tests...");
            await TestRunner.RunAll();

            Log("========================================");
            Log("✅ Test run complete!");
        }

        private static async System.Threading.Tasks.Task WaitForDependencies()
        {
            var attempts = 0;
            while (attempts < 50)
            {
                var hasReact = Script.Get<object>("window", "React") != null;
                var hasReactDOM = Script.Get<object>("window", "ReactDOM") != null;
                var hasTestingLib = Script.Get<object>("window", "TestingLibrary") != null;

                if (hasReact && hasReactDOM && hasTestingLib)
                {
                    Log("✓ Dependencies loaded: React, ReactDOM, Testing Library");
                    return;
                }

                await System.Threading.Tasks.Task.Delay(100);
                attempts++;
            }

            Log("⚠️ Warning: Some dependencies may not be loaded");
        }

        private static void Log(string message)
        {
            Script.Call<object>("console.log", message);
        }
    }
}
