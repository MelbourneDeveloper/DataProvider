using H5;

namespace Dashboard.Models
{
    /// <summary>
    /// FHIR Patient resource model.
    /// </summary>
    [External]
    [Name("Object")]
    public class Patient
    {
        /// <summary>Patient unique identifier.</summary>
        public extern string Id { get; set; }

        /// <summary>Whether patient record is active.</summary>
        public extern bool Active { get; set; }

        /// <summary>Patient's given name.</summary>
        public extern string GivenName { get; set; }

        /// <summary>Patient's family name.</summary>
        public extern string FamilyName { get; set; }

        /// <summary>Patient's birth date.</summary>
        public extern string BirthDate { get; set; }

        /// <summary>Patient's gender.</summary>
        public extern string Gender { get; set; }

        /// <summary>Patient's phone number.</summary>
        public extern string Phone { get; set; }

        /// <summary>Patient's email address.</summary>
        public extern string Email { get; set; }

        /// <summary>Patient's address line.</summary>
        public extern string AddressLine { get; set; }

        /// <summary>Patient's city.</summary>
        public extern string City { get; set; }

        /// <summary>Patient's state.</summary>
        public extern string State { get; set; }

        /// <summary>Patient's postal code.</summary>
        public extern string PostalCode { get; set; }

        /// <summary>Patient's country.</summary>
        public extern string Country { get; set; }

        /// <summary>Last updated timestamp.</summary>
        public extern string LastUpdated { get; set; }

        /// <summary>Version identifier.</summary>
        public extern long VersionId { get; set; }
    }

    /// <summary>
    /// FHIR Encounter resource model.
    /// </summary>
    [External]
    [Name("Object")]
    public class Encounter
    {
        /// <summary>Encounter unique identifier.</summary>
        public extern string Id { get; set; }

        /// <summary>Encounter status.</summary>
        public extern string Status { get; set; }

        /// <summary>Encounter class.</summary>
        public extern string Class { get; set; }

        /// <summary>Patient reference.</summary>
        public extern string PatientId { get; set; }

        /// <summary>Practitioner reference.</summary>
        public extern string PractitionerId { get; set; }

        /// <summary>Service type.</summary>
        public extern string ServiceType { get; set; }

        /// <summary>Reason code.</summary>
        public extern string ReasonCode { get; set; }

        /// <summary>Period start.</summary>
        public extern string PeriodStart { get; set; }

        /// <summary>Period end.</summary>
        public extern string PeriodEnd { get; set; }

        /// <summary>Notes.</summary>
        public extern string Notes { get; set; }

        /// <summary>Last updated timestamp.</summary>
        public extern string LastUpdated { get; set; }

        /// <summary>Version identifier.</summary>
        public extern long VersionId { get; set; }
    }

    /// <summary>
    /// FHIR Condition resource model.
    /// </summary>
    [External]
    [Name("Object")]
    public class Condition
    {
        /// <summary>Condition unique identifier.</summary>
        public extern string Id { get; set; }

        /// <summary>Clinical status.</summary>
        public extern string ClinicalStatus { get; set; }

        /// <summary>Verification status.</summary>
        public extern string VerificationStatus { get; set; }

        /// <summary>Condition category.</summary>
        public extern string Category { get; set; }

        /// <summary>Severity.</summary>
        public extern string Severity { get; set; }

        /// <summary>ICD-10 code value.</summary>
        public extern string CodeValue { get; set; }

        /// <summary>Code display name.</summary>
        public extern string CodeDisplay { get; set; }

        /// <summary>Subject reference (patient).</summary>
        public extern string SubjectReference { get; set; }

        /// <summary>Onset date/time.</summary>
        public extern string OnsetDateTime { get; set; }

        /// <summary>Recorded date.</summary>
        public extern string RecordedDate { get; set; }

        /// <summary>Note text.</summary>
        public extern string NoteText { get; set; }
    }

    /// <summary>
    /// FHIR MedicationRequest resource model.
    /// </summary>
    [External]
    [Name("Object")]
    public class MedicationRequest
    {
        /// <summary>MedicationRequest unique identifier.</summary>
        public extern string Id { get; set; }

        /// <summary>Status.</summary>
        public extern string Status { get; set; }

        /// <summary>Intent.</summary>
        public extern string Intent { get; set; }

        /// <summary>Patient reference.</summary>
        public extern string PatientId { get; set; }

        /// <summary>Practitioner reference.</summary>
        public extern string PractitionerId { get; set; }

        /// <summary>Medication code (RxNorm).</summary>
        public extern string MedicationCode { get; set; }

        /// <summary>Medication display name.</summary>
        public extern string MedicationDisplay { get; set; }

        /// <summary>Dosage instruction.</summary>
        public extern string DosageInstruction { get; set; }

        /// <summary>Quantity.</summary>
        public extern double Quantity { get; set; }

        /// <summary>Unit.</summary>
        public extern string Unit { get; set; }

        /// <summary>Number of refills.</summary>
        public extern int Refills { get; set; }

        /// <summary>Authored on date.</summary>
        public extern string AuthoredOn { get; set; }
    }
}
