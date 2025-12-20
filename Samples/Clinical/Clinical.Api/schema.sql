-- Clinical.Api Schema - FHIR R4 Compliant
-- See: https://hl7.org/fhir/R4/

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

CREATE TABLE IF NOT EXISTS sync_Provider (
    ProviderId TEXT PRIMARY KEY,
    FirstName TEXT NOT NULL,
    LastName TEXT NOT NULL,
    Specialty TEXT,
    SyncedAt TEXT NOT NULL DEFAULT (datetime('now'))
);

CREATE INDEX IF NOT EXISTS idx_fhir_patient_family ON fhir_Patient(FamilyName);
CREATE INDEX IF NOT EXISTS idx_fhir_patient_given ON fhir_Patient(GivenName);
CREATE INDEX IF NOT EXISTS idx_fhir_encounter_patient ON fhir_Encounter(PatientId);
CREATE INDEX IF NOT EXISTS idx_fhir_condition_patient ON fhir_Condition(SubjectReference);
CREATE INDEX IF NOT EXISTS idx_fhir_medication_patient ON fhir_MedicationRequest(PatientId);
