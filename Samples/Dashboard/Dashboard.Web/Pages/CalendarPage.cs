using System;
using System.Linq;
using Dashboard.Api;
using Dashboard.Components;
using Dashboard.Models;
using Dashboard.React;
using static Dashboard.React.Elements;
using static Dashboard.React.Hooks;

namespace Dashboard.Pages
{
    /// <summary>
    /// Calendar page state class.
    /// </summary>
    public class CalendarState
    {
        /// <summary>List of appointments.</summary>
        public Appointment[] Appointments { get; set; }

        /// <summary>Whether loading.</summary>
        public bool Loading { get; set; }

        /// <summary>Error message if any.</summary>
        public string Error { get; set; }

        /// <summary>Current view year.</summary>
        public int Year { get; set; }

        /// <summary>Current view month (1-12).</summary>
        public int Month { get; set; }

        /// <summary>Selected day for details view.</summary>
        public int SelectedDay { get; set; }
    }

    /// <summary>
    /// Calendar-based schedule view page.
    /// </summary>
    public static class CalendarPage
    {
        private static readonly string[] MonthNames = new[]
        {
            "January",
            "February",
            "March",
            "April",
            "May",
            "June",
            "July",
            "August",
            "September",
            "October",
            "November",
            "December",
        };

        private static readonly string[] DayNames = new[]
        {
            "Sun",
            "Mon",
            "Tue",
            "Wed",
            "Thu",
            "Fri",
            "Sat",
        };

        /// <summary>
        /// Renders the calendar page.
        /// </summary>
        public static ReactElement Render(Action<string> onEditAppointment)
        {
            var now = DateTime.Now;
            var stateResult = UseState(
                new CalendarState
                {
                    Appointments = new Appointment[0],
                    Loading = true,
                    Error = null,
                    Year = now.Year,
                    Month = now.Month,
                    SelectedDay = 0,
                }
            );

            var state = stateResult.State;
            var setState = stateResult.SetState;

            UseEffect(
                () =>
                {
                    LoadAppointments(setState, state);
                },
                new object[0]
            );

            return Div(
                className: "page",
                children: new[]
                {
                    RenderHeader(state, setState),
                    state.Loading ? RenderLoadingState()
                    : state.Error != null ? RenderError(state.Error)
                    : RenderCalendarContent(state, setState, onEditAppointment),
                }
            );
        }

        private static async void LoadAppointments(
            Action<CalendarState> setState,
            CalendarState currentState
        )
        {
            try
            {
                var appointments = await ApiClient.GetAppointmentsAsync();
                setState(
                    new CalendarState
                    {
                        Appointments = appointments,
                        Loading = false,
                        Error = null,
                        Year = currentState.Year,
                        Month = currentState.Month,
                        SelectedDay = currentState.SelectedDay,
                    }
                );
            }
            catch (Exception ex)
            {
                setState(
                    new CalendarState
                    {
                        Appointments = new Appointment[0],
                        Loading = false,
                        Error = ex.Message,
                        Year = currentState.Year,
                        Month = currentState.Month,
                        SelectedDay = currentState.SelectedDay,
                    }
                );
            }
        }

        private static ReactElement RenderHeader(
            CalendarState state,
            Action<CalendarState> setState
        )
        {
            var monthName = MonthNames[state.Month - 1];
            return Div(
                className: "page-header flex justify-between items-center mb-6",
                children: new[]
                {
                    Div(
                        children: new[]
                        {
                            H(2, className: "page-title", children: new[] { Text("Schedule") }),
                            P(
                                className: "page-description",
                                children: new[]
                                {
                                    Text("View and manage appointments on the calendar"),
                                }
                            ),
                        }
                    ),
                    Div(
                        className: "flex items-center gap-4",
                        children: new[]
                        {
                            Button(
                                className: "btn btn-secondary btn-sm",
                                onClick: () => NavigateMonth(state, setState, -1),
                                children: new[] { Icons.ChevronLeft() }
                            ),
                            Span(
                                className: "text-lg font-semibold",
                                children: new[] { Text(monthName + " " + state.Year) }
                            ),
                            Button(
                                className: "btn btn-secondary btn-sm",
                                onClick: () => NavigateMonth(state, setState, 1),
                                children: new[] { Icons.ChevronRight() }
                            ),
                            Button(
                                className: "btn btn-primary btn-sm ml-4",
                                onClick: () => GoToToday(state, setState),
                                children: new[] { Text("Today") }
                            ),
                        }
                    ),
                }
            );
        }

        private static void NavigateMonth(
            CalendarState state,
            Action<CalendarState> setState,
            int delta
        )
        {
            var newMonth = state.Month + delta;
            var newYear = state.Year;

            if (newMonth < 1)
            {
                newMonth = 12;
                newYear--;
            }
            else if (newMonth > 12)
            {
                newMonth = 1;
                newYear++;
            }

            setState(
                new CalendarState
                {
                    Appointments = state.Appointments,
                    Loading = state.Loading,
                    Error = state.Error,
                    Year = newYear,
                    Month = newMonth,
                    SelectedDay = 0,
                }
            );
        }

