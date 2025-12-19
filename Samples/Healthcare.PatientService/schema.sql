-- FHIR-compliant healthcare schema for Clinic.Api
-- Uses FHIR resource naming conventions

-- Patient resource (FHIR R4)
CREATE TABLE IF NOT EXISTS Patient (
    Id TEXT PRIMARY KEY,
    Active INTEGER NOT NULL DEFAULT 1,
    GivenName TEXT NOT NULL,
    FamilyName TEXT NOT NULL,
    BirthDate TEXT NULL,
    Gender TEXT NULL CHECK (Gender IN ('male', 'female', 'other', 'unknown')),
    Phone TEXT NULL,
    Email TEXT NULL,
    AddressLine TEXT NULL,
    City TEXT NULL,
    State TEXT NULL,
    PostalCode TEXT NULL,
    Country TEXT NULL,
    LastUpdated TEXT NOT NULL,
    VersionId INTEGER NOT NULL DEFAULT 1
);

-- Practitioner resource (FHIR R4)
CREATE TABLE IF NOT EXISTS Practitioner (
    Id TEXT PRIMARY KEY,
    Active INTEGER NOT NULL DEFAULT 1,
    GivenName TEXT NOT NULL,
    FamilyName TEXT NOT NULL,
    Qualification TEXT NULL,
    Specialty TEXT NULL,
    Phone TEXT NULL,
    Email TEXT NULL,
    LastUpdated TEXT NOT NULL,
    VersionId INTEGER NOT NULL DEFAULT 1
);

-- Encounter resource (FHIR R4) - represents patient visits
CREATE TABLE IF NOT EXISTS Encounter (
    Id TEXT PRIMARY KEY,
    Status TEXT NOT NULL CHECK (Status IN ('planned', 'arrived', 'triaged', 'in-progress', 'onleave', 'finished', 'cancelled', 'entered-in-error')),
    Class TEXT NOT NULL CHECK (Class IN ('ambulatory', 'emergency', 'inpatient', 'observation', 'virtual')),
    PatientId TEXT NOT NULL,
    PractitionerId TEXT NULL,
    ServiceType TEXT NULL,
    ReasonCode TEXT NULL,
    PeriodStart TEXT NOT NULL,
    PeriodEnd TEXT NULL,
    Notes TEXT NULL,
    LastUpdated TEXT NOT NULL,
    VersionId INTEGER NOT NULL DEFAULT 1,
    FOREIGN KEY (PatientId) REFERENCES Patient (Id),
    FOREIGN KEY (PractitionerId) REFERENCES Practitioner (Id)
);

-- MedicationRequest resource (FHIR R4) - prescriptions
CREATE TABLE IF NOT EXISTS MedicationRequest (
    Id TEXT PRIMARY KEY,
    Status TEXT NOT NULL CHECK (Status IN ('active', 'on-hold', 'cancelled', 'completed', 'entered-in-error', 'stopped', 'draft')),
    Intent TEXT NOT NULL CHECK (Intent IN ('proposal', 'plan', 'order', 'original-order', 'reflex-order', 'filler-order', 'instance-order', 'option')),
    PatientId TEXT NOT NULL,
    PractitionerId TEXT NOT NULL,
    EncounterId TEXT NULL,
    MedicationCode TEXT NOT NULL,
    MedicationDisplay TEXT NOT NULL,
    DosageInstruction TEXT NULL,
    Quantity REAL NULL,
    Unit TEXT NULL,
    Refills INTEGER NULL DEFAULT 0,
    AuthoredOn TEXT NOT NULL,
    LastUpdated TEXT NOT NULL,
    VersionId INTEGER NOT NULL DEFAULT 1,
    FOREIGN KEY (PatientId) REFERENCES Patient (Id),
    FOREIGN KEY (PractitionerId) REFERENCES Practitioner (Id),
    FOREIGN KEY (EncounterId) REFERENCES Encounter (Id)
);

-- Create indexes for common queries
CREATE INDEX IF NOT EXISTS IX_Patient_FamilyName ON Patient (FamilyName);
CREATE INDEX IF NOT EXISTS IX_Patient_BirthDate ON Patient (BirthDate);
CREATE INDEX IF NOT EXISTS IX_Encounter_PatientId ON Encounter (PatientId);
CREATE INDEX IF NOT EXISTS IX_Encounter_PractitionerId ON Encounter (PractitionerId);
CREATE INDEX IF NOT EXISTS IX_Encounter_Status ON Encounter (Status);
CREATE INDEX IF NOT EXISTS IX_MedicationRequest_PatientId ON MedicationRequest (PatientId);
CREATE INDEX IF NOT EXISTS IX_MedicationRequest_Status ON MedicationRequest (Status);

-- Clinical documentation table synced back into PatientService
CREATE TABLE IF NOT EXISTS MedicalRecord (
    Id TEXT PRIMARY KEY,
    PatientId TEXT NOT NULL,
    VisitDate TEXT NOT NULL,
    ChiefComplaint TEXT NOT NULL,
    Diagnosis TEXT NULL,
    Treatment TEXT NULL,
    Prescriptions TEXT NULL,
    Notes TEXT NULL,
    ProviderId TEXT NULL,
    CreatedAt TEXT NOT NULL,
    FOREIGN KEY (PatientId) REFERENCES Patient (Id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS IX_MedicalRecord_PatientId ON MedicalRecord (PatientId);
CREATE INDEX IF NOT EXISTS IX_MedicalRecord_VisitDate ON MedicalRecord (VisitDate);
