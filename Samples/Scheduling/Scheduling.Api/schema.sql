-- Scheduling.Api Schema
-- FHIR R4 compliant scheduling resources
-- See: https://build.fhir.org/resourcelist.html

-- FHIR Practitioner - Healthcare providers
CREATE TABLE IF NOT EXISTS fhir_Practitioner (
    Id TEXT PRIMARY KEY,
    Identifier TEXT NOT NULL,
    Active INTEGER NOT NULL DEFAULT 1,
    NameFamily TEXT NOT NULL,
    NameGiven TEXT NOT NULL,
    Qualification TEXT,
    Specialty TEXT,
    TelecomEmail TEXT,
    TelecomPhone TEXT
);

-- FHIR Schedule - Practitioner availability
CREATE TABLE IF NOT EXISTS fhir_Schedule (
    Id TEXT PRIMARY KEY,
    Active INTEGER NOT NULL DEFAULT 1,
    PractitionerReference TEXT NOT NULL,
    PlanningHorizon INTEGER NOT NULL DEFAULT 30,
    Comment TEXT,
    FOREIGN KEY (PractitionerReference) REFERENCES fhir_Practitioner(Id)
);

-- FHIR Slot - Available time slots for booking
CREATE TABLE IF NOT EXISTS fhir_Slot (
    Id TEXT PRIMARY KEY,
    ScheduleReference TEXT NOT NULL,
    Status TEXT NOT NULL CHECK (Status IN ('free', 'busy', 'busy-unavailable', 'busy-tentative')),
    StartTime TEXT NOT NULL,
    EndTime TEXT NOT NULL,
    Overbooked INTEGER NOT NULL DEFAULT 0,
    Comment TEXT,
    FOREIGN KEY (ScheduleReference) REFERENCES fhir_Schedule(Id)
);

-- FHIR Appointment - Scheduled healthcare encounters
-- Note: References are stored as FHIR-style strings (e.g., "Practitioner/abc123")
-- No FK constraint as references may point to external systems
CREATE TABLE IF NOT EXISTS fhir_Appointment (
    Id TEXT PRIMARY KEY,
    Status TEXT NOT NULL CHECK (Status IN ('proposed', 'pending', 'booked', 'arrived', 'fulfilled', 'cancelled', 'noshow', 'entered-in-error', 'checked-in', 'waitlist')),
    ServiceCategory TEXT,
    ServiceType TEXT,
    ReasonCode TEXT,
    Priority TEXT NOT NULL CHECK (Priority IN ('routine', 'urgent', 'asap', 'stat')),
    Description TEXT,
    StartTime TEXT NOT NULL,
    EndTime TEXT NOT NULL,
    MinutesDuration INTEGER NOT NULL,
    PatientReference TEXT NOT NULL,
    PractitionerReference TEXT NOT NULL,
    Created TEXT NOT NULL,
    Comment TEXT
);

-- Synced patient data (mapped from Clinical domain)
-- This is NOT the source of truth - Clinical domain owns fhir_Patient
CREATE TABLE IF NOT EXISTS sync_ScheduledPatient (
    PatientId TEXT PRIMARY KEY,
    DisplayName TEXT NOT NULL,
    ContactPhone TEXT,
    ContactEmail TEXT,
    SyncedAt TEXT NOT NULL DEFAULT (datetime('now'))
);

-- Indexes for FHIR resources
CREATE INDEX IF NOT EXISTS idx_practitioner_identifier ON fhir_Practitioner(Identifier);
CREATE INDEX IF NOT EXISTS idx_practitioner_specialty ON fhir_Practitioner(Specialty);
CREATE INDEX IF NOT EXISTS idx_practitioner_active ON fhir_Practitioner(Active);

CREATE INDEX IF NOT EXISTS idx_schedule_practitioner ON fhir_Schedule(PractitionerReference);
CREATE INDEX IF NOT EXISTS idx_schedule_active ON fhir_Schedule(Active);

CREATE INDEX IF NOT EXISTS idx_slot_schedule ON fhir_Slot(ScheduleReference);
CREATE INDEX IF NOT EXISTS idx_slot_status ON fhir_Slot(Status);
CREATE INDEX IF NOT EXISTS idx_slot_start ON fhir_Slot(StartTime);

CREATE INDEX IF NOT EXISTS idx_appointment_status ON fhir_Appointment(Status);
CREATE INDEX IF NOT EXISTS idx_appointment_start ON fhir_Appointment(StartTime);
CREATE INDEX IF NOT EXISTS idx_appointment_patient ON fhir_Appointment(PatientReference);
CREATE INDEX IF NOT EXISTS idx_appointment_practitioner ON fhir_Appointment(PractitionerReference);
CREATE INDEX IF NOT EXISTS idx_appointment_created ON fhir_Appointment(Created);

CREATE INDEX IF NOT EXISTS idx_sync_patient_id ON sync_ScheduledPatient(PatientId);
