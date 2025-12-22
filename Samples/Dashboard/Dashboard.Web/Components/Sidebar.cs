using System;
using System.Linq;
using Dashboard.React;
using static Dashboard.React.Elements;

namespace Dashboard.Components
{
    /// <summary>
    /// Navigation item class.
    /// </summary>
    public class NavItem
    {
        /// <summary>Item identifier.</summary>
        public string Id { get; set; }

        /// <summary>Display label.</summary>
        public string Label { get; set; }

        /// <summary>Icon factory.</summary>
        public Func<ReactElement> Icon { get; set; }

        /// <summary>Optional badge count.</summary>
        public int? Badge { get; set; }
    }

    /// <summary>
    /// Navigation section class.
    /// </summary>
    public class NavSection
    {
        /// <summary>Section title.</summary>
        public string Title { get; set; }

        /// <summary>Navigation items.</summary>
        public NavItem[] Items { get; set; }
    }

    /// <summary>
    /// Sidebar navigation component.
    /// </summary>
    public static class Sidebar
    {
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
                className: "sidebar " + (collapsed ? "collapsed" : ""),
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
                new NavSection
                {
                    Title = "Overview",
                    Items = new[]
                    {
                        new NavItem
                        {
                            Id = "dashboard",
                            Label = "Dashboard",
                            Icon = Icons.Home,
                        },
                    },
                },
                new NavSection
                {
                    Title = "Clinical",
                    Items = new[]
                    {
                        new NavItem
                        {
                            Id = "patients",
                            Label = "Patients",
                            Icon = Icons.Users,
                        },
                        new NavItem
                        {
                            Id = "encounters",
                            Label = "Encounters",
                            Icon = Icons.Clipboard,
                        },
                        new NavItem
                        {
                            Id = "conditions",
                            Label = "Conditions",
                            Icon = Icons.Heart,
                        },
                        new NavItem
                        {
                            Id = "medications",
                            Label = "Medications",
                            Icon = Icons.Pill,
                        },
                    },
                },
                new NavSection
                {
                    Title = "Scheduling",
                    Items = new[]
                    {
                        new NavItem
                        {
                            Id = "practitioners",
                            Label = "Practitioners",
                            Icon = Icons.UserDoctor,
                        },
                        new NavItem
                        {
                            Id = "appointments",
                            Label = "Appointments",
                            Icon = Icons.Clipboard,
                            Badge = 3,
                        },
                        new NavItem
                        {
                            Id = "calendar",
                            Label = "Schedule",
                            Icon = Icons.Calendar,
                        },
                    },
                },
                new NavSection
                {
                    Title = "System",
                    Items = new[]
                    {
                        new NavItem
                        {
                            Id = "settings",
                            Label = "Settings",
                            Icon = Icons.Settings,
                        },
                    },
                },
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
                            Div(
                                className: "sidebar-logo-icon",
                                children: new[] { Icons.Activity() }
                            ),
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
        )
        {
            var items = section
                .Items.Select(item => RenderNavItem(item, activeView, onNavigate))
                .ToArray();
            var allChildren = new ReactElement[items.Length + 1];
            allChildren[0] = Div(
                className: "nav-section-title",
                children: new[] { Text(section.Title) }
            );
            for (int i = 0; i < items.Length; i++)
            {
                allChildren[i + 1] = items[i];
            }
            return Div(className: "nav-section", children: allChildren);
        }

        private static ReactElement RenderNavItem(
            NavItem item,
            string activeView,
            Action<string> onNavigate
        )
        {
            var isActive = activeView == item.Id;
            ReactElement[] children;

            if (item.Badge.HasValue)
            {
                children = new ReactElement[]
                {
                    Span(className: "nav-item-icon", children: new[] { item.Icon() }),
                    Span(className: "nav-item-text", children: new[] { Text(item.Label) }),
                    Span(
                        className: "nav-item-badge",
                        children: new[] { Text(item.Badge.Value.ToString()) }
                    ),
                };
            }
            else
            {
                children = new ReactElement[]
                {
                    Span(className: "nav-item-icon", children: new[] { item.Icon() }),
                    Span(className: "nav-item-text", children: new[] { Text(item.Label) }),
                };
            }

            return A(
                href: "#",
                className: "nav-item " + (isActive ? "active" : ""),
                onClick: () => onNavigate(item.Id),
                children: children
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
}