        private static void GoToToday(CalendarState state, Action<CalendarState> setState)
        {
            var now = DateTime.Now;
            setState(
                new CalendarState
                {
                    Appointments = state.Appointments,
                    Loading = state.Loading,
                    Error = state.Error,
                    Year = now.Year,
                    Month = now.Month,
                    SelectedDay = now.Day,
                }
            );
        }

        private static ReactElement RenderLoadingState() =>
            Div(
                className: "card",
                children: new[]
                {
                    Div(
                        className: "flex items-center justify-center p-8",
                        children: new[] { Text("Loading calendar...") }
                    ),
                }
            );

        private static ReactElement RenderError(string message) =>
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
                            Text("Error loading appointments: " + message),
                        }
                    ),
                }
            );

        private static ReactElement RenderCalendarContent(
            CalendarState state,
            Action<CalendarState> setState,
            Action<string> onEditAppointment
        ) =>
            Div(
                className: "flex gap-6",
                children: new[]
                {
                    Div(
                        className: "flex-1",
                        children: new[] { RenderCalendarGrid(state, setState) }
                    ),
                    state.SelectedDay > 0
                        ? RenderDayDetails(state, setState, onEditAppointment)
                        : RenderNoSelection(),
                }
            );

        private static ReactElement RenderCalendarGrid(
            CalendarState state,
            Action<CalendarState> setState
        )
        {
            var daysInMonth = DateTime.DaysInMonth(state.Year, state.Month);
            var firstDay = new DateTime(state.Year, state.Month, 1);
            var startDayOfWeek = (int)firstDay.DayOfWeek;

            var headerCells = DayNames
                .Select(day =>
                    Div(
                        className: "calendar-header-cell text-center font-semibold p-2",
                        children: new[] { Text(day) }
                    )
                )
                .ToArray();

            var dayCells = new ReactElement[42]; // 6 rows * 7 days
            var today = DateTime.Now;

            for (var i = 0; i < 42; i++)
            {
                var dayNum = i - startDayOfWeek + 1;
                if (dayNum < 1 || dayNum > daysInMonth)
                {
                    dayCells[i] = Div(
                        className: "calendar-cell empty",
                        children: new ReactElement[0]
                    );
                }
                else
                {
                    var appointments = GetAppointmentsForDay(
                        state.Appointments,
                        state.Year,
                        state.Month,
                        dayNum
                    );
                    var isToday =
                        state.Year == today.Year
                        && state.Month == today.Month
                        && dayNum == today.Day;
                    var isSelected = dayNum == state.SelectedDay;
                    var dayNumCaptured = dayNum;

                    var cellClasses = "calendar-cell";
                    if (isToday)
                        cellClasses += " today";
                    if (isSelected)
                        cellClasses += " selected";
                    if (appointments.Length > 0)
                        cellClasses += " has-appointments";

                    dayCells[i] = Div(
                        className: cellClasses,
                        onClick: () => SelectDay(state, setState, dayNumCaptured),
                        children: new[]
                        {
                            Div(
                                className: "calendar-day-number",
                                children: new[] { Text(dayNum.ToString()) }
                            ),
                            appointments.Length > 0
                                ? Div(
                                    className: "calendar-appointments-preview",
                                    children: appointments
                                        .Take(3)
                                        .Select(a => RenderAppointmentDot(a))
                                        .ToArray()
                                )
                                : null,
                            appointments.Length > 3
                                ? Span(
                                    className: "calendar-more-indicator",
                                    children: new[] { Text("+" + (appointments.Length - 3)) }
                                )
                                : null,
                        }
                    );
                }
            }

            return Div(
                className: "card calendar-grid-container",
                children: new[]
                {
                    Div(className: "calendar-grid-header grid grid-cols-7", children: headerCells),
                    Div(className: "calendar-grid grid grid-cols-7", children: dayCells),
                }
            );
        }

        private static ReactElement RenderAppointmentDot(Appointment appointment)
        {
            var dotClass = "calendar-dot";
            if (appointment.Status == "booked")
                dotClass += " blue";
            else if (appointment.Status == "arrived")
                dotClass += " teal";
            else if (appointment.Status == "fulfilled")
                dotClass += " green";
            else if (appointment.Status == "cancelled")
                dotClass += " red";
            else
                dotClass += " gray";

            return Span(className: dotClass);
        }

        private static void SelectDay(
            CalendarState state,
            Action<CalendarState> setState,
            int day
        ) =>
            setState(
                new CalendarState
                {
                    Appointments = state.Appointments,
                    Loading = state.Loading,
                    Error = state.Error,
                    Year = state.Year,
                    Month = state.Month,
                    SelectedDay = day,
                }
            );

        private static Appointment[] GetAppointmentsForDay(
            Appointment[] appointments,
            int year,
            int month,
            int day
        )
        {
            var targetDate = new DateTime(year, month, day).ToString("yyyy-MM-dd");
            return appointments
                .Where(a => a.StartTime != null && a.StartTime.StartsWith(targetDate))
                .OrderBy(a => a.StartTime)
                .ToArray();
        }

        private static ReactElement RenderNoSelection() =>
            Div(
                className: "card calendar-details-panel",
                style: new { width = "320px", minHeight = "400px" },
                children: new[]
                {
                    Div(
                        className: "empty-state p-6",
                        children: new[]
                        {
                            Icons.Calendar(),
                            H(
                                4,
                                className: "empty-state-title mt-4",
                                children: new[] { Text("Select a Day") }
                            ),
                            P(
                                className: "empty-state-description",
                                children: new[] { Text("Click on a day to view appointments") }
                            ),
                        }
                    ),
                }
            );

        private static ReactElement RenderDayDetails(
            CalendarState state,
            Action<CalendarState> setState,
            Action<string> onEditAppointment
        )
        {
            var appointments = GetAppointmentsForDay(
                state.Appointments,
                state.Year,
                state.Month,
                state.SelectedDay
            );

            var monthName = MonthNames[state.Month - 1];
            var dateStr = monthName + " " + state.SelectedDay + ", " + state.Year;

            return Div(
                className: "card calendar-details-panel",
                style: new { width = "320px", minHeight = "400px" },
                children: new[]
                {
                    Div(
                        className: "flex justify-between items-center mb-4 p-4 border-b",
                        children: new[]
                        {
                            H(4, className: "font-semibold", children: new[] { Text(dateStr) }),
                            Button(
                                className: "btn btn-secondary btn-sm",
                                onClick: () => SelectDay(state, setState, 0),
                                children: new[] { Icons.X() }
                            ),
                        }
                    ),
                    appointments.Length == 0
                        ? Div(
                            className: "empty-state p-6",
                            children: new[]
                            {
                                Icons.Calendar(),
                                P(
                                    className: "empty-state-description mt-4",
                                    children: new[] { Text("No appointments scheduled") }
                                ),
                            }
                        )
                        : Div(
                            className: "p-4 space-y-3",
                            children: appointments
                                .Select(a => RenderDayAppointment(a, onEditAppointment))
                                .ToArray()
                        ),
                }
            );
        }

        private static ReactElement RenderDayAppointment(
            Appointment appointment,
            Action<string> onEditAppointment
        )
        {
            var time = FormatTime(appointment.StartTime);
            var endTime = FormatTime(appointment.EndTime);
            var statusClass = GetStatusClass(appointment.Status);

            return Div(
                className: "calendar-appointment-item p-3 rounded-lg border",
                children: new[]
                {
                    Div(
                        className: "flex justify-between items-start mb-2",
                        children: new[]
                        {
                            Div(
                                children: new[]
                                {
                                    Div(
                                        className: "font-semibold",
                                        children: new[]
                                        {
                                            Text(appointment.ServiceType ?? "Appointment"),
                                        }
                                    ),
                                    Div(
                                        className: "text-sm text-gray-500",
                                        children: new[] { Text(time + " - " + endTime) }
                                    ),
                                }
                            ),
                            Span(
                                className: "badge " + statusClass,
                                children: new[] { Text(appointment.Status ?? "unknown") }
                            ),
                        }
                    ),
                    Div(
                        className: "text-sm text-gray-600 mb-2",
                        children: new[]
                        {
                            Div(
                                children: new[]
                                {
                                    Text(
                                        "Patient: " + FormatReference(appointment.PatientReference)
                                    ),
                                }
                            ),
                            Div(
                                children: new[]
                                {
                                    Text(
                                        "Provider: "
                                            + FormatReference(appointment.PractitionerReference)
                                    ),
                                }
                            ),
                        }
                    ),
                    Div(
                        className: "flex gap-2",
                        children: new[]
                        {
                            Button(
                                className: "btn btn-primary btn-sm flex-1",
                                onClick: () => onEditAppointment(appointment.Id),
                                children: new[] { Icons.Edit(), Text("Edit") }
                            ),
                        }
                    ),
                }
            );
        }

        private static string FormatTime(string dateTime)
        {
            if (string.IsNullOrEmpty(dateTime))
                return "N/A";
            if (dateTime.Length > 16)
                return dateTime.Substring(11, 5);
            return dateTime;
        }

        private static string FormatReference(string reference)
        {
            if (string.IsNullOrEmpty(reference))
                return "N/A";
            var parts = reference.Split('/');
            if (parts.Length > 1)
            {
                var id = parts[1];
                var length = Math.Min(8, id.Length);
                return id.Substring(0, length) + "...";
            }
            return reference;
        }

        private static string GetStatusClass(string status)
        {
            if (status == "booked")
                return "badge-primary";
            if (status == "arrived")
                return "badge-teal";
            if (status == "fulfilled")
                return "badge-success";
            if (status == "cancelled")
                return "badge-error";
            if (status == "noshow")
                return "badge-warning";
            return "badge-gray";
        }
    }
}
