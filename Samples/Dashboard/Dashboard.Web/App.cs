namespace Dashboard;

using Dashboard.Components;
using Dashboard.Pages;
using Dashboard.React;
using static Dashboard.React.Elements;
using static Dashboard.React.Hooks;

/// <summary>
/// Main application component.
/// </summary>
public static class App
{
    /// <summary>
    /// Application state record.
    /// </summary>
    public record AppState(
        string ActiveView,
        bool SidebarCollapsed,
        string SearchQuery,
        int NotificationCount
    );

    /// <summary>
    /// Renders the main application.
    /// </summary>
    public static ReactElement Render()
    {
        var (state, setState) = UseState(
            new AppState(
                ActiveView: "dashboard",
                SidebarCollapsed: false,
                SearchQuery: "",
                NotificationCount: 3
            )
        );

        return Div(
            className: "app",
            children: new[]
            {
                // Sidebar
                Sidebar.Render(
                    activeView: state.ActiveView,
                    onNavigate: view => setState(s => s with { ActiveView = view }),
                    collapsed: state.SidebarCollapsed,
                    onToggle: () => setState(s => s with { SidebarCollapsed = !s.SidebarCollapsed })
                ),
                // Main content wrapper
                Div(
                    className: "main-wrapper",
                    children: new[]
                    {
                        // Header
                        Header.Render(
                            title: GetPageTitle(state.ActiveView),
                            searchQuery: state.SearchQuery,
                            onSearchChange: query => setState(s => s with { SearchQuery = query }),
                            notificationCount: state.NotificationCount
                        ),
                        // Main content area
                        Main(
                            className: "main-content",
                            children: new[] { RenderPage(state.ActiveView) }
                        ),
                    }
                ),
            }
        );
    }

    private static string GetPageTitle(string view) =>
        view switch
        {
            "dashboard" => "Dashboard",
            "patients" => "Patients",
            "encounters" => "Encounters",
            "conditions" => "Conditions",
            "medications" => "Medications",
            "practitioners" => "Practitioners",
            "appointments" => "Appointments",
            "settings" => "Settings",
            _ => "Healthcare",
        };

    private static ReactElement RenderPage(string view) =>
        view switch
        {
            "dashboard" => DashboardPage.Render(),
            "patients" => PatientsPage.Render(),
            "practitioners" => PractitionersPage.Render(),
            "appointments" => AppointmentsPage.Render(),
            "encounters" => RenderPlaceholderPage(
                "Encounters",
                "Manage patient encounters and visits"
            ),
            "conditions" => RenderPlaceholderPage(
                "Conditions",
                "View and manage patient conditions"
            ),
            "medications" => RenderPlaceholderPage("Medications", "Manage medication requests"),
            "settings" => RenderPlaceholderPage("Settings", "Configure application settings"),
            _ => RenderPlaceholderPage("Page Not Found", "The requested page does not exist"),
        };

    private static ReactElement RenderPlaceholderPage(string title, string description) =>
        Div(
            className: "page",
            children: new[]
            {
                Div(
                    className: "page-header",
                    children: new[]
                    {
                        H(2, className: "page-title", children: new[] { Text(title) }),
                        P(className: "page-description", children: new[] { Text(description) }),
                    }
                ),
                Div(
                    className: "card",
                    children: new[]
                    {
                        Div(
                            className: "empty-state",
                            children: new[]
                            {
                                Icons.Clipboard(),
                                H(
                                    4,
                                    className: "empty-state-title",
                                    children: new[] { Text("Coming Soon") }
                                ),
                                P(
                                    className: "empty-state-description",
                                    children: new[] { Text("This page is under development.") }
                                ),
                            }
                        ),
                    }
                ),
            }
        );
}
