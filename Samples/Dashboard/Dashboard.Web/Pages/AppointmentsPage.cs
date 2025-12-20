namespace Dashboard.Pages;

using Dashboard.Api;
using Dashboard.Components;
using Dashboard.Models;
using Dashboard.React;
using static Dashboard.React.Elements;
using static Dashboard.React.Hooks;

/// <summary>
/// Appointments management page.
/// </summary>
public static class AppointmentsPage
{
    /// <summary>
    /// Page state record.
    /// </summary>
    public record State(
        Appointment[] Appointments,
        bool Loading,
        string? Error,
        string? StatusFilter
    );

    /// <summary>
    /// Renders the appointments page.
    /// </summary>
    public static ReactElement Render()
    {
        var (state, setState) = UseState(
            new State(
                Appointments: Array.Empty<Appointment>(),
                Loading: true,
                Error: null,
                StatusFilter: null
            )
        );

        UseEffect(
            () =>
            {
                LoadAppointments(setState);
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
                                    children: new[] { Text("Appointments") }
                                ),
                                P(
                                    className: "page-description",
                                    children: new[]
                                    {
                                        Text(
                                            "Manage scheduled appointments from the Scheduling domain"
                                        ),
                                    }
                                ),
                            }
                        ),
                        Button(
                            className: "btn btn-primary",
                            children: new[] { Icons.Plus(), Text("New Appointment") }
                        ),
                    }
                ),
                // Filters
                Div(
                    className: "card mb-6",
                    children: new[]
                    {
                        Div(
                            className: "tabs",
                            children: new[]
                            {
                                RenderTab(
                                    "All",
                                    null,
                                    state.StatusFilter,
                                    s => FilterByStatus(s, setState)
                                ),
                                RenderTab(
                                    "Booked",
                                    "booked",
                                    state.StatusFilter,
                                    s => FilterByStatus(s, setState)
                                ),
                                RenderTab(
                                    "Arrived",
                                    "arrived",
                                    state.StatusFilter,
                                    s => FilterByStatus(s, setState)
                                ),
                                RenderTab(
                                    "Fulfilled",
                                    "fulfilled",
                                    state.StatusFilter,
                                    s => FilterByStatus(s, setState)
                                ),
                                RenderTab(
                                    "Cancelled",
                                    "cancelled",
                                    state.StatusFilter,
                                    s => FilterByStatus(s, setState)
                                ),
                            }
                        ),
                    }
                ),
                // Content
                state.Loading
                    ? RenderLoadingList()
                : state.Error != null ? RenderError(state.Error)
                : state.Appointments.Length == 0 ? RenderEmpty()
                : RenderAppointmentList(state.Appointments),
            }
        );
    }

    private static async void LoadAppointments(Action<State> setState)
    {
        try
        {
            var appointments = await ApiClient.GetAppointmentsAsync();
            setState(
                new State(
                    Appointments: appointments,
                    Loading: false,
                    Error: null,
                    StatusFilter: null
                )
            );
        }
        catch (Exception ex)
        {
            setState(
                new State(
                    Appointments: Array.Empty<Appointment>(),
                    Loading: false,
                    Error: ex.Message,
                    StatusFilter: null
                )
            );
        }
    }

    private static void FilterByStatus(string? status, Action<State> setState)
    {
        // TODO: Implement server-side filtering or client-side filter
        setState(s => s with { StatusFilter = status });
    }

    private static ReactElement RenderTab(
        string label,
        string? status,
        string? currentFilter,
        Action<string?> onSelect
    )
    {
        var isActive = status == currentFilter;
        return Button(
            className: $"tab {(isActive ? "active" : "")}",
            onClick: () => onSelect(status),
            children: new[] { Text(label) }
        );
    }

    private static ReactElement RenderError(string message) =>
        Div(
            className: "card",
            style: new { borderLeft = "4px solid var(--error)" },
            children: new[]
            {
                Div(
                    className: "flex items-center gap-3 p-4",
                    children: new[] { Icons.X(), Text($"Error loading appointments: {message}") }
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
                        Icons.Calendar(),
                        H(
                            4,
                            className: "empty-state-title",
                            children: new[] { Text("No Appointments") }
                        ),
                        P(
                            className: "empty-state-description",
                            children: new[]
                            {
                                Text(
                                    "No appointments scheduled. Create a new appointment to get started."
                                ),
                            }
                        ),
                        Button(
                            className: "btn btn-primary mt-4",
                            children: new[] { Icons.Plus(), Text("New Appointment") }
                        ),
                    }
                ),
            }
        );

    private static ReactElement RenderLoadingList() =>
        Div(
            className: "data-list",
            children: Enumerable
                .Range(0, 5)
                .Select(_ =>
                    Div(
                        className: "card",
                        children: new[]
                        {
                            Div(
                                className: "flex items-center gap-4",
                                children: new[]
                                {
                                    Div(
                                        className: "skeleton",
                                        style: new
                                        {
                                            width = "60px",
                                            height = "60px",
                                            borderRadius = "var(--radius-lg)",
                                        }
                                    ),
                                    Div(
                                        className: "flex-1",
                                        children: new[]
                                        {
                                            Div(
                                                className: "skeleton",
                                                style: new { width = "200px", height = "20px" }
                                            ),
                                            Div(
                                                className: "skeleton mt-2",
                                                style: new { width = "150px", height = "16px" }
                                            ),
                                        }
                                    ),
                                    Div(
                                        className: "skeleton",
                                        style: new { width = "100px", height = "32px" }
                                    ),
                                }
                            ),
                        }
                    )
                )
                .ToArray()
        );

    private static ReactElement RenderAppointmentList(Appointment[] appointments) =>
        Div(className: "data-list", children: appointments.Select(RenderAppointmentCard).ToArray());

    private static ReactElement RenderAppointmentCard(Appointment appointment) =>
        Div(
            className: "card-glass mb-4",
            children: new[]
            {
                Div(
                    className: "flex items-start gap-4",
                    children: new[]
                    {
                        // Time block
                        Div(
                            className: "metric-icon blue",
                            style: new { width = "60px", height = "60px" },
                            children: new[]
                            {
                                Div(
                                    className: "text-center",
                                    children: new[]
                                    {
                                        Div(
                                            className: "text-lg font-bold",
                                            children: new[]
                                            {
                                                Text(FormatTime(appointment.StartTime)),
                                            }
                                        ),
                                        Div(
                                            className: "text-xs",
                                            children: new[]
                                            {
                                                Text($"{appointment.MinutesDuration}min"),
                                            }
                                        ),
                                    }
                                ),
                            }
                        ),
                        // Details
                        Div(
                            className: "flex-1",
                            children: new[]
                            {
                                Div(
                                    className: "flex items-center gap-2",
                                    children: new[]
                                    {
                                        H(
                                            4,
                                            className: "font-semibold",
                                            children: new[]
                                            {
                                                Text(appointment.ServiceType ?? "Appointment"),
                                            }
                                        ),
                                        RenderStatusBadge(appointment.Status),
                                        RenderPriorityBadge(appointment.Priority),
                                    }
                                ),
                                Div(
                                    className: "text-sm text-gray-600 mt-1",
                                    children: new[]
                                    {
                                        Icons.Users(),
                                        Text(
                                            $" Patient: {FormatReference(appointment.PatientReference)}"
                                        ),
                                    }
                                ),
                                Div(
                                    className: "text-sm text-gray-600",
                                    children: new[]
                                    {
                                        Icons.UserDoctor(),
                                        Text(
                                            $" Provider: {FormatReference(appointment.PractitionerReference)}"
                                        ),
                                    }
                                ),
                                appointment.Description != null
                                    ? P(
                                        className: "text-sm mt-2",
                                        children: new[] { Text(appointment.Description) }
                                    )
                                    : Text(""),
                            }
                        ),
                        // Actions
                        Div(
                            className: "flex flex-col gap-2",
                            children: new[]
                            {
                                Button(
                                    className: "btn btn-primary btn-sm",
                                    children: new[] { Text("Check In") }
                                ),
                                Button(
                                    className: "btn btn-secondary btn-sm",
                                    children: new[] { Icons.Edit() }
                                ),
                            }
                        ),
                    }
                ),
            }
        );

    private static ReactElement RenderStatusBadge(string status)
    {
        var badgeClass = status switch
        {
            "booked" => "badge-primary",
            "arrived" => "badge-teal",
            "fulfilled" => "badge-success",
            "cancelled" => "badge-error",
            "noshow" => "badge-warning",
            _ => "badge-gray",
        };
        return Span(className: $"badge {badgeClass}", children: new[] { Text(status) });
    }

    private static ReactElement RenderPriorityBadge(string priority)
    {
        if (priority == "routine")
            return Text("");

        var badgeClass = priority switch
        {
            "urgent" => "badge-warning",
            "asap" => "badge-error",
            "stat" => "badge-error",
            _ => "badge-gray",
        };
        return Span(
            className: $"badge {badgeClass}",
            children: new[] { Text(priority.ToUpperInvariant()) }
        );
    }

    private static string FormatTime(string? dateTime)
    {
        if (string.IsNullOrEmpty(dateTime))
            return "N/A";
        // Simple time extraction - in real app use proper date parsing
        return dateTime.Length > 11 ? dateTime[11..16] : dateTime;
    }

    private static string FormatReference(string reference)
    {
        // Extract ID from reference like "Patient/abc-123" -> "abc-123"
        var parts = reference.Split('/');
        return parts.Length > 1 ? parts[1][..Math.Min(8, parts[1].Length)] + "..." : reference;
    }
}
