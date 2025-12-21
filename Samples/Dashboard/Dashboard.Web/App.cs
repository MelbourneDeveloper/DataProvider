namespace Dashboard
{
    using Dashboard.Components;
    using Dashboard.Pages;
    using Dashboard.React;
    using static Dashboard.React.Elements;
    using static Dashboard.React.Hooks;

    /// <summary>
    /// Application state class.
    /// </summary>
    public class AppState
    {
        /// <summary>Active view identifier.</summary>
        public string ActiveView { get; set; }

        /// <summary>Whether sidebar is collapsed.</summary>
        public bool SidebarCollapsed { get; set; }

        /// <summary>Search query string.</summary>
        public string SearchQuery { get; set; }

        /// <summary>Notification count.</summary>
        public int NotificationCount { get; set; }

        /// <summary>Patient ID being edited (null if not editing).</summary>
        public string EditingPatientId { get; set; }

        /// <summary>Appointment ID being edited (null if not editing).</summary>
        public string EditingAppointmentId { get; set; }
    }

    /// <summary>
    /// Main application component.
    /// </summary>
    public static class App
    {
        /// <summary>
        /// Renders the main application.
        /// </summary>
        public static ReactElement Render()
        {
            var stateResult = UseState(
                new AppState
                {
                    ActiveView = "dashboard",
                    SidebarCollapsed = false,
                    SearchQuery = "",
                    NotificationCount = 3,
                    EditingPatientId = null,
                    EditingAppointmentId = null,
                }
            );

            var state = stateResult.State;
            var setState = stateResult.SetState;

            return Div(
                className: "app",
                children: new[]
                {
                    // Sidebar
                    Sidebar.Render(
                        activeView: state.ActiveView,
                        onNavigate: view =>
                        {
                            var newState = new AppState
                            {
                                ActiveView = view,
                                SidebarCollapsed = state.SidebarCollapsed,
                                SearchQuery = state.SearchQuery,
                                NotificationCount = state.NotificationCount,
                                EditingPatientId = null,
                                EditingAppointmentId = null,
                            };
                            setState(newState);
                        },
                        collapsed: state.SidebarCollapsed,
                        onToggle: () =>
                        {
                            var newState = new AppState
                            {
                                ActiveView = state.ActiveView,
                                SidebarCollapsed = !state.SidebarCollapsed,
                                SearchQuery = state.SearchQuery,
                                NotificationCount = state.NotificationCount,
                                EditingPatientId = state.EditingPatientId,
                                EditingAppointmentId = state.EditingAppointmentId,
                            };
                            setState(newState);
                        }
                    ),
                    // Main content wrapper
                    Div(
                        className: "main-wrapper",
                        children: new[]
                        {
                            // Header
                            Components.Header.Render(
                                title: GetPageTitle(state.ActiveView),
                                searchQuery: state.SearchQuery,
                                onSearchChange: query =>
                                {
                                    var newState = new AppState
                                    {
                                        ActiveView = state.ActiveView,
                                        SidebarCollapsed = state.SidebarCollapsed,
                                        SearchQuery = query,
                                        NotificationCount = state.NotificationCount,
                                        EditingPatientId = state.EditingPatientId,
                                        EditingAppointmentId = state.EditingAppointmentId,
                                    };
                                    setState(newState);
                                },
                                notificationCount: state.NotificationCount
                            ),
                            // Main content area
                            Main(
                                className: "main-content",
                                children: new[] { RenderPage(state, setState) }
                            ),
                        }
                    ),
                }
            );
        }

        private static string GetPageTitle(string view)
        {
            if (view == "dashboard")
                return "Dashboard";
            if (view == "patients")
                return "Patients";
            if (view == "encounters")
                return "Encounters";
            if (view == "conditions")
                return "Conditions";
            if (view == "medications")
                return "Medications";
            if (view == "practitioners")
                return "Practitioners";
            if (view == "appointments")
                return "Appointments";
            if (view == "calendar")
                return "Schedule";
            if (view == "settings")
                return "Settings";
            return "Healthcare";
        }

        private static ReactElement RenderPage(AppState state, System.Action<AppState> setState)
        {
            var view = state.ActiveView;

            // Handle editing patient
            if (view == "patients" && state.EditingPatientId != null)
            {
                return EditPatientPage.Render(
                    state.EditingPatientId,
                    () =>
                    {
                        var newState = new AppState
                        {
                            ActiveView = "patients",
                            SidebarCollapsed = state.SidebarCollapsed,
                            SearchQuery = state.SearchQuery,
                            NotificationCount = state.NotificationCount,
                            EditingPatientId = null,
                            EditingAppointmentId = null,
                        };
                        setState(newState);
                    }
                );
            }

            // Handle editing appointment
            if ((view == "appointments" || view == "calendar") && state.EditingAppointmentId != null)
            {
                return EditAppointmentPage.Render(
                    state.EditingAppointmentId,
                    () =>
                    {
                        var newState = new AppState
                        {
                            ActiveView = view,
                            SidebarCollapsed = state.SidebarCollapsed,
                            SearchQuery = state.SearchQuery,
                            NotificationCount = state.NotificationCount,
                            EditingPatientId = null,
                            EditingAppointmentId = null,
                        };
                        setState(newState);
                    }
                );
            }

            if (view == "dashboard")
                return DashboardPage.Render();
            if (view == "patients")
            {
                return PatientsPage.Render(patientId =>
                {
                    var newState = new AppState
                    {
                        ActiveView = "patients",
                        SidebarCollapsed = state.SidebarCollapsed,
                        SearchQuery = state.SearchQuery,
                        NotificationCount = state.NotificationCount,
                        EditingPatientId = patientId,
                        EditingAppointmentId = null,
                    };
                    setState(newState);
                });
            }
            if (view == "practitioners")
                return PractitionersPage.Render();
            if (view == "appointments")
            {
                return AppointmentsPage.Render(appointmentId =>
                {
                    var newState = new AppState
                    {
                        ActiveView = "appointments",
                        SidebarCollapsed = state.SidebarCollapsed,
                        SearchQuery = state.SearchQuery,
                        NotificationCount = state.NotificationCount,
                        EditingPatientId = null,
                        EditingAppointmentId = appointmentId,
                    };
                    setState(newState);
                });
            }
            if (view == "calendar")
            {
                return CalendarPage.Render(appointmentId =>
                {
                    var newState = new AppState
                    {
                        ActiveView = "calendar",
                        SidebarCollapsed = state.SidebarCollapsed,
                        SearchQuery = state.SearchQuery,
                        NotificationCount = state.NotificationCount,
                        EditingPatientId = null,
                        EditingAppointmentId = appointmentId,
                    };
                    setState(newState);
                });
            }
            if (view == "encounters")
                return RenderPlaceholderPage("Encounters", "Manage patient encounters and visits");
            if (view == "conditions")
                return RenderPlaceholderPage("Conditions", "View and manage patient conditions");
            if (view == "medications")
                return RenderPlaceholderPage("Medications", "Manage medication requests");
            if (view == "settings")
                return RenderPlaceholderPage("Settings", "Configure application settings");
            return RenderPlaceholderPage("Page Not Found", "The requested page does not exist");
        }

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
}
