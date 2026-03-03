using H5;
using Reporting.React.Api;
using Reporting.React.Components;
using Reporting.React.Core;
using static Reporting.React.Core.Elements;
using static Reporting.React.Core.Hooks;

namespace Reporting.React
{
    /// <summary>
    /// Report viewer application state.
    /// </summary>
    public class AppState
    {
        /// <summary>Current report definition (from API).</summary>
        public object ReportDef { get; set; }

        /// <summary>Current execution result (from API).</summary>
        public object ExecutionResult { get; set; }

        /// <summary>Whether a report is currently loading.</summary>
        public bool Loading { get; set; }

        /// <summary>Error message if loading failed.</summary>
        public string Error { get; set; }

        /// <summary>List of available reports.</summary>
        public object[] ReportList { get; set; }
    }

    /// <summary>
    /// Entry point for the report viewer React application.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Main entry point - called when H5 script loads.
        /// </summary>
        public static void Main()
        {
            // Read config from window object
            var apiBaseUrl = GetConfigValue("apiBaseUrl", "");
            var reportId = GetConfigValue("reportId", "");

            ReportApiClient.Configure(apiBaseUrl);

            Log("Report Viewer starting...");
            Log("API Base URL: " + apiBaseUrl);

            HideLoadingScreen();

            ReactInterop.RenderApp(RenderApp(apiBaseUrl, reportId));

            Log("Report Viewer initialized.");
        }

        private static ReactElement RenderApp(string apiBaseUrl, string initialReportId)
        {
            var stateResult = UseState(
                new AppState
                {
                    ReportDef = null,
                    ExecutionResult = null,
                    Loading = false,
                    Error = null,
                    ReportList = null,
                }
            );

            var state = stateResult.State;
            var setState = stateResult.SetState;

            // Load report list on mount
            UseEffect(
                () =>
                {
                    if (string.IsNullOrEmpty(apiBaseUrl)) return;

                    LoadReportList(state, setState);

                    // If a specific report was requested, load it
                    if (!string.IsNullOrEmpty(initialReportId))
                    {
                        LoadAndExecuteReport(initialReportId, state, setState);
                    }
                },
                new object[] { }
            );

            // Render based on state
            if (!string.IsNullOrEmpty(state.Error))
            {
                return Div(
                    className: "report-viewer-error",
                    children: new[]
                    {
                        H(2, children: new[] { Text("Error") }),
                        P(children: new[] { Text(state.Error) }),
                    }
                );
            }

            if (state.Loading)
            {
                return Div(
                    className: "report-viewer-loading",
                    children: new[] { Text("Loading report...") }
                );
            }

            if (state.ReportDef != null && state.ExecutionResult != null)
            {
                return ReportRenderer.Render(state.ReportDef, state.ExecutionResult);
            }

            // Show report list
            return RenderReportList(state, setState);
        }

        private static ReactElement RenderReportList(AppState state, System.Action<AppState> setState)
        {
            var reports = state.ReportList ?? new object[0];

            if (reports.Length == 0)
            {
                return Div(
                    className: "report-viewer-empty",
                    children: new[]
                    {
                        H(2, children: new[] { Text("Report Viewer") }),
                        P(children: new[] { Text("No reports available. Configure the API base URL and add report definitions.") }),
                    }
                );
            }

            var reportItems = new ReactElement[reports.Length];
            for (var i = 0; i < reports.Length; i++)
            {
                var report = reports[i];
                var id = Script.Get<string>(report, "id");
                var title = Script.Get<string>(report, "title") ?? id;
                var capturedId = id;

                reportItems[i] = Div(
                    className: "report-list-item",
                    onClick: () => LoadAndExecuteReport(capturedId, state, setState),
                    children: new[]
                    {
                        H(3, children: new[] { Text(title) }),
                    }
                );
            }

            return Div(
                className: "report-viewer-list",
                children: new[]
                {
                    H(2, children: new[] { Text("Available Reports") }),
                    Div(className: "report-list", children: reportItems),
                }
            );
        }

        private static async void LoadReportList(AppState currentState, System.Action<AppState> setState)
        {
            try
            {
                var reports = await ReportApiClient.GetReportsAsync();
                setState(
                    new AppState
                    {
                        ReportDef = currentState.ReportDef,
                        ExecutionResult = currentState.ExecutionResult,
                        Loading = false,
                        Error = null,
                        ReportList = reports,
                    }
                );
            }
            catch (System.Exception ex)
            {
                Log("Failed to load report list: " + ex.Message);
            }
        }

        private static async void LoadAndExecuteReport(
            string reportId,
            AppState currentState,
            System.Action<AppState> setState
        )
        {
            setState(
                new AppState
                {
                    ReportDef = null,
                    ExecutionResult = null,
                    Loading = true,
                    Error = null,
                    ReportList = currentState.ReportList,
                }
            );

            try
            {
                var reportDef = await ReportApiClient.GetReportAsync(reportId);
                var parameters = Script.Call<object>("Object.create", (object)null);
                var result = await ReportApiClient.ExecuteReportAsync(reportId, parameters);

                setState(
                    new AppState
                    {
                        ReportDef = reportDef,
                        ExecutionResult = result,
                        Loading = false,
                        Error = null,
                        ReportList = currentState.ReportList,
                    }
                );
            }
            catch (System.Exception ex)
            {
                setState(
                    new AppState
                    {
                        ReportDef = null,
                        ExecutionResult = null,
                        Loading = false,
                        Error = "Failed to load report: " + ex.Message,
                        ReportList = currentState.ReportList,
                    }
                );
            }
        }

        private static string GetConfigValue(string key, string defaultValue)
        {
            var windowConfig = Script.Get<object>("window", "reportConfig");
            if (windowConfig != null)
            {
                var value = Script.Get<string>(windowConfig, key);
                if (!string.IsNullOrEmpty(value)) return value;
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
            Script.Call<object>("console.log", "[ReportViewer] " + message);
    }
}
