namespace Dashboard.Pages;

using Dashboard.Api;
using Dashboard.Components;
using Dashboard.Models;
using Dashboard.React;
using static Dashboard.React.Elements;
using static Dashboard.React.Hooks;

/// <summary>
/// Practitioners list page.
/// </summary>
public static class PractitionersPage
{
    /// <summary>
    /// Page state record.
    /// </summary>
    public record State(
        Practitioner[] Practitioners,
        bool Loading,
        string? Error,
        string? SpecialtyFilter
    );

    /// <summary>
    /// Renders the practitioners page.
    /// </summary>
    public static ReactElement Render()
    {
        var (state, setState) = UseState(
            new State(
                Practitioners: Array.Empty<Practitioner>(),
                Loading: true,
                Error: null,
                SpecialtyFilter: null
            )
        );

        UseEffect(
            () =>
            {
                LoadPractitioners(setState);
            },
            Array.Empty<object>()
        );

        return Div(
            className: "page",
            children: new[]
            {
                // Page header
                Div(
                    className: "page-header flex justify-between items-center",
                    children: new[]
                    {
                        Div(
                            children: new[]
                            {
                                H(
                                    2,
                                    className: "page-title",
                                    children: new[] { Text("Practitioners") }
                                ),
                                P(
                                    className: "page-description",
                                    children: new[]
                                    {
                                        Text(
                                            "Manage healthcare providers from the Scheduling domain"
                                        ),
                                    }
                                ),
                            }
                        ),
                        Button(
                            className: "btn btn-primary",
                            children: new[] { Icons.Plus(), Text("Add Practitioner") }
                        ),
                    }
                ),
                // Filters
                Div(
                    className: "card mb-6",
                    children: new[]
                    {
                        Div(
                            className: "flex gap-4",
                            children: new[]
                            {
                                Div(
                                    className: "input-group",
                                    children: new[]
                                    {
                                        Label(
                                            className: "input-label",
                                            children: new[] { Text("Filter by Specialty") }
                                        ),
                                        Select(
                                            className: "input",
                                            value: state.SpecialtyFilter ?? "",
                                            onChange: specialty =>
                                                FilterBySpecialty(specialty, setState),
                                            children: new[]
                                            {
                                                Option("", "All Specialties"),
                                                Option("Cardiology", "Cardiology"),
                                                Option("Dermatology", "Dermatology"),
                                                Option("Family Medicine", "Family Medicine"),
                                                Option("Internal Medicine", "Internal Medicine"),
                                                Option("Neurology", "Neurology"),
                                                Option("Oncology", "Oncology"),
                                                Option("Pediatrics", "Pediatrics"),
                                                Option("Psychiatry", "Psychiatry"),
                                                Option("Surgery", "Surgery"),
                                            }
                                        ),
                                    }
                                ),
                                Div(className: "flex-1"),
                                Button(
                                    className: "btn btn-secondary",
                                    onClick: () => LoadPractitioners(setState),
                                    children: new[] { Icons.Refresh(), Text("Refresh") }
                                ),
                            }
                        ),
                    }
                ),
                // Content
                state.Loading
                    ? RenderLoadingGrid()
                : state.Error != null ? RenderError(state.Error)
                : state.Practitioners.Length == 0 ? RenderEmpty()
                : RenderPractitionerGrid(state.Practitioners),
            }
        );
    }

    private static async void LoadPractitioners(Action<State> setState)
    {
        try
        {
            var practitioners = await ApiClient.GetPractitionersAsync();
            setState(
                new State(
                    Practitioners: practitioners,
                    Loading: false,
                    Error: null,
                    SpecialtyFilter: null
                )
            );
        }
        catch (Exception ex)
        {
            setState(
                new State(
                    Practitioners: Array.Empty<Practitioner>(),
                    Loading: false,
                    Error: ex.Message,
                    SpecialtyFilter: null
                )
            );
        }
    }

    private static async void FilterBySpecialty(string specialty, Action<State> setState)
    {
        if (string.IsNullOrEmpty(specialty))
        {
            LoadPractitioners(setState);
            return;
        }

        try
        {
            var practitioners = await ApiClient.SearchPractitionersAsync(specialty);
            setState(
                new State(
                    Practitioners: practitioners,
                    Loading: false,
                    Error: null,
                    SpecialtyFilter: specialty
                )
            );
        }
        catch (Exception ex)
        {
            setState(
                new State(
                    Practitioners: Array.Empty<Practitioner>(),
                    Loading: false,
                    Error: ex.Message,
                    SpecialtyFilter: specialty
                )
            );
        }
    }

