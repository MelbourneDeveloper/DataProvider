namespace Healthcare.ClinicScheduling.Queries;

using Lql;
using Lql.SQLite;
using Microsoft.Data.Sqlite;

/// <summary>
/// LQL-based queries for the ClinicScheduling service.
/// Uses functional pipeline syntax instead of raw SQL files.
/// </summary>
public static class ScheduleQueries
{
    /// <summary>
    /// Get today's appointments for a provider with patient info.
    /// LQL: Appointment |> join(ScheduledPatient, PatientId) |> join(ExamRoom, RoomId)
    ///      |> where(ProviderId = @providerId AND AppointmentDate = @date) |> orderBy(StartTime)
    /// </summary>
    public static string GetProviderDailySchedule =>
        """
            Appointment
            |> join(ScheduledPatient, PatientId, PatientId)
            |> join(ExamRoom, RoomId, RoomId)
            |> select(
                AppointmentId, AppointmentDate, StartTime, EndTime, DurationMinutes,
                AppointmentType, Status, ReasonForVisit, ChiefComplaint,
                PatientId, DisplayName, ContactPhone, HasAllergies, AllergyWarning,
                RoomNumber, RoomType
            )
            |> where(ProviderId = @providerId)
            |> where(AppointmentDate = @date)
            |> orderBy(StartTime)
            """;

    /// <summary>
    /// Find available appointment slots for a provider on a given date.
    /// </summary>
    public static string GetProviderAvailability =>
        """
            ProviderSchedule
            |> join(Provider, ProviderId, ProviderId)
            |> select(ProviderId, FullName, DayOfWeek, StartTime, EndTime, DefaultAppointmentMinutes)
            |> where(ProviderId = @providerId)
            |> where(IsAvailable = 1)
            """;

    /// <summary>
    /// Get appointments by status for dashboard views.
    /// </summary>
    public static string GetAppointmentsByStatus =>
        """
            Appointment
            |> join(ScheduledPatient, PatientId, PatientId)
            |> join(Provider, ProviderId, ProviderId)
            |> select(
                AppointmentId, AppointmentDate, StartTime, Status,
                DisplayName, FullName, AppointmentType, ReasonForVisit
            )
            |> where(AppointmentDate = @date)
            |> where(Status = @status)
            |> orderBy(StartTime)
            """;

    /// <summary>
    /// Search waitlist with priority ordering.
    /// </summary>
    public static string GetWaitlistByPriority =>
        """
            WaitlistEntry
            |> join(ScheduledPatient, PatientId, PatientId)
            |> select(
                WaitlistId, PatientId, DisplayName, ContactPhone,
                AppointmentType, Priority, ReasonForVisit, AddedAt, Status
            )
            |> where(Status = 'Waiting')
            |> orderBy(Priority)
            |> orderBy(AddedAt)
            |> take(@limit)
            """;

    /// <summary>
    /// Get upcoming reminders to send.
    /// </summary>
    public static string GetPendingReminders =>
        """
            AppointmentReminder
            |> join(Appointment, AppointmentId, AppointmentId)
            |> join(ScheduledPatient, PatientId, PatientId)
            |> select(
                ReminderId, ReminderType, ScheduledFor,
                AppointmentId, AppointmentDate, StartTime,
                PatientId, DisplayName, ContactPhone, ContactEmail
            )
            |> where(DeliveryStatus = 'Pending')
            |> where(ScheduledFor <= @cutoffTime)
            |> orderBy(ScheduledFor)
            """;

    /// <summary>
    /// Get patient appointment history.
    /// </summary>
    public static string GetPatientAppointmentHistory =>
        """
            Appointment
            |> join(Provider, ProviderId, ProviderId)
            |> join(ExamRoom, RoomId, RoomId)
            |> select(
                AppointmentId, AppointmentDate, StartTime, EndTime,
                AppointmentType, Status, ReasonForVisit, ChiefComplaint,
                FullName, PrimarySpecialty, RoomNumber
            )
            |> where(PatientId = @patientId)
            |> orderByDesc(AppointmentDate)
            |> take(@limit)
            """;

    /// <summary>
    /// Get room utilization for a date range.
    /// </summary>
    public static string GetRoomUtilization =>
        """
            Appointment
            |> join(ExamRoom, RoomId, RoomId)
            |> select(RoomId, RoomNumber, RoomType, AppointmentDate, count() as AppointmentCount)
            |> where(AppointmentDate >= @startDate)
            |> where(AppointmentDate <= @endDate)
            |> where(Status != 'Cancelled')
            |> groupBy(RoomId, RoomNumber, RoomType, AppointmentDate)
            |> orderBy(AppointmentDate)
            |> orderBy(RoomNumber)
            """;

    /// <summary>
    /// Check for scheduling conflicts.
    /// </summary>
    public static string CheckSchedulingConflicts =>
        """
            Appointment
            |> select(AppointmentId, StartTime, EndTime, Status)
            |> where(ProviderId = @providerId)
            |> where(AppointmentDate = @date)
            |> where(Status != 'Cancelled')
            |> where(StartTime < @proposedEnd)
            |> where(EndTime > @proposedStart)
            """;

    /// <summary>
    /// Get provider time off for date range.
    /// </summary>
    public static string GetProviderTimeOff =>
        """
            ProviderTimeOff
            |> join(Provider, ProviderId, ProviderId)
            |> select(TimeOffId, ProviderId, FullName, StartDate, EndDate, Reason, IsApproved)
            |> where(StartDate <= @endDate)
            |> where(EndDate >= @startDate)
            |> where(IsApproved = 1)
            |> orderBy(StartDate)
            """;
}
