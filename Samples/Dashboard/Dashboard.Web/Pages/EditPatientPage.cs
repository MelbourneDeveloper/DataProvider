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
    /// Edit patient page state class.
    /// </summary>
    public class EditPatientState
    {
        /// <summary>Patient being edited.</summary>
        public Patient Patient { get; set; }

        /// <summary>Whether loading.</summary>
        public bool Loading { get; set; }

        /// <summary>Whether saving.</summary>
        public bool Saving { get; set; }

        /// <summary>Error message if any.</summary>
        public string Error { get; set; }

        /// <summary>Success message if any.</summary>
        public string Success { get; set; }

        /// <summary>Form field: Active status.</summary>
        public bool Active { get; set; }

        /// <summary>Form field: Given name.</summary>
        public string GivenName { get; set; }

        /// <summary>Form field: Family name.</summary>
        public string FamilyName { get; set; }

        /// <summary>Form field: Birth date.</summary>
        public string BirthDate { get; set; }

        /// <summary>Form field: Gender.</summary>
        public string Gender { get; set; }

        /// <summary>Form field: Phone.</summary>
        public string Phone { get; set; }

        /// <summary>Form field: Email.</summary>
        public string Email { get; set; }

        /// <summary>Form field: Address line.</summary>
        public string AddressLine { get; set; }

        /// <summary>Form field: City.</summary>
        public string City { get; set; }

        /// <summary>Form field: State.</summary>
        public string State { get; set; }

        /// <summary>Form field: Postal code.</summary>
        public string PostalCode { get; set; }

        /// <summary>Form field: Country.</summary>
        public string Country { get; set; }
    }

    /// <summary>
    /// Edit patient page component.
    /// </summary>
    public static class EditPatientPage
    {
        /// <summary>
        /// Renders the edit patient page.
        /// </summary>
        public static ReactElement Render(string patientId, Action onBack)
        {
            var stateResult = UseState(
                new EditPatientState
                {
                    Patient = null,
                    Loading = true,
                    Saving = false,
                    Error = null,
                    Success = null,
                    Active = true,
                    GivenName = "",
                    FamilyName = "",
                    BirthDate = "",
                    Gender = "",
                    Phone = "",
                    Email = "",
                    AddressLine = "",
                    City = "",
                    State = "",
                    PostalCode = "",
                    Country = "",
                }
            );

            var state = stateResult.State;
            var setState = stateResult.SetState;

            UseEffect(
                () =>
                {
                    LoadPatient(patientId, setState);
                },
                new object[] { patientId }
            );

            if (state.Loading)
            {
                return RenderLoadingState();
            }

            if (state.Error != null && state.Patient == null)
            {
                return RenderErrorState(state.Error, onBack);
            }

            return Div(
                className: "page",
                children: new[]
                {
                    RenderHeader(state.Patient, onBack),
                    RenderForm(state, setState, onBack),
                }
            );
        }

        private static async void LoadPatient(string patientId, Action<EditPatientState> setState)
        {
            try
            {
                var patient = await ApiClient.GetPatientAsync(patientId);
                setState(
                    new EditPatientState
                    {
                        Patient = patient,
                        Loading = false,
                        Saving = false,
                        Error = null,
                        Success = null,
                        Active = patient.Active,
                        GivenName = patient.GivenName ?? "",
                        FamilyName = patient.FamilyName ?? "",
                        BirthDate = patient.BirthDate ?? "",
                        Gender = patient.Gender ?? "",
                        Phone = patient.Phone ?? "",
                        Email = patient.Email ?? "",
                        AddressLine = patient.AddressLine ?? "",
                        City = patient.City ?? "",
                        State = patient.State ?? "",
                        PostalCode = patient.PostalCode ?? "",
                        Country = patient.Country ?? "",
                    }
                );
            }
            catch (Exception ex)
            {
                setState(
                    new EditPatientState
                    {
                        Patient = null,
                        Loading = false,
                        Saving = false,
                        Error = ex.Message,
                        Success = null,
                        Active = true,
                        GivenName = "",
                        FamilyName = "",
                        BirthDate = "",
                        Gender = "",
                        Phone = "",
                        Email = "",
                        AddressLine = "",
                        City = "",
                        State = "",
                        PostalCode = "",
                        Country = "",
                    }
                );
            }
        }

        private static async void SavePatient(
            EditPatientState state,
            Action<EditPatientState> setState,
            Action onBack
        )
        {
            setState(
                new EditPatientState
                {
                    Patient = state.Patient,
                    Loading = false,
                    Saving = true,
                    Error = null,
                    Success = null,
                    Active = state.Active,
                    GivenName = state.GivenName,
                    FamilyName = state.FamilyName,
                    BirthDate = state.BirthDate,
                    Gender = state.Gender,
                    Phone = state.Phone,
                    Email = state.Email,
                    AddressLine = state.AddressLine,
                    City = state.City,
                    State = state.State,
                    PostalCode = state.PostalCode,
                    Country = state.Country,
                }
            );

            try
            {
                var updateData = new Patient
                {
                    Id = state.Patient.Id,
                    Active = state.Active,
                    GivenName = state.GivenName,
                    FamilyName = state.FamilyName,
                    BirthDate = string.IsNullOrWhiteSpace(state.BirthDate) ? null : state.BirthDate,
                    Gender = string.IsNullOrWhiteSpace(state.Gender) ? null : state.Gender,
                    Phone = string.IsNullOrWhiteSpace(state.Phone) ? null : state.Phone,
                    Email = string.IsNullOrWhiteSpace(state.Email) ? null : state.Email,
                    AddressLine = string.IsNullOrWhiteSpace(state.AddressLine)
                        ? null
                        : state.AddressLine,
                    City = string.IsNullOrWhiteSpace(state.City) ? null : state.City,
                    State = string.IsNullOrWhiteSpace(state.State) ? null : state.State,
                    PostalCode = string.IsNullOrWhiteSpace(state.PostalCode)
                        ? null
                        : state.PostalCode,
                    Country = string.IsNullOrWhiteSpace(state.Country) ? null : state.Country,
                };

                var updatedPatient = await ApiClient.UpdatePatientAsync(
                    state.Patient.Id,
                    updateData
                );

                setState(
                    new EditPatientState
                    {
                        Patient = updatedPatient,
                        Loading = false,
                        Saving = false,
                        Error = null,
                        Success = "Patient updated successfully!",
                        Active = updatedPatient.Active,
                        GivenName = updatedPatient.GivenName ?? "",
                        FamilyName = updatedPatient.FamilyName ?? "",
                        BirthDate = updatedPatient.BirthDate ?? "",
                        Gender = updatedPatient.Gender ?? "",
                        Phone = updatedPatient.Phone ?? "",
                        Email = updatedPatient.Email ?? "",
                        AddressLine = updatedPatient.AddressLine ?? "",
                        City = updatedPatient.City ?? "",
                        State = updatedPatient.State ?? "",
                        PostalCode = updatedPatient.PostalCode ?? "",
                        Country = updatedPatient.Country ?? "",
                    }
                );
            }
            catch (Exception ex)
            {
                setState(
                    new EditPatientState
                    {
                        Patient = state.Patient,
                        Loading = false,
                        Saving = false,
                        Error = ex.Message,
                        Success = null,
                        Active = state.Active,
                        GivenName = state.GivenName,
                        FamilyName = state.FamilyName,
                        BirthDate = state.BirthDate,
                        Gender = state.Gender,
                        Phone = state.Phone,
                        Email = state.Email,
                        AddressLine = state.AddressLine,
                        City = state.City,
                        State = state.State,
                        PostalCode = state.PostalCode,
                        Country = state.Country,
                    }
                );
            }
        }

        private static ReactElement RenderLoadingState() =>
            Div(
                className: "page",
                children: new[]
                {
                    Div(
                        className: "page-header",
                        children: new[]
                        {
                            H(2, className: "page-title", children: new[] { Text("Edit Patient") }),
                            P(
                                className: "page-description",
                                children: new[] { Text("Loading patient data...") }
                            ),
                        }
                    ),
                    Div(
                        className: "card",
                        children: new[]
                        {
                            Div(
                                className: "flex items-center justify-center p-8",
                                children: new[] { Text("Loading...") }
                            ),
                        }
                    ),
                }
            );

        private static ReactElement RenderErrorState(string error, Action onBack) =>
            Div(
                className: "page",
                children: new[]
                {
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
                                        children: new[] { Text("Edit Patient") }
                                    ),
                                    P(
                                        className: "page-description",
                                        children: new[] { Text("Error loading patient") }
                                    ),
                                }
                            ),
                            Button(
                                className: "btn btn-secondary",
                                onClick: onBack,
                                children: new[] { Icons.ChevronLeft(), Text("Back to Patients") }
                            ),
                        }
                    ),
                    Div(
                        className: "card",
                        style: new { borderLeft = "4px solid var(--error)" },
                        children: new[]
                        {
                            Div(
                                className: "flex items-center gap-3 p-4",
                                children: new[]
                                {
                                    Icons.X(),
                                    Text("Error loading patient: " + error),
                                }
                            ),
                        }
                    ),
                }
            );

        private static ReactElement RenderHeader(Patient patient, Action onBack)
        {
            var fullName = patient.GivenName + " " + patient.FamilyName;
            return Div(
                className: "page-header flex justify-between items-center",
                children: new[]
                {
                    Div(
                        children: new[]
                        {
                            H(2, className: "page-title", children: new[] { Text("Edit Patient") }),
                            P(
                                className: "page-description",
                                children: new[] { Text("Update information for " + fullName) }
                            ),
                        }
                    ),
                    Button(
                        className: "btn btn-secondary",
                        onClick: onBack,
                        children: new[] { Icons.ChevronLeft(), Text("Back to Patients") }
                    ),
                }
            );
        }

        private static ReactElement RenderForm(
            EditPatientState state,
            Action<EditPatientState> setState,
            Action onBack
        ) =>
            Div(
                className: "card",
                children: new[]
                {
                    state.Error != null
                        ? Div(
                            className: "alert alert-error mb-4",
                            children: new[] { Icons.X(), Text(state.Error) }
                        )
                        : null,
                    state.Success != null
                        ? Div(
                            className: "alert alert-success mb-4",
                            children: new[] { Text(state.Success) }
                        )
                        : null,
                    Form(
                        className: "form",
                        onSubmit: () => SavePatient(state, setState, onBack),
                        children: new[]
                        {
                            RenderFormSection(
                                "Personal Information",
                                new[]
                                {
                                    RenderInputField(
                                        "Given Name",
                                        "patient-edit-given-name",
                                        state.GivenName,
                                        "Enter first name",
                                        v => UpdateField(state, setState, "GivenName", v)
                                    ),
                                    RenderInputField(
                                        "Family Name",
                                        "patient-edit-family-name",
                                        state.FamilyName,
                                        "Enter last name",
                                        v => UpdateField(state, setState, "FamilyName", v)
                                    ),
                                    RenderInputField(
                                        "Birth Date",
                                        "patient-edit-birth-date",
                                        state.BirthDate,
                                        "YYYY-MM-DD",
                                        v => UpdateField(state, setState, "BirthDate", v),
                                        "date"
                                    ),
                                    RenderSelectField(
                                        "Gender",
                                        "patient-edit-gender",
                                        state.Gender,
                                        new[]
                                        {
                                            ("", "Select gender"),
                                            ("male", "Male"),
                                            ("female", "Female"),
                                            ("other", "Other"),
                                            ("unknown", "Unknown"),
                                        },
                                        v => UpdateField(state, setState, "Gender", v)
                                    ),
                                    RenderCheckboxField(
                                        "Active",
                                        "patient-edit-active",
                                        state.Active,
                                        v => UpdateActive(state, setState, v)
                                    ),
                                }
                            ),
                            RenderFormSection(
                                "Contact Information",
                                new[]
                                {
                                    RenderInputField(
                                        "Phone",
                                        "patient-edit-phone",
                                        state.Phone,
                                        "Enter phone number",
                                        v => UpdateField(state, setState, "Phone", v),
                                        "tel"
                                    ),
                                    RenderInputField(
                                        "Email",
                                        "patient-edit-email",
                                        state.Email,
                                        "Enter email address",
                                        v => UpdateField(state, setState, "Email", v),
                                        "email"
                                    ),
                                }
                            ),
                            RenderFormSection(
                                "Address",
                                new[]
                                {
                                    RenderInputField(
                                        "Address Line",
                                        "patient-edit-address",
                                        state.AddressLine,
                                        "Enter street address",
                                        v => UpdateField(state, setState, "AddressLine", v)
                                    ),
                                    RenderInputField(
                                        "City",
                                        "patient-edit-city",
                                        state.City,
                                        "Enter city",
                                        v => UpdateField(state, setState, "City", v)
                                    ),
                                    RenderInputField(
                                        "State",
                                        "patient-edit-state",
                                        state.State,
                                        "Enter state/province",
                                        v => UpdateField(state, setState, "State", v)
                                    ),
                                    RenderInputField(
                                        "Postal Code",
                                        "patient-edit-postal-code",
                                        state.PostalCode,
                                        "Enter postal code",
                                        v => UpdateField(state, setState, "PostalCode", v)
                                    ),
                                    RenderInputField(
                                        "Country",
                                        "patient-edit-country",
                                        state.Country,
                                        "Enter country",
                                        v => UpdateField(state, setState, "Country", v)
                                    ),
                                }
                            ),
                            RenderFormActions(state, onBack),
                        }
                    ),
                }
            );

        private static ReactElement RenderFormSection(string title, ReactElement[] fields) =>
            Div(
                className: "form-section mb-6",
                children: new[]
                {
                    H(4, className: "form-section-title mb-4", children: new[] { Text(title) }),
                    Div(className: "grid grid-cols-2 gap-4", children: fields),
                }
            );

        private static ReactElement RenderInputField(
            string label,
            string id,
            string value,
            string placeholder,
            Action<string> onChange,
            string type = "text"
        ) =>
            Div(
                className: "form-group",
                children: new[]
                {
                    Label(htmlFor: id, className: "form-label", children: new[] { Text(label) }),
                    Input(
                        className: "input",
                        type: type,
                        value: value,
                        placeholder: placeholder,
                        onChange: onChange
                    ),
                }
            );

        private static ReactElement RenderSelectField(
            string label,
            string id,
            string value,
            (string value, string label)[] options,
            Action<string> onChange
        )
        {
            var optionElements = new ReactElement[options.Length];
            for (var i = 0; i < options.Length; i++)
            {
                optionElements[i] = Option(options[i].value, options[i].label);
            }

            return Div(
                className: "form-group",
                children: new[]
                {
                    Label(htmlFor: id, className: "form-label", children: new[] { Text(label) }),
                    Select(
                        className: "input",
                        value: value,
                        onChange: onChange,
                        children: optionElements
                    ),
                }
            );
        }

        private static ReactElement RenderCheckboxField(
            string label,
            string id,
            bool value,
            Action<bool> onChange
        ) =>
            Div(
                className: "form-group flex items-center gap-2",
                children: new[]
                {
                    Div(
                        className: "flex items-center gap-2",
                        onClick: () => onChange(!value),
                        children: new[]
                        {
                            Span(className: "status-dot " + (value ? "active" : "inactive")),
                            Text(label + ": " + (value ? "Active" : "Inactive")),
                        }
                    ),
                }
            );

        private static ReactElement RenderFormActions(EditPatientState state, Action onBack) =>
            Div(
                className: "form-actions flex justify-end gap-4 mt-6",
                children: new[]
                {
                    Button(
                        className: "btn btn-secondary",
                        type: "button",
                        onClick: onBack,
                        disabled: state.Saving,
                        children: new[] { Text("Cancel") }
                    ),
                    Button(
                        className: "btn btn-primary",
                        type: "submit",
                        disabled: state.Saving,
                        children: new[] { Text(state.Saving ? "Saving..." : "Save Changes") }
                    ),
                }
            );

        private static void UpdateField(
            EditPatientState state,
            Action<EditPatientState> setState,
            string field,
            string value
        )
        {
            var newState = new EditPatientState
            {
                Patient = state.Patient,
                Loading = state.Loading,
                Saving = state.Saving,
                Error = null,
                Success = null,
                Active = state.Active,
                GivenName = state.GivenName,
                FamilyName = state.FamilyName,
                BirthDate = state.BirthDate,
                Gender = state.Gender,
                Phone = state.Phone,
                Email = state.Email,
                AddressLine = state.AddressLine,
                City = state.City,
                State = state.State,
                PostalCode = state.PostalCode,
                Country = state.Country,
            };

            if (field == "GivenName")
                newState.GivenName = value;
            else if (field == "FamilyName")
                newState.FamilyName = value;
            else if (field == "BirthDate")
                newState.BirthDate = value;
            else if (field == "Gender")
                newState.Gender = value;
            else if (field == "Phone")
                newState.Phone = value;
            else if (field == "Email")
                newState.Email = value;
            else if (field == "AddressLine")
                newState.AddressLine = value;
            else if (field == "City")
                newState.City = value;
            else if (field == "State")
                newState.State = value;
            else if (field == "PostalCode")
                newState.PostalCode = value;
            else if (field == "Country")
                newState.Country = value;

            setState(newState);
        }

        private static void UpdateActive(
            EditPatientState state,
            Action<EditPatientState> setState,
            bool value
        ) =>
            setState(
                new EditPatientState
                {
                    Patient = state.Patient,
                    Loading = state.Loading,
                    Saving = state.Saving,
                    Error = null,
                    Success = null,
                    Active = value,
                    GivenName = state.GivenName,
                    FamilyName = state.FamilyName,
                    BirthDate = state.BirthDate,
                    Gender = state.Gender,
                    Phone = state.Phone,
                    Email = state.Email,
                    AddressLine = state.AddressLine,
                    City = state.City,
                    State = state.State,
                    PostalCode = state.PostalCode,
                    Country = state.Country,
                }
            );
    }
}
