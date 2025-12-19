-- Clinical.Api Schema - FHIR R4 Compliant
-- All FHIR tables use fhir_ prefix per Clinical domain requirements
-- See: https://hl7.org/fhir/R4/

-- FHIR Patient Resource (https://hl7.org/fhir/R4/patient.html)
CREATE TABLE IF NOT EXISTS fhir_Patient (
    Id TEXT PRIMARY KEY,
    Active INTEGER NOT NULL DEFAULT 1,
    GivenName TEXT NOT NULL,
    FamilyName TEXT NOT NULL,
    BirthDate TEXT,
    Gender TEXT CHECK (Gender IN ('male', 'female', 'other', 'unknown')),
    Phone TEXT,
    Email TEXT,
    AddressLine TEXT,
    City TEXT,
    State TEXT,
    PostalCode TEXT,
    Country TEXT,
    LastUpdated TEXT NOT NULL DEFAULT (datetime('now')),
    VersionId INTEGER NOT NULL DEFAULT 1
);

-- FHIR Encounter Resource (https://hl7.org/fhir/R4/encounter.html)
CREATE TABLE IF NOT EXISTS fhir_Encounter (
    Id TEXT PRIMARY KEY,
    Status TEXT NOT NULL CHECK (Status IN ('planned', 'arrived', 'triaged', 'in-progress', 'onleave', 'finished', 'cancelled', 'entered-in-error')),
    Class TEXT NOT NULL CHECK (Class IN ('ambulatory', 'emergency', 'inpatient', 'observation', 'virtual')),
    PatientId TEXT NOT NULL,
    PractitionerId TEXT,
    ServiceType TEXT,
    ReasonCode TEXT,
    PeriodStart TEXT NOT NULL,
    PeriodEnd TEXT,
    Notes TEXT,
    LastUpdated TEXT NOT NULL DEFAULT (datetime('now')),
    VersionId INTEGER NOT NULL DEFAULT 1,
    FOREIGN KEY (PatientId) REFERENCES fhir_Patient(Id)
);

-- FHIR Condition Resource (https://hl7.org/fhir/R4/condition.html)
CREATE TABLE IF NOT EXISTS fhir_Condition (
    Id TEXT PRIMARY KEY,
    ClinicalStatus TEXT NOT NULL CHECK (ClinicalStatus IN ('active', 'recurrence', 'relapse', 'inactive', 'remission', 'resolved')),
    VerificationStatus TEXT CHECK (VerificationStatus IN ('unconfirmed', 'provisional', 'differential', 'confirmed', 'refuted', 'entered-in-error')),
    Category TEXT DEFAULT 'problem-list-item',
    Severity TEXT CHECK (Severity IN ('mild', 'moderate', 'severe')),
    CodeSystem TEXT NOT NULL DEFAULT 'http://hl7.org/fhir/sid/icd-10-cm',
    CodeValue TEXT NOT NULL,
    CodeDisplay TEXT NOT NULL,
    SubjectReference TEXT NOT NULL,
    EncounterReference TEXT,
    OnsetDateTime TEXT,
    RecordedDate TEXT NOT NULL DEFAULT (date('now')),
    RecorderReference TEXT,
    NoteText TEXT,
    LastUpdated TEXT NOT NULL DEFAULT (datetime('now')),
    VersionId INTEGER NOT NULL DEFAULT 1,
    FOREIGN KEY (SubjectReference) REFERENCES fhir_Patient(Id)
);

-- FHIR MedicationRequest Resource (https://hl7.org/fhir/R4/medicationrequest.html)
CREATE TABLE IF NOT EXISTS fhir_MedicationRequest (
    Id TEXT PRIMARY KEY,
    Status TEXT NOT NULL CHECK (Status IN ('active', 'on-hold', 'cancelled', 'completed', 'entered-in-error', 'stopped', 'draft')),
    Intent TEXT NOT NULL CHECK (Intent IN ('proposal', 'plan', 'order', 'original-order', 'reflex-order', 'filler-order', 'instance-order', 'option')),
    PatientId TEXT NOT NULL,
    PractitionerId TEXT NOT NULL,
    EncounterId TEXT,
    MedicationCode TEXT NOT NULL,
    MedicationDisplay TEXT NOT NULL,
    DosageInstruction TEXT,
    Quantity REAL,
    Unit TEXT,
    Refills INTEGER NOT NULL DEFAULT 0,
    AuthoredOn TEXT NOT NULL DEFAULT (datetime('now')),
    LastUpdated TEXT NOT NULL DEFAULT (datetime('now')),
    VersionId INTEGER NOT NULL DEFAULT 1,
    FOREIGN KEY (PatientId) REFERENCES fhir_Patient(Id),
    FOREIGN KEY (EncounterId) REFERENCES fhir_Encounter(Id)
);

-- Sync table: Mapped provider data from Scheduling domain
-- This is NOT a FHIR table, it's a synced/mapped copy from Scheduling.fhir_Practitioner
CREATE TABLE IF NOT EXISTS sync_Provider (
    ProviderId TEXT PRIMARY KEY,
    FirstName TEXT NOT NULL,
    LastName TEXT NOT NULL,
    Specialty TEXT,
    SyncedAt TEXT NOT NULL DEFAULT (datetime('now'))
);

-- Indexes for FHIR resource queries
CREATE INDEX IF NOT EXISTS idx_fhir_patient_family ON fhir_Patient(FamilyName);
CREATE INDEX IF NOT EXISTS idx_fhir_patient_given ON fhir_Patient(GivenName);
CREATE INDEX IF NOT EXISTS idx_fhir_patient_birthdate ON fhir_Patient(BirthDate);
CREATE INDEX IF NOT EXISTS idx_fhir_encounter_patient ON fhir_Encounter(PatientId);
CREATE INDEX IF NOT EXISTS idx_fhir_encounter_status ON fhir_Encounter(Status);
CREATE INDEX IF NOT EXISTS idx_fhir_encounter_period ON fhir_Encounter(PeriodStart);
CREATE INDEX IF NOT EXISTS idx_fhir_condition_patient ON fhir_Condition(SubjectReference);
CREATE INDEX IF NOT EXISTS idx_fhir_condition_code ON fhir_Condition(CodeValue);
CREATE INDEX IF NOT EXISTS idx_fhir_condition_clinical ON fhir_Condition(ClinicalStatus);
CREATE INDEX IF NOT EXISTS idx_fhir_medication_patient ON fhir_MedicationRequest(PatientId);
CREATE INDEX IF NOT EXISTS idx_fhir_medication_status ON fhir_MedicationRequest(Status);
CREATE INDEX IF NOT EXISTS idx_sync_provider_specialty ON sync_Provider(Specialty);
