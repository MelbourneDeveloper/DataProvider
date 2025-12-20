namespace Dashboard.Pages
{
    using System;
    using Dashboard.Api;
    using Dashboard.Components;
    using Dashboard.Models;
    using Dashboard.React;
    using static Dashboard.React.Elements;
    using static Dashboard.React.Hooks;

    /// <summary>
    /// Patients page state class.
    /// </summary>
    public class PatientsState
    {
        /// <summary>List of patients.</summary>
        public Patient[] Patients { get; set; }

        /// <summary>Whether loading.</summary>
        public bool Loading { get; set; }

        /// <summary>Error message if any.</summary>
        public string Error { get; set; }

        /// <summary>Current search query.</summary>
        public string SearchQuery { get; set; }

        /// <summary>Selected patient.</summary>
        public Patient SelectedPatient { get; set; }
    }

    /// <summary>
    /// Patients list page.
    /// </summary>
    public static class PatientsPage
    {
        /// <summary>
        /// Renders the patients page.
        /// </summary>
        public static ReactElement Render()
        {
            var stateResult = UseState(
                new PatientsState
                {
                    Patients = new Patient[0],
                    Loading = true,
                    Error = null,
                    SearchQuery = "",
                    SelectedPatient = null,
                }
            );

            var state = stateResult.State;
            var setState = stateResult.SetState;

            UseEffect(
                () =>
                {
                    LoadPatients(setState);
                },
                new object[0]
            );

            ReactElement content;
            if (state.Loading)
            {
                content = DataTable.RenderLoading(5, 5);
            }
            else if (state.Error != null)
            {
                content = RenderError(state.Error);
            }
            else if (state.Patients.Length == 0)
            {
                content = DataTable.RenderEmpty(
                    "No patients found. Start by adding a new patient."
                );
            }
            else
            {
                content = RenderPatientTable(state.Patients, p => SelectPatient(p, setState));
            }

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
                                        children: new[] { Text("Patients") }
                                    ),
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
                    content,
                }
            );
        }

        private static async void LoadPatients(Action<PatientsState> setState)
        {
            try
            {
                var patients = await ApiClient.GetPatientsAsync();
                setState(
                    new PatientsState
                    {
                        Patients = patients,
                        Loading = false,
                        Error = null,
                        SearchQuery = "",
                        SelectedPatient = null,
                    }
                );
            }
            catch (Exception ex)
            {
                setState(
                    new PatientsState
                    {
                        Patients = new Patient[0],
                        Loading = false,
                        Error = ex.Message,
                        SearchQuery = "",
                        SelectedPatient = null,
                    }
                );
            }
        }

        private static async void HandleSearch(string query, Action<PatientsState> setState)
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
                    new PatientsState
                    {
                        Patients = patients,
                        Loading = false,
                        Error = null,
                        SearchQuery = query,
                        SelectedPatient = null,
                    }
                );
            }
            catch (Exception ex)
            {
                setState(
                    new PatientsState
                    {
                        Patients = new Patient[0],
                        Loading = false,
                        Error = ex.Message,
                        SearchQuery = query,
                        SelectedPatient = null,
                    }
                );
            }
        }

        private static void SelectPatient(Patient patient, Action<PatientsState> setState)
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
                        children: new[] { Icons.X(), Text("Error loading patients: " + message) }
                    ),
                }
            );

        private static ReactElement RenderPatientTable(Patient[] patients, Action<Patient> onSelect)
        {
            var columns = new[]
            {
                new Column { Key = "name", Header = "Name" },
                new Column { Key = "gender", Header = "Gender" },
                new Column { Key = "birthDate", Header = "Birth Date" },
                new Column { Key = "contact", Header = "Contact" },
                new Column { Key = "status", Header = "Status" },
                new Column
                {
                    Key = "actions",
                    Header = "Actions",
                    ClassName = "text-right",
                },
            };

            return DataTable.Render(
                columns: columns,
                data: patients,
                getKey: p => p.Id,
                renderCell: (patient, key) => RenderCell(patient, key, onSelect),
                onRowClick: onSelect
            );
        }

        private static ReactElement RenderCell(
            Patient patient,
            string key,
            Action<Patient> onSelect
        )
        {
            if (key == "name")
                return RenderPatientName(patient);
            if (key == "gender")
                return RenderGender(patient.Gender);
            if (key == "birthDate")
                return Text(patient.BirthDate ?? "N/A");
            if (key == "contact")
                return RenderContact(patient);
            if (key == "status")
                return RenderStatus(patient.Active);
            if (key == "actions")
                return RenderActions(patient, onSelect);
            return Text("");
        }

        private static ReactElement RenderPatientName(Patient patient)
        {
            var idPrefix = patient.Id.Length > 8 ? patient.Id.Substring(0, 8) : patient.Id;
            return Div(
                className: "flex items-center gap-3",
                children: new[]
                {
                    Div(
                        className: "avatar avatar-sm",
                        children: new[] { Text(GetInitials(patient)) }
                    ),
                    Div(
                        children: new[]
                        {
                            Div(
                                className: "font-medium",
                                children: new[]
                                {
                                    Text(patient.GivenName + " " + patient.FamilyName),
                                }
                            ),
                            Div(
                                className: "text-sm text-gray-500",
                                children: new[] { Text("ID: " + idPrefix + "...") }
                            ),
                        }
                    ),
                }
            );
        }

        private static ReactElement RenderGender(string gender) =>
            Span(
                className: "badge " + GenderBadgeClass(gender),
                children: new[] { Text(gender ?? "Unknown") }
            );

        private static string GenderBadgeClass(string gender)
        {
            if (gender == "male")
                return "badge-primary";
            if (gender == "female")
                return "badge-teal";
            return "badge-gray";
        }

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
                    Span(className: "status-dot " + (active ? "active" : "inactive")),
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
            FirstChar(patient.GivenName) + FirstChar(patient.FamilyName);

        private static string FirstChar(string s)
        {
            if (string.IsNullOrEmpty(s))
                return "";
            return s.Substring(0, 1).ToUpper();
        }
    }
}
