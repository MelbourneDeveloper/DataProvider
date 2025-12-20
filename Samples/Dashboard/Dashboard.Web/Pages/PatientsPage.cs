namespace Dashboard.Pages;

using Dashboard.Api;
using Dashboard.Components;
using Dashboard.Models;
using Dashboard.React;
using static Dashboard.React.Elements;
using static Dashboard.React.Hooks;

/// <summary>
/// Patients list page.
/// </summary>
public static class PatientsPage
{
    /// <summary>
    /// Page state record.
    /// </summary>
    public record State(
        Patient[] Patients,
        bool Loading,
        string? Error,
        string SearchQuery,
        Patient? SelectedPatient
    );

    /// <summary>
    /// Renders the patients page.
    /// </summary>
    public static ReactElement Render()
    {
        var (state, setState) = UseState(
            new State(
                Patients: Array.Empty<Patient>(),
                Loading: true,
                Error: null,
                SearchQuery: "",
                SelectedPatient: null
            )
        );

        UseEffect(
            () =>
            {
                LoadPatients(setState);
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
                                H(2, className: "page-title", children: new[] { Text("Patients") }),
                                P(
                                    className: "page-description",
                                    children: new[]
                                    {
                                        Text("Manage patient records from the Clinical domain"),
                                    }
                                ),
                            }
                        ),
                        Button(
                            className: "btn btn-primary",
                            children: new[] { Icons.Plus(), Text("Add Patient") }
                        ),
                    }
                ),
                // Search bar
                Div(
                    className: "card mb-6",
                    children: new[]
                    {
                        Div(
                            className: "flex gap-4",
                            children: new[]
                            {
                                Div(
                                    className: "flex-1 search-input",
                                    children: new[]
                                    {
                                        Span(
                                            className: "search-icon",
                                            children: new[] { Icons.Search() }
                                        ),
                                        Input(
                                            className: "input",
                                            type: "text",
                                            placeholder: "Search patients by name...",
                                            value: state.SearchQuery,
                                            onChange: query => HandleSearch(query, setState)
                                        ),
                                    }
                                ),
                                Button(
                                    className: "btn btn-secondary",
                                    onClick: () => LoadPatients(setState),
                                    children: new[] { Icons.Refresh(), Text("Refresh") }
                                ),
                            }
                        ),
                    }
                ),
                // Content
                state.Loading
                    ? DataTable.RenderLoading(5, 5)
                : state.Error != null ? RenderError(state.Error)
                : state.Patients.Length == 0
                    ? DataTable.RenderEmpty("No patients found. Start by adding a new patient.")
                : RenderPatientTable(state.Patients, p => SelectPatient(p, setState)),
            }
        );
    }

    private static async void LoadPatients(Action<State> setState)
    {
        try
        {
            var patients = await ApiClient.GetPatientsAsync();
            setState(
                new State(
                    Patients: patients,
                    Loading: false,
                    Error: null,
                    SearchQuery: "",
                    SelectedPatient: null
                )
            );
        }
        catch (Exception ex)
        {
            setState(
                new State(
                    Patients: Array.Empty<Patient>(),
                    Loading: false,
                    Error: ex.Message,
                    SearchQuery: "",
                    SelectedPatient: null
                )
            );
        }
    }

    private static async void HandleSearch(string query, Action<State> setState)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            LoadPatients(setState);
            return;
        }

        try
        {
            var patients = await ApiClient.SearchPatientsAsync(query);
            setState(
                new State(
                    Patients: patients,
                    Loading: false,
                    Error: null,
                    SearchQuery: query,
                    SelectedPatient: null
                )
            );
        }
        catch (Exception ex)
        {
            setState(
                new State(
                    Patients: Array.Empty<Patient>(),
                    Loading: false,
                    Error: ex.Message,
                    SearchQuery: query,
                    SelectedPatient: null
                )
            );
        }
    }

    private static void SelectPatient(Patient patient, Action<State> setState)
    {
        // TODO: Navigate to patient detail or open modal
    }

    private static ReactElement RenderError(string message) =>
        Div(
            className: "card",
            style: new { borderLeft = "4px solid var(--error)" },
            children: new[]
            {
                Div(
                    className: "flex items-center gap-3 p-4",
                    children: new[] { Icons.X(), Text($"Error loading patients: {message}") }
                ),
            }
        );

    private static ReactElement RenderPatientTable(Patient[] patients, Action<Patient> onSelect)
    {
        var columns = new[]
        {
            new DataTable.Column("name", "Name"),
            new DataTable.Column("gender", "Gender"),
            new DataTable.Column("birthDate", "Birth Date"),
            new DataTable.Column("contact", "Contact"),
            new DataTable.Column("status", "Status"),
            new DataTable.Column("actions", "Actions", "text-right"),
        };

        return DataTable.Render(
            columns: columns,
            data: patients,
            getKey: p => p.Id,
            renderCell: (patient, key) =>
                key switch
                {
                    "name" => RenderPatientName(patient),
                    "gender" => RenderGender(patient.Gender),
                    "birthDate" => Text(patient.BirthDate ?? "N/A"),
                    "contact" => RenderContact(patient),
                    "status" => RenderStatus(patient.Active),
                    "actions" => RenderActions(patient, onSelect),
                    _ => Text(""),
                },
            onRowClick: onSelect
        );
    }

    private static ReactElement RenderPatientName(Patient patient) =>
        Div(
            className: "flex items-center gap-3",
            children: new[]
            {
                Div(className: "avatar avatar-sm", children: new[] { Text(GetInitials(patient)) }),
                Div(
                    children: new[]
                    {
                        Div(
                            className: "font-medium",
                            children: new[] { Text($"{patient.GivenName} {patient.FamilyName}") }
                        ),
                        Div(
                            className: "text-sm text-gray-500",
                            children: new[] { Text($"ID: {patient.Id[..8]}...") }
                        ),
                    }
                ),
            }
        );

    private static ReactElement RenderGender(string? gender) =>
        Span(
            className: $"badge {GenderBadgeClass(gender)}",
            children: new[] { Text(gender ?? "Unknown") }
        );

    private static string GenderBadgeClass(string? gender) =>
        gender switch
        {
            "male" => "badge-primary",
            "female" => "badge-teal",
            _ => "badge-gray",
        };

    private static ReactElement RenderContact(Patient patient)
    {
        var contact = patient.Email ?? patient.Phone ?? "No contact";
        return Text(contact);
    }

    private static ReactElement RenderStatus(bool active) =>
        Div(
            className: "flex items-center gap-2",
            children: new[]
            {
                Span(className: $"status-dot {(active ? "active" : "inactive")}"),
                Text(active ? "Active" : "Inactive"),
            }
        );

    private static ReactElement RenderActions(Patient patient, Action<Patient> onSelect) =>
        Div(
            className: "table-action",
            children: new[]
            {
                Button(
                    className: "btn btn-ghost btn-sm",
                    onClick: () => onSelect(patient),
                    children: new[] { Icons.Eye() }
                ),
                Button(className: "btn btn-ghost btn-sm", children: new[] { Icons.Edit() }),
            }
        );

    private static string GetInitials(Patient patient) =>
        $"{FirstChar(patient.GivenName)}{FirstChar(patient.FamilyName)}";

    private static string FirstChar(string? s) =>
        string.IsNullOrEmpty(s) ? "" : s[..1].ToUpperInvariant();
}
