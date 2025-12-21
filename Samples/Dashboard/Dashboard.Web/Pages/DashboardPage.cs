namespace Dashboard.Pages
{
    using System;
    using Dashboard.Api;
    using Dashboard.Components;
    using Dashboard.React;
    using static Dashboard.React.Elements;
    using static Dashboard.React.Hooks;

    /// <summary>
    /// Dashboard state class.
    /// </summary>
    public class DashboardState
    {
        /// <summary>Patient count.</summary>
        public int PatientCount { get; set; }

        /// <summary>Practitioner count.</summary>
        public int PractitionerCount { get; set; }

        /// <summary>Appointment count.</summary>
        public int AppointmentCount { get; set; }

        /// <summary>Encounter count.</summary>
        public int EncounterCount { get; set; }

        /// <summary>Whether loading.</summary>
        public bool Loading { get; set; }

        /// <summary>Error message if any.</summary>
        public string Error { get; set; }
    }

    /// <summary>
    /// Main dashboard overview page.
    /// </summary>
    public static class DashboardPage
    {
        /// <summary>
        /// Renders the dashboard page.
        /// </summary>
        public static ReactElement Render()
        {
            var stateResult = UseState(
                new DashboardState
                {
                    PatientCount = 0,
                    PractitionerCount = 0,
                    AppointmentCount = 0,
                    EncounterCount = 0,
                    Loading = true,
                    Error = null,
                }
            );

            var state = stateResult.State;
            var setState = stateResult.SetState;

            UseEffect(
                () =>
                {
                    LoadData(setState);
                },
                new object[0]
            );

            ReactElement errorElement;
            if (state.Error != null)
            {
                errorElement = RenderError(state.Error);
            }
            else
            {
                errorElement = Text("");
            }

            return Div(
                className: "page",
                children: new[]
                {
                    // Page header
                    Div(
                        className: "page-header",
                        children: new[]
                        {
                            H(2, className: "page-title", children: new[] { Text("Dashboard") }),
                            P(
                                className: "page-description",
                                children: new[] { Text("Overview of your healthcare system") }
                            ),
                        }
                    ),
                    // Error display
                    errorElement,
                    // Metrics grid
                    Div(
                        className: "dashboard-grid metrics mb-6",
                        children: new[]
                        {
                            MetricCard.Render(
                                new MetricCardProps
                                {
                                    Label = "Total Patients",
                                    Value = state.Loading ? "-" : state.PatientCount.ToString(),
                                    Icon = Icons.Users,
                                    IconColor = "blue",
                                    TrendValue = "+12%",
                                    Trend = TrendDirection.Up,
                                }
                            ),
                            MetricCard.Render(
                                new MetricCardProps
                                {
                                    Label = "Practitioners",
                                    Value = state.Loading
                                        ? "-"
                                        : state.PractitionerCount.ToString(),
                                    Icon = Icons.UserDoctor,
                                    IconColor = "teal",
                                }
                            ),
                            MetricCard.Render(
                                new MetricCardProps
                                {
                                    Label = "Appointments",
                                    Value = state.Loading ? "-" : state.AppointmentCount.ToString(),
                                    Icon = Icons.Calendar,
                                    IconColor = "success",
                                    TrendValue = "+8%",
                                    Trend = TrendDirection.Up,
                                }
                            ),
                            MetricCard.Render(
                                new MetricCardProps
                                {
                                    Label = "Encounters",
                                    Value = state.Loading ? "-" : state.EncounterCount.ToString(),
                                    Icon = Icons.Clipboard,
                                    IconColor = "warning",
                                    TrendValue = "-3%",
                                    Trend = TrendDirection.Down,
                                }
                            ),
                        }
                    ),
                    // Quick actions and activity
                    Div(
                        className: "dashboard-grid mixed",
                        children: new[] { RenderQuickActions(), RenderRecentActivity() }
                    ),
                }
            );
        }

        private static async void LoadData(Action<DashboardState> setState)
        {
            try
            {
                var patients = await ApiClient.GetPatientsAsync();
                var practitioners = await ApiClient.GetPractitionersAsync();
                var appointments = await ApiClient.GetAppointmentsAsync();

                setState(
                    new DashboardState
                    {
                        PatientCount = patients.Length,
                        PractitionerCount = practitioners.Length,
                        AppointmentCount = appointments.Length,
                        EncounterCount = 0,
                        Loading = false,
                        Error = null,
                    }
                );
            }
            catch (Exception ex)
            {
                setState(
                    new DashboardState
                    {
                        PatientCount = 0,
                        PractitionerCount = 0,
                        AppointmentCount = 0,
                        EncounterCount = 0,
                        Loading = false,
                        Error = ex.Message,
                    }
                );
            }
        }

        private static ReactElement RenderError(string message) =>
            Div(
                className: "card mb-6",
                style: new { borderLeft = "4px solid var(--warning)" },
                children: new[]
                {
                    Div(
                        className: "flex items-center gap-3",
                        children: new[]
                        {
                            Icons.Bell(),
                            Div(
                                children: new[]
                                {
                                    H(
                                        4,
                                        className: "font-semibold",
                                        children: new[] { Text("Connection Warning") }
                                    ),
                                    P(
                                        className: "text-sm text-gray-600",
                                        children: new[]
                                        {
                                            Text("Could not connect to API: " + message),
                                        }
                                    ),
                                    P(
                                        className: "text-sm text-gray-500",
                                        children: new[]
                                        {
                                            Text(
                                                "Make sure Clinical API (port 5000) and Scheduling API (port 5001) are running."
                                            ),
                                        }
                                    ),
                                }
                            ),
                        }
                    ),
                }
            );

        private static ReactElement RenderQuickActions() =>
            Div(
                className: "card",
                children: new[]
                {
                    Div(
                        className: "card-header",
                        children: new[]
                        {
                            H(
                                3,
                                className: "card-title",
                                children: new[] { Text("Quick Actions") }
                            ),
                        }
                    ),
                    Div(
                        className: "card-body",
                        children: new[]
                        {
                            Div(
                                className: "grid grid-cols-2 gap-4",
                                children: new[]
                                {
                                    RenderActionButton("New Patient", Icons.Plus, "primary"),
                                    RenderActionButton(
                                        "New Appointment",
                                        Icons.Calendar,
                                        "secondary"
                                    ),
                                    RenderActionButton(
                                        "View Schedule",
                                        Icons.Calendar,
                                        "secondary"
                                    ),
                                    RenderActionButton("Patient Search", Icons.Search, "secondary"),
                                }
                            ),
                        }
                    ),
                }
            );

        private static ReactElement RenderActionButton(
            string label,
            Func<ReactElement> icon,
            string variant
        ) =>
            Button(
                className: "btn btn-" + variant + " w-full",
                children: new[] { icon(), Text(label) }
            );

        private static ReactElement RenderRecentActivity() =>
            Div(
                className: "card",
                children: new[]
                {
                    Div(
                        className: "card-header",
                        children: new[]
                        {
                            H(
                                3,
                                className: "card-title",
                                children: new[] { Text("Recent Activity") }
                            ),
                            Button(
                                className: "btn btn-ghost btn-sm",
                                children: new[] { Text("View All") }
                            ),
                        }
                    ),
                    Div(
                        className: "card-body",
                        children: new[]
                        {
                            Div(
                                className: "data-list",
                                children: new[]
                                {
                                    RenderActivityItem(
                                        "New patient registered",
                                        "John Smith added to system",
                                        "2 min ago"
                                    ),
                                    RenderActivityItem(
                                        "Appointment completed",
                                        "Dr. Wilson with Jane Doe",
                                        "15 min ago"
                                    ),
                                    RenderActivityItem(
                                        "Lab results available",
                                        "Patient ID: PAT-0042",
                                        "1 hour ago"
                                    ),
                                }
                            ),
                        }
                    ),
                }
            );

        private static ReactElement RenderActivityItem(
            string title,
            string subtitle,
            string time
        ) =>
            Div(
                className: "data-list-item",
                children: new[]
                {
                    Div(className: "avatar avatar-sm", children: new[] { Icons.Activity() }),
                    Div(
                        className: "data-list-item-content",
                        children: new[]
                        {
                            Div(className: "data-list-item-title", children: new[] { Text(title) }),
                            Div(
                                className: "data-list-item-subtitle",
                                children: new[] { Text(subtitle) }
                            ),
                        }
                    ),
                    Div(className: "data-list-item-meta", children: new[] { Text(time) }),
                }
            );
    }
}
