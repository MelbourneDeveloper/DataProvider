namespace Dashboard.Components;

using Dashboard.React;
using static Dashboard.React.Elements;

/// <summary>
/// Sidebar navigation component.
/// </summary>
public static class Sidebar
{
    /// <summary>
    /// Navigation item record.
    /// </summary>
    public record NavItem(string Id, string Label, Func<ReactElement> Icon, int? Badge = null);

    /// <summary>
    /// Navigation section record.
    /// </summary>
    public record NavSection(string Title, NavItem[] Items);

    /// <summary>
    /// Renders the sidebar component.
    /// </summary>
    public static ReactElement Render(
        string activeView,
        Action<string> onNavigate,
        bool collapsed,
        Action onToggle
    )
    {
        var sections = GetNavSections();

        return Aside(
            className: $"sidebar {(collapsed ? "collapsed" : "")}",
            children: new[]
            {
                // Header with logo
                RenderHeader(collapsed),
                // Navigation sections
                Nav(
                    className: "sidebar-nav",
                    children: sections
                        .Select(section => RenderSection(section, activeView, onNavigate))
                        .ToArray()
                ),
                // Toggle button
                Button(
                    className: "sidebar-toggle",
                    onClick: onToggle,
                    children: new[] { collapsed ? Icons.ChevronRight() : Icons.ChevronLeft() }
                ),
                // Footer with user
                RenderFooter(collapsed),
            }
        );
    }

    private static NavSection[] GetNavSections() =>
        new[]
        {
            new NavSection("Overview", new[] { new NavItem("dashboard", "Dashboard", Icons.Home) }),
            new NavSection(
                "Clinical",
                new[]
                {
                    new NavItem("patients", "Patients", Icons.Users),
                    new NavItem("encounters", "Encounters", Icons.Clipboard),
                    new NavItem("conditions", "Conditions", Icons.Heart),
                    new NavItem("medications", "Medications", Icons.Pill),
                }
            ),
            new NavSection(
                "Scheduling",
                new[]
                {
                    new NavItem("practitioners", "Practitioners", Icons.UserDoctor),
                    new NavItem("appointments", "Appointments", Icons.Calendar, 3),
                }
            ),
            new NavSection("System", new[] { new NavItem("settings", "Settings", Icons.Settings) }),
        };

    private static ReactElement RenderHeader(bool collapsed) =>
        Div(
            className: "sidebar-header",
            children: new[]
            {
                A(
                    href: "#",
                    className: "sidebar-logo",
                    children: new[]
                    {
                        Div(className: "sidebar-logo-icon", children: new[] { Icons.Activity() }),
                        Span(
                            className: "sidebar-logo-text",
                            children: new[] { Text("HealthCare") }
                        ),
                    }
                ),
            }
        );

    private static ReactElement RenderSection(
        NavSection section,
        string activeView,
        Action<string> onNavigate
    ) =>
        Div(
            className: "nav-section",
            children: new[]
            {
                Div(className: "nav-section-title", children: new[] { Text(section.Title) }),
            }
                .Concat(section.Items.Select(item => RenderNavItem(item, activeView, onNavigate)))
                .ToArray()
        );

    private static ReactElement RenderNavItem(
        NavItem item,
        string activeView,
        Action<string> onNavigate
    )
    {
        var isActive = activeView == item.Id;

        return A(
            href: "#",
            className: $"nav-item {(isActive ? "active" : "")}",
            onClick: () => onNavigate(item.Id),
            children: new ReactElement[]
            {
                Span(className: "nav-item-icon", children: new[] { item.Icon() }),
                Span(className: "nav-item-text", children: new[] { Text(item.Label) }),
            }
                .Concat(
                    item.Badge.HasValue
                        ? new[]
                        {
                            Span(
                                className: "nav-item-badge",
                                children: new[] { Text(item.Badge.Value.ToString()) }
                            ),
                        }
                        : Array.Empty<ReactElement>()
                )
                .ToArray()
        );
    }

    private static ReactElement RenderFooter(bool collapsed) =>
        Div(
            className: "sidebar-footer",
            children: new[]
            {
                Div(
                    className: "sidebar-user",
                    children: new[]
                    {
                        Div(className: "avatar avatar-md", children: new[] { Text("JD") }),
                        Div(
                            className: "sidebar-user-info",
                            children: new[]
                            {
                                Div(
                                    className: "sidebar-user-name",
                                    children: new[] { Text("John Doe") }
                                ),
                                Div(
                                    className: "sidebar-user-role",
                                    children: new[] { Text("Administrator") }
                                ),
                            }
                        ),
                    }
                ),
            }
        );
}
