using System.Collections.Generic;

namespace Dashboard.Tests.TestData
{
    /// <summary>
    /// Mock data for dashboard tests.
    /// </summary>
    public static class MockData
    {
        /// <summary>
        /// Sample patients for testing.
        /// </summary>
        public static readonly object[] Patients = new object[]
        {
            new
            {
                id = "patient-001",
                identifier = "PAT-0001",
                given_name = "John",
                family_name = "Smith",
                birth_date = "1985-03-15",
                gender = "male",
                active = true,
                email = "john.smith@email.com",
                phone = "555-0101",
            },
            new
            {
                id = "patient-002",
                identifier = "PAT-0002",
                given_name = "Jane",
                family_name = "Doe",
                birth_date = "1990-07-22",
                gender = "female",
                active = true,
                email = "jane.doe@email.com",
                phone = "555-0102",
            },
            new
            {
                id = "patient-003",
                identifier = "PAT-0003",
                given_name = "Robert",
                family_name = "Johnson",
                birth_date = "1978-11-08",
                gender = "male",
                active = false,
                email = "robert.j@email.com",
                phone = "555-0103",
            },
            new
            {
                id = "patient-004",
                identifier = "PAT-0004",
                given_name = "Emily",
                family_name = "Wilson",
                birth_date = "1995-01-30",
                gender = "female",
                active = true,
                email = "emily.w@email.com",
                phone = "555-0104",
            },
            new
            {
                id = "patient-005",
                identifier = "PAT-0005",
                given_name = "Michael",
                family_name = "Brown",
                birth_date = "1982-09-12",
                gender = "male",
                active = true,
                email = "michael.b@email.com",
                phone = "555-0105",
            },
        };

        /// <summary>
        /// Sample practitioners for testing.
        /// </summary>
        public static readonly object[] Practitioners = new object[]
        {
            new
            {
                id = "pract-001",
                identifier = "DR-0001",
                given_name = "Sarah",
                family_name = "Williams",
                specialty = "Cardiology",
                qualification = "MD, FACC",
                active = true,
                email = "dr.williams@hospital.com",
                phone = "555-1001",
            },
            new
            {
                id = "pract-002",
                identifier = "DR-0002",
                given_name = "James",
                family_name = "Anderson",
                specialty = "Neurology",
                qualification = "MD, PhD",
                active = true,
                email = "dr.anderson@hospital.com",
                phone = "555-1002",
            },
            new
            {
                id = "pract-003",
                identifier = "DR-0003",
                given_name = "Maria",
                family_name = "Garcia",
                specialty = "Pediatrics",
                qualification = "MD, FAAP",
                active = true,
                email = "dr.garcia@hospital.com",
                phone = "555-1003",
            },
            new
            {
                id = "pract-004",
                identifier = "DR-0004",
                given_name = "David",
                family_name = "Lee",
                specialty = "Internal Medicine",
                qualification = "MD",
                active = false,
                email = "dr.lee@hospital.com",
                phone = "555-1004",
            },
        };

        /// <summary>
        /// Sample appointments for testing.
        /// </summary>
        public static readonly object[] Appointments = new object[]
        {
            new
            {
                id = "appt-001",
                status = "booked",
                start_time = "2024-12-20T09:00:00Z",
                end_time = "2024-12-20T09:30:00Z",
                minutes_duration = 30,
                patient_id = "patient-001",
                patient_name = "John Smith",
                practitioner_id = "pract-001",
                practitioner_name = "Dr. Sarah Williams",
                service_type = "Follow-up",
                priority = "routine",
                description = "Cardiac checkup",
            },
            new
            {
                id = "appt-002",
                status = "fulfilled",
                start_time = "2024-12-19T14:00:00Z",
                end_time = "2024-12-19T14:45:00Z",
                minutes_duration = 45,
                patient_id = "patient-002",
                patient_name = "Jane Doe",
                practitioner_id = "pract-002",
                practitioner_name = "Dr. James Anderson",
                service_type = "Consultation",
                priority = "routine",
                description = "Headache evaluation",
            },
            new
            {
                id = "appt-003",
                status = "cancelled",
                start_time = "2024-12-18T10:00:00Z",
                end_time = "2024-12-18T10:30:00Z",
                minutes_duration = 30,
                patient_id = "patient-003",
                patient_name = "Robert Johnson",
                practitioner_id = "pract-003",
                practitioner_name = "Dr. Maria Garcia",
                service_type = "Annual Physical",
                priority = "routine",
                description = "Cancelled by patient",
            },
            new
            {
                id = "appt-004",
                status = "booked",
                start_time = "2024-12-21T11:00:00Z",
                end_time = "2024-12-21T11:30:00Z",
                minutes_duration = 30,
                patient_id = "patient-004",
                patient_name = "Emily Wilson",
                practitioner_id = "pract-001",
                practitioner_name = "Dr. Sarah Williams",
                service_type = "New Patient",
                priority = "urgent",
                description = "Chest pain evaluation",
            },
            new
            {
                id = "appt-005",
                status = "arrived",
                start_time = "2024-12-20T08:00:00Z",
                end_time = "2024-12-20T08:30:00Z",
                minutes_duration = 30,
                patient_id = "patient-005",
                patient_name = "Michael Brown",
                practitioner_id = "pract-002",
                practitioner_name = "Dr. James Anderson",
                service_type = "Follow-up",
                priority = "routine",
                description = "Post-treatment review",
            },
        };

        /// <summary>
        /// Gets mock responses for all API endpoints.
        /// </summary>
        public static Dictionary<string, object> GetApiResponses() =>
            new Dictionary<string, object>
            {
                { "/fhir/Patient", Patients },
                { "/fhir/Patient/_search", Patients },
                { "/Practitioner", Practitioners },
                { "/Appointment", Appointments },
            };

        /// <summary>
        /// Gets filtered patients by name.
        /// </summary>
        public static object[] FilterPatientsByName(string query)
        {
            var results = new List<object>();
            var q = query.ToLower();

            foreach (dynamic patient in Patients)
            {
                var given = ((string)patient.given_name).ToLower();
                var family = ((string)patient.family_name).ToLower();
                if (given.Contains(q) || family.Contains(q))
                {
                    results.Add(patient);
                }
            }

            return results.ToArray();
        }

        /// <summary>
        /// Gets filtered appointments by status.
        /// </summary>
        public static object[] FilterAppointmentsByStatus(string status)
        {
            var results = new List<object>();

            foreach (dynamic appt in Appointments)
            {
                if ((string)appt.status == status)
                {
                    results.Add(appt);
                }
            }

            return results.ToArray();
        }
    }
}
