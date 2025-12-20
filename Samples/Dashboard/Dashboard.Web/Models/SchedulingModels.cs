namespace Dashboard.Models
{
    using H5;

    /// <summary>
    /// FHIR Practitioner resource model.
    /// </summary>
    [External]
    [Name("Object")]
    public class Practitioner
    {
        /// <summary>Practitioner unique identifier.</summary>
        public extern string Id { get; set; }

        /// <summary>Practitioner identifier (NPI).</summary>
        public extern string Identifier { get; set; }

        /// <summary>Whether practitioner is active.</summary>
        public extern bool Active { get; set; }

        /// <summary>Family name.</summary>
        public extern string NameFamily { get; set; }

        /// <summary>Given name.</summary>
        public extern string NameGiven { get; set; }

        /// <summary>Qualification.</summary>
        public extern string Qualification { get; set; }

        /// <summary>Specialty.</summary>
        public extern string Specialty { get; set; }

        /// <summary>Email.</summary>
        public extern string TelecomEmail { get; set; }

        /// <summary>Phone.</summary>
        public extern string TelecomPhone { get; set; }
    }

    /// <summary>
    /// FHIR Appointment resource model.
    /// </summary>
    [External]
    [Name("Object")]
    public class Appointment
    {
        /// <summary>Appointment unique identifier.</summary>
        public extern string Id { get; set; }

        /// <summary>Appointment status.</summary>
        public extern string Status { get; set; }

        /// <summary>Service category.</summary>
        public extern string ServiceCategory { get; set; }

        /// <summary>Service type.</summary>
        public extern string ServiceType { get; set; }

        /// <summary>Reason code.</summary>
        public extern string ReasonCode { get; set; }

        /// <summary>Priority.</summary>
        public extern string Priority { get; set; }

        /// <summary>Description.</summary>
        public extern string Description { get; set; }

        /// <summary>Start time.</summary>
        public extern string StartTime { get; set; }

        /// <summary>End time.</summary>
        public extern string EndTime { get; set; }

        /// <summary>Duration in minutes.</summary>
        public extern int MinutesDuration { get; set; }

        /// <summary>Patient reference.</summary>
        public extern string PatientReference { get; set; }

        /// <summary>Practitioner reference.</summary>
        public extern string PractitionerReference { get; set; }

        /// <summary>Created timestamp.</summary>
        public extern string Created { get; set; }

        /// <summary>Comment.</summary>
        public extern string Comment { get; set; }
    }

    /// <summary>
    /// FHIR Schedule resource model.
    /// </summary>
    [External]
    [Name("Object")]
    public class Schedule
    {
        /// <summary>Schedule unique identifier.</summary>
        public extern string Id { get; set; }

        /// <summary>Whether schedule is active.</summary>
        public extern bool Active { get; set; }

        /// <summary>Practitioner reference.</summary>
        public extern string PractitionerReference { get; set; }

        /// <summary>Planning horizon in days.</summary>
        public extern int PlanningHorizon { get; set; }

        /// <summary>Comment.</summary>
        public extern string Comment { get; set; }
    }

    /// <summary>
    /// FHIR Slot resource model.
    /// </summary>
    [External]
    [Name("Object")]
    public class Slot
    {
        /// <summary>Slot unique identifier.</summary>
        public extern string Id { get; set; }

        /// <summary>Schedule reference.</summary>
        public extern string ScheduleReference { get; set; }

        /// <summary>Slot status.</summary>
        public extern string Status { get; set; }

        /// <summary>Start time.</summary>
        public extern string StartTime { get; set; }

        /// <summary>End time.</summary>
        public extern string EndTime { get; set; }

        /// <summary>Whether overbooked.</summary>
        public extern bool Overbooked { get; set; }

        /// <summary>Comment.</summary>
        public extern string Comment { get; set; }
    }
}
