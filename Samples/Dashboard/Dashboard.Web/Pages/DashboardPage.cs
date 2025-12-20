namespace Dashboard.Pages;

using Dashboard.Api;
using Dashboard.Components;
using Dashboard.Models;
using Dashboard.React;
using static Dashboard.React.Elements;
using static Dashboard.React.Hooks;

/// <summary>
/// Main dashboard overview page.
/// </summary>
public static class DashboardPage
{
    /// <summary>
    /// Dashboard state record.
    /// </summary>
    public record State(
        int PatientCount,
        int PractitionerCount,
        int AppointmentCount,
        int EncounterCount,
        bool Loading,
        string? Error
    );

    /// <summary>
    /// Renders the dashboard page.
    /// </summary>
    public static ReactElement Render()
    {
        var (state, setState) = UseState(new State(0, 0, 0, 0, true, null));

        UseEffect(
            () =>
            {
                LoadData(setState);
            },
            Array.Empty<object>()
        );

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
                state.Error != null
                    ? RenderError(state.Error)
                    : Text(""),
                // Metrics grid
                Div(
                    className: "dashboard-grid metrics mb-6",
                    children: new[]
                    {
                        MetricCard.Render(
                            new MetricCard.Props(
                                Label: "Total Patients",
                                Value: state.Loading ? "-" : state.PatientCount.ToString(),
                                Icon: Icons.Users,
                                IconColor: "blue",
                                TrendValue: "+12%",
                                Trend: MetricCard.TrendDirection.Up
                            )
                        ),
                        MetricCard.Render(
                            new MetricCard.Props(
                                Label: "Practitioners",
                                Value: state.Loading ? "-" : state.PractitionerCount.ToString(),
                                Icon: Icons.UserDoctor,
                                IconColor: "teal"
                            )
                        ),
                        MetricCard.Render(
                            new MetricCard.Props(
                                Label: "Appointments",
                                Value: state.Loading ? "-" : state.AppointmentCount.ToString(),
                                Icon: Icons.Calendar,
                                IconColor: "success",
                                TrendValue: "+8%",
                                Trend: MetricCard.TrendDirection.Up
                            )
                        ),
                        MetricCard.Render(
                            new MetricCard.Props(
                                Label: "Encounters",
                                Value: state.Loading ? "-" : state.EncounterCount.ToString(),
                                Icon: Icons.Clipboard,
                                IconColor: "warning",
                                TrendValue: "-3%",
                                Trend: MetricCard.TrendDirection.Down
                            )
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

    private static async void LoadData(Action<State> setState)
    {
        try
        {
            var patients = await ApiClient.GetPatientsAsync();
            var practitioners = await ApiClient.GetPractitionersAsync();
            var appointments = await ApiClient.GetAppointmentsAsync();

            setState(
                new State(
                    PatientCount: patients.Length,
                    PractitionerCount: practitioners.Length,
                    AppointmentCount: appointments.Length,
                    EncounterCount: 0, // TODO: Add encounter count endpoint
                    Loading: false,
                    Error: null
                )
            );
        }
        catch (Exception ex)
        {
            setState(new State(0, 0, 0, 0, false, ex.Message));
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
                                    children: new[] { Text($"Could not connect to API: {message}") }
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
                        H(3, className: "card-title", children: new[] { Text("Quick Actions") }),
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
                                RenderActionButton("New Appointment", Icons.Calendar, "secondary"),
                                RenderActionButton("View Schedule", Icons.Calendar, "secondary"),
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
    ) => Button(className: $"btn btn-{variant} w-full", children: new[] { icon(), Text(label) });

    private static ReactElement RenderRecentActivity() =>
        Div(
            className: "card",
            children: new[]
            {
                Div(
                    className: "card-header",
                    children: new[]
                    {
                        H(3, className: "card-title", children: new[] { Text("Recent Activity") }),
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

    private static ReactElement RenderActivityItem(string title, string subtitle, string time) =>
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