    private static ReactElement RenderError(string message) =>
        Div(
            className: "card",
            style: new { borderLeft = "4px solid var(--error)" },
            children: new[]
            {
                Div(
                    className: "flex items-center gap-3 p-4",
                    children: new[] { Icons.X(), Text($"Error loading practitioners: {message}") }
                ),
            }
        );

    private static ReactElement RenderEmpty() =>
        Div(
            className: "card",
            children: new[]
            {
                Div(
                    className: "empty-state",
                    children: new[]
                    {
                        Icons.UserDoctor(),
                        H(
                            4,
                            className: "empty-state-title",
                            children: new[] { Text("No Practitioners") }
                        ),
                        P(
                            className: "empty-state-description",
                            children: new[]
                            {
                                Text(
                                    "No practitioners found. Add a new practitioner to get started."
                                ),
                            }
                        ),
                        Button(
                            className: "btn btn-primary mt-4",
                            children: new[] { Icons.Plus(), Text("Add Practitioner") }
                        ),
                    }
                ),
            }
        );

    private static ReactElement RenderLoadingGrid() =>
        Div(
            className: "grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6",
            children: Enumerable
                .Range(0, 6)
                .Select(_ =>
                    Div(
                        className: "card",
                        children: new[]
                        {
                            Div(
                                className: "skeleton",
                                style: new
                                {
                                    width = "80px",
                                    height = "80px",
                                    borderRadius = "50%",
                                }
                            ),
                            Div(
                                className: "skeleton mt-4",
                                style: new { width = "60%", height = "20px" }
                            ),
                            Div(
                                className: "skeleton mt-2",
                                style: new { width = "40%", height = "16px" }
                            ),
                            Div(
                                className: "skeleton mt-4",
                                style: new { width = "100%", height = "16px" }
                            ),
                        }
                    )
                )
                .ToArray()
        );

    private static ReactElement RenderPractitionerGrid(Practitioner[] practitioners) =>
        Div(
            className: "grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6",
            children: practitioners.Select(RenderPractitionerCard).ToArray()
        );

    private static ReactElement RenderPractitionerCard(Practitioner practitioner) =>
        Div(
            className: "card",
            children: new[]
            {
                // Header
                Div(
                    className: "flex items-start gap-4",
                    children: new[]
                    {
                        Div(
                            className: "avatar avatar-xl",
                            children: new[] { Text(GetInitials(practitioner)) }
                        ),
                        Div(
                            className: "flex-1",
                            children: new[]
                            {
                                H(
                                    4,
                                    className: "font-semibold",
                                    children: new[]
                                    {
                                        Text(
                                            $"Dr. {practitioner.NameGiven} {practitioner.NameFamily}"
                                        ),
                                    }
                                ),
                                Span(
                                    className: "badge badge-teal mt-1",
                                    children: new[] { Text(practitioner.Specialty ?? "General") }
                                ),
                                Div(
                                    className: "flex items-center gap-2 mt-2",
                                    children: new[]
                                    {
                                        Span(
                                            className: $"status-dot {(practitioner.Active ? "active" : "inactive")}"
                                        ),
                                        Text(practitioner.Active ? "Available" : "Unavailable"),
                                    }
                                ),
                            }
                        ),
                    }
                ),
                // Details
                Div(
                    className: "mt-4 pt-4 border-t border-gray-200",
                    children: new[]
                    {
                        RenderDetail("ID", practitioner.Identifier),
                        RenderDetail("Qualification", practitioner.Qualification ?? "N/A"),
                        RenderDetail("Email", practitioner.TelecomEmail ?? "N/A"),
                        RenderDetail("Phone", practitioner.TelecomPhone ?? "N/A"),
                    }
                ),
                // Actions
                Div(
                    className: "flex gap-2 mt-4",
                    children: new[]
                    {
                        Button(
                            className: "btn btn-primary btn-sm flex-1",
                            children: new[] { Icons.Calendar(), Text("View Schedule") }
                        ),
                        Button(
                            className: "btn btn-secondary btn-sm",
                            children: new[] { Icons.Edit() }
                        ),
                    }
                ),
            }
        );

    private static ReactElement RenderDetail(string label, string value) =>
        Div(
            className: "flex justify-between py-1",
            children: new[]
            {
                Span(className: "text-sm text-gray-500", children: new[] { Text(label) }),
                Span(className: "text-sm font-medium", children: new[] { Text(value) }),
            }
        );

    private static string GetInitials(Practitioner p) =>
        $"{FirstChar(p.NameGiven)}{FirstChar(p.NameFamily)}";

    private static string FirstChar(string? s) =>
        string.IsNullOrEmpty(s) ? "" : s[..1].ToUpperInvariant();
}
