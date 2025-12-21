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
    /// Edit appointment page state class.
    /// </summary>
    public class EditAppointmentState
    {
        /// <summary>Appointment being edited.</summary>
        public Appointment Appointment { get; set; }

        /// <summary>Whether loading.</summary>
        public bool Loading { get; set; }

        /// <summary>Whether saving.</summary>
        public bool Saving { get; set; }

        /// <summary>Error message if any.</summary>
        public string Error { get; set; }

        /// <summary>Success message if any.</summary>
        public string Success { get; set; }

        /// <summary>Form field: Service category.</summary>
        public string ServiceCategory { get; set; }

        /// <summary>Form field: Service type.</summary>
        public string ServiceType { get; set; }

        /// <summary>Form field: Reason code.</summary>
        public string ReasonCode { get; set; }

        /// <summary>Form field: Priority.</summary>
        public string Priority { get; set; }

        /// <summary>Form field: Description.</summary>
        public string Description { get; set; }

        /// <summary>Form field: Start date.</summary>
        public string StartDate { get; set; }

        /// <summary>Form field: Start time.</summary>
        public string StartTime { get; set; }

        /// <summary>Form field: End date.</summary>
        public string EndDate { get; set; }

        /// <summary>Form field: End time.</summary>
        public string EndTime { get; set; }

        /// <summary>Form field: Patient reference.</summary>
        public string PatientReference { get; set; }

        /// <summary>Form field: Practitioner reference.</summary>
        public string PractitionerReference { get; set; }

        /// <summary>Form field: Comment.</summary>
        public string Comment { get; set; }

        /// <summary>Form field: Status.</summary>
        public string Status { get; set; }
    }

    /// <summary>
    /// Edit appointment page component.
    /// </summary>
    public static class EditAppointmentPage
    {
        /// <summary>
        /// Renders the edit appointment page.
        /// </summary>
        public static ReactElement Render(string appointmentId, Action onBack)
        {
            var stateResult = UseState(
                new EditAppointmentState
                {
                    Appointment = null,
                    Loading = true,
                    Saving = false,
                    Error = null,
                    Success = null,
                    ServiceCategory = "",
                    ServiceType = "",
                    ReasonCode = "",
                    Priority = "routine",
                    Description = "",
                    StartDate = "",
                    StartTime = "",
                    EndDate = "",
                    EndTime = "",
                    PatientReference = "",
                    PractitionerReference = "",
                    Comment = "",
                    Status = "booked",
                }
            );

            var state = stateResult.State;
            var setState = stateResult.SetState;

            UseEffect(
                () =>
                {
                    LoadAppointment(appointmentId, setState);
                },
                new object[] { appointmentId }
            );

            if (state.Loading)
            {
                return RenderLoadingState();
            }

            if (state.Error != null && state.Appointment == null)
            {
                return RenderErrorState(state.Error, onBack);
            }

            return Div(
                className: "page",
                children: new[]
                {
                    RenderHeader(state.Appointment, onBack),
                    RenderForm(state, setState, onBack),
                }
            );
        }

        private static async void LoadAppointment(
            string appointmentId,
            Action<EditAppointmentState> setState
        )
        {
            try
            {
                var appointment = await ApiClient.GetAppointmentAsync(appointmentId);
                var startParts = ParseDateTime(appointment.StartTime);
                var endParts = ParseDateTime(appointment.EndTime);

                setState(
                    new EditAppointmentState
                    {
                        Appointment = appointment,
                        Loading = false,
                        Saving = false,
                        Error = null,
                        Success = null,
                        ServiceCategory = appointment.ServiceCategory ?? "",
                        ServiceType = appointment.ServiceType ?? "",
                        ReasonCode = appointment.ReasonCode ?? "",
                        Priority = appointment.Priority ?? "routine",
                        Description = appointment.Description ?? "",
                        StartDate = startParts.Item1,
                        StartTime = startParts.Item2,
                        EndDate = endParts.Item1,
                        EndTime = endParts.Item2,
                        PatientReference = appointment.PatientReference ?? "",
                        PractitionerReference = appointment.PractitionerReference ?? "",
                        Comment = appointment.Comment ?? "",
                        Status = appointment.Status ?? "booked",
                    }
                );
            }
            catch (Exception ex)
            {
                setState(
                    new EditAppointmentState
                    {
                        Appointment = null,
                        Loading = false,
                        Saving = false,
                        Error = ex.Message,
                        Success = null,
                        ServiceCategory = "",
                        ServiceType = "",
                        ReasonCode = "",
                        Priority = "routine",
                        Description = "",
                        StartDate = "",
                        StartTime = "",
                        EndDate = "",
                        EndTime = "",
                        PatientReference = "",
                        PractitionerReference = "",
                        Comment = "",
                        Status = "booked",
                    }
                );
            }
        }

        private static (string, string) ParseDateTime(string isoDateTime)
        {
            if (string.IsNullOrEmpty(isoDateTime))
                return ("", "");

            // Parse ISO datetime like "2025-12-20T12:02:00.000Z"
            if (isoDateTime.Length >= 16)
            {
                var datePart = isoDateTime.Substring(0, 10);
                var timePart = isoDateTime.Substring(11, 5);
                return (datePart, timePart);
            }

            return ("", "");
        }

        private static string CombineDateTime(string date, string time)
        {
            if (string.IsNullOrEmpty(date) || string.IsNullOrEmpty(time))
                return "";
            return date + "T" + time + ":00.000Z";
        }

        private static async void SaveAppointment(
            EditAppointmentState state,
            Action<EditAppointmentState> setState,
            Action onBack
        )
        {
            setState(
                new EditAppointmentState
                {
                    Appointment = state.Appointment,
                    Loading = false,
                    Saving = true,
                    Error = null,
                    Success = null,
                    ServiceCategory = state.ServiceCategory,
                    ServiceType = state.ServiceType,
                    ReasonCode = state.ReasonCode,
                    Priority = state.Priority,
                    Description = state.Description,
                    StartDate = state.StartDate,
                    StartTime = state.StartTime,
                    EndDate = state.EndDate,
                    EndTime = state.EndTime,
                    PatientReference = state.PatientReference,
                    PractitionerReference = state.PractitionerReference,
                    Comment = state.Comment,
                    Status = state.Status,
                }
            );

            try
            {
                var updateData = new
                {
                    ServiceCategory = state.ServiceCategory,
                    ServiceType = state.ServiceType,
                    ReasonCode = string.IsNullOrWhiteSpace(state.ReasonCode)
                        ? null
                        : state.ReasonCode,
                    Priority = state.Priority,
                    Description = string.IsNullOrWhiteSpace(state.Description)
                        ? null
                        : state.Description,
                    Start = CombineDateTime(state.StartDate, state.StartTime),
                    End = CombineDateTime(state.EndDate, state.EndTime),
                    PatientReference = state.PatientReference,
                    PractitionerReference = state.PractitionerReference,
                    Comment = string.IsNullOrWhiteSpace(state.Comment) ? null : state.Comment,
                    Status = state.Status,
                };

                var updatedAppointment = await ApiClient.UpdateAppointmentAsync(
                    state.Appointment.Id,
                    updateData
                );

                var startParts = ParseDateTime(updatedAppointment.StartTime);
                var endParts = ParseDateTime(updatedAppointment.EndTime);

                setState(
                    new EditAppointmentState
                    {
                        Appointment = updatedAppointment,
                        Loading = false,
                        Saving = false,
                        Error = null,
                        Success = "Appointment updated successfully!",
                        ServiceCategory = updatedAppointment.ServiceCategory ?? "",
                        ServiceType = updatedAppointment.ServiceType ?? "",
                        ReasonCode = updatedAppointment.ReasonCode ?? "",
                        Priority = updatedAppointment.Priority ?? "routine",
                        Description = updatedAppointment.Description ?? "",
                        StartDate = startParts.Item1,
                        StartTime = startParts.Item2,
                        EndDate = endParts.Item1,
                        EndTime = endParts.Item2,
                        PatientReference = updatedAppointment.PatientReference ?? "",
                        PractitionerReference = updatedAppointment.PractitionerReference ?? "",
                        Comment = updatedAppointment.Comment ?? "",
                        Status = updatedAppointment.Status ?? "booked",
                    }
                );
            }
            catch (Exception ex)
            {
                setState(
                    new EditAppointmentState
                    {
                        Appointment = state.Appointment,
                        Loading = false,
                        Saving = false,
                        Error = ex.Message,
                        Success = null,
                        ServiceCategory = state.ServiceCategory,
                        ServiceType = state.ServiceType,
                        ReasonCode = state.ReasonCode,
                        Priority = state.Priority,
                        Description = state.Description,
                        StartDate = state.StartDate,
                        StartTime = state.StartTime,
                        EndDate = state.EndDate,
                        EndTime = state.EndTime,
                        PatientReference = state.PatientReference,
                        PractitionerReference = state.PractitionerReference,
                        Comment = state.Comment,
                        Status = state.Status,
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
                            H(
                                2,
                                className: "page-title",
                                children: new[] { Text("Edit Appointment") }
                            ),
                            P(
                                className: "page-description",
                                children: new[] { Text("Loading appointment data...") }
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
                                        children: new[] { Text("Edit Appointment") }
                                    ),
                                    P(
                                        className: "page-description",
                                        children: new[] { Text("Error loading appointment") }
                                    ),
                                }
                            ),
                            Button(
                                className: "btn btn-secondary",
                                onClick: onBack,
                                children: new[]
                                {
                                    Icons.ChevronLeft(),
                                    Text("Back to Appointments"),
                                }
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
                                    Text("Error loading appointment: " + error),
                                }
                            ),
                        }
                    ),
                }
            );

        private static ReactElement RenderHeader(Appointment appointment, Action onBack)
        {
            var title = appointment.ServiceType ?? "Appointment";
            return Div(
                className: "page-header flex justify-between items-center",
                children: new[]
                {
                    Div(
                        children: new[]
                        {
                            H(
                                2,
                                className: "page-title",
                                children: new[] { Text("Edit Appointment") }
                            ),
                            P(
                                className: "page-description",
                                children: new[] { Text("Update details for " + title) }
                            ),
                        }
                    ),
                    Button(
                        className: "btn btn-secondary",
                        onClick: onBack,
                        children: new[] { Icons.ChevronLeft(), Text("Back to Appointments") }
                    ),
                }
            );
        }

        private static ReactElement RenderForm(
            EditAppointmentState state,
            Action<EditAppointmentState> setState,
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
                        onSubmit: () => SaveAppointment(state, setState, onBack),
                        children: new[]
                        {
                            RenderFormSection(
                                "Appointment Details",
                                new[]
                                {
                                    RenderInputField(
                                        "Service Category",
                                        "appointment-service-category",
                                        state.ServiceCategory,
                                        "e.g., General Practice",
                                        v => UpdateField(state, setState, "ServiceCategory", v)
                                    ),
                                    RenderInputField(
                                        "Service Type",
                                        "appointment-service-type",
                                        state.ServiceType,
                                        "e.g., Checkup, Follow-up",
                                        v => UpdateField(state, setState, "ServiceType", v)
                                    ),
                                    RenderInputField(
                                        "Reason",
                                        "appointment-reason",
                                        state.ReasonCode,
                                        "Reason for appointment",
                                        v => UpdateField(state, setState, "ReasonCode", v)
                                    ),
                                    RenderSelectField(
                                        "Priority",
                                        "appointment-priority",
                                        state.Priority,
                                        new[]
                                        {
                                            ("routine", "Routine"),
                                            ("urgent", "Urgent"),
                                            ("asap", "ASAP"),
                                            ("stat", "STAT"),
                                        },
                                        v => UpdateField(state, setState, "Priority", v)
                                    ),
                                    RenderSelectField(
                                        "Status",
                                        "appointment-status",
                                        state.Status,
                                        new[]
                                        {
                                            ("booked", "Booked"),
                                            ("arrived", "Arrived"),
                                            ("fulfilled", "Fulfilled"),
                                            ("cancelled", "Cancelled"),
                                            ("noshow", "No Show"),
                                        },
                                        v => UpdateField(state, setState, "Status", v)
                                    ),
                                    RenderTextareaField(
                                        "Description",
                                        "appointment-description",
                                        state.Description,
                                        "Additional details",
                                        v => UpdateField(state, setState, "Description", v)
                                    ),
                                }
                            ),
                            RenderFormSection(
                                "Schedule",
                                new[]
                                {
                                    RenderInputField(
                                        "Start Date",
                                        "appointment-start-date",
                                        state.StartDate,
                                        "YYYY-MM-DD",
                                        v => UpdateField(state, setState, "StartDate", v),
                                        "date"
                                    ),
                                    RenderInputField(
                                        "Start Time",
                                        "appointment-start-time",
                                        state.StartTime,
                                        "HH:MM",
                                        v => UpdateField(state, setState, "StartTime", v),
                                        "time"
                                    ),
                                    RenderInputField(
                                        "End Date",
                                        "appointment-end-date",
                                        state.EndDate,
                                        "YYYY-MM-DD",
                                        v => UpdateField(state, setState, "EndDate", v),
                                        "date"
                                    ),
                                    RenderInputField(
                                        "End Time",
                                        "appointment-end-time",
                                        state.EndTime,
                                        "HH:MM",
                                        v => UpdateField(state, setState, "EndTime", v),
                                        "time"
                                    ),
                                }
                            ),
                            RenderFormSection(
                                "Participants",
                                new[]
                                {
                                    RenderInputField(
                                        "Patient Reference",
                                        "appointment-patient",
                                        state.PatientReference,
                                        "Patient/[id]",
                                        v => UpdateField(state, setState, "PatientReference", v)
                                    ),
                                    RenderInputField(
                                        "Practitioner Reference",
                                        "appointment-practitioner",
                                        state.PractitionerReference,
                                        "Practitioner/[id]",
                                        v =>
                                            UpdateField(state, setState, "PractitionerReference", v)
                                    ),
                                }
                            ),
                            RenderFormSection(
                                "Notes",
                                new[]
                                {
                                    RenderTextareaField(
                                        "Comment",
                                        "appointment-comment",
                                        state.Comment,
                                        "Any additional comments",
                                        v => UpdateField(state, setState, "Comment", v)
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

        private static ReactElement RenderTextareaField(
            string label,
            string id,
            string value,
            string placeholder,
            Action<string> onChange
        ) =>
            Div(
                className: "form-group col-span-2",
                children: new[]
                {
                    Label(htmlFor: id, className: "form-label", children: new[] { Text(label) }),
                    TextArea(
                        className: "input",
                        value: value,
                        placeholder: placeholder,
                        onChange: onChange,
                        rows: 3
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

        private static ReactElement RenderFormActions(EditAppointmentState state, Action onBack) =>
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
            EditAppointmentState state,
            Action<EditAppointmentState> setState,
            string field,
            string value
        )
        {
            var newState = new EditAppointmentState
            {
                Appointment = state.Appointment,
                Loading = state.Loading,
                Saving = state.Saving,
                Error = null,
                Success = null,
                ServiceCategory = state.ServiceCategory,
                ServiceType = state.ServiceType,
                ReasonCode = state.ReasonCode,
                Priority = state.Priority,
                Description = state.Description,
                StartDate = state.StartDate,
                StartTime = state.StartTime,
                EndDate = state.EndDate,
                EndTime = state.EndTime,
                PatientReference = state.PatientReference,
                PractitionerReference = state.PractitionerReference,
                Comment = state.Comment,
                Status = state.Status,
            };

            if (field == "ServiceCategory")
                newState.ServiceCategory = value;
            else if (field == "ServiceType")
                newState.ServiceType = value;
            else if (field == "ReasonCode")
                newState.ReasonCode = value;
            else if (field == "Priority")
                newState.Priority = value;
            else if (field == "Description")
                newState.Description = value;
            else if (field == "StartDate")
                newState.StartDate = value;
            else if (field == "StartTime")
                newState.StartTime = value;
            else if (field == "EndDate")
                newState.EndDate = value;
            else if (field == "EndTime")
                newState.EndTime = value;
            else if (field == "PatientReference")
                newState.PatientReference = value;
            else if (field == "PractitionerReference")
                newState.PractitionerReference = value;
            else if (field == "Comment")
                newState.Comment = value;
            else if (field == "Status")
                newState.Status = value;

            setState(newState);
        }
    }
}
