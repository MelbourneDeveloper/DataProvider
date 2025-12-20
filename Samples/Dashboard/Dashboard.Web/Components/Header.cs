namespace Dashboard.Components;

using Dashboard.React;
using static Dashboard.React.Elements;

/// <summary>
/// Header component with search and actions.
/// </summary>
public static class Header
{
    /// <summary>
    /// Renders the header component.
    /// </summary>
    public static ReactElement Render(
        string title,
        string? searchQuery = null,
        Action<string>? onSearchChange = null,
        int notificationCount = 0
    )
    {
        return React.Elements.Header(
            className: "header",
            children: new[]
            {
                // Left section
                Div(
                    className: "header-left",
                    children: new[]
                    {
                        H(1, className: "header-title", children: new[] { Text(title) }),
                    }
                ),
                // Right section
                Div(
                    className: "header-right",
                    children: new[]
                    {
                        // Search
                        Div(
                            className: "header-search",
                            children: new[]
                            {
                                Span(
                                    className: "header-search-icon",
                                    children: new[] { Icons.Search() }
                                ),
                                Input(
                                    className: "input",
                                    type: "text",
                                    placeholder: "Search patients, appointments...",
                                    value: searchQuery,
                                    onChange: onSearchChange
                                ),
                            }
                        ),
                        // Actions
                        Div(
                            className: "header-actions",
                            children: new[]
                            {
                                RenderNotificationButton(notificationCount),
                                RenderUserAvatar(),
                            }
                        ),
                    }
                ),
            }
        );
    }

    private static ReactElement RenderNotificationButton(int count) =>
        Button(
            className: "header-action-btn",
            onClick: () => { /* TODO: Open notifications */
            },
            children: count > 0
                ? new[] { Icons.Bell(), Span(className: "header-action-badge") }
                : new[] { Icons.Bell() }
        );

    private static ReactElement RenderUserAvatar() =>
        Div(className: "avatar avatar-md", children: new[] { Text("JD") });
}
