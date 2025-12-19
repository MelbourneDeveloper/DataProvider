-- Healthcare Clinic Database Schema (FHIR-aligned)
-- This microservice uses SQLite with DataProvider SQL files for data access
-- Resource names and fields aligned with FHIR R4 standard

-- Patient resource - FHIR Patient (https://hl7.org/fhir/patient.html)
-- Syncs to Insurance.Member with column mapping
CREATE TABLE IF NOT EXISTS Patient (
    Id TEXT PRIMARY KEY,                    -- FHIR Resource ID (UUID)
    Identifier TEXT NOT NULL UNIQUE,        -- FHIR identifier.value (MRN)
    Active INTEGER NOT NULL DEFAULT 1,      -- FHIR active
    FamilyName TEXT NOT NULL,               -- FHIR name.family
    GivenName TEXT NOT NULL,                -- FHIR name.given
    BirthDate TEXT NOT NULL,                -- FHIR birthDate (YYYY-MM-DD)
    Gender TEXT NOT NULL CHECK (Gender IN ('male', 'female', 'other', 'unknown')), -- FHIR gender
    TelecomEmail TEXT NULL,                 -- FHIR telecom (email)
    TelecomPhone TEXT NULL,                 -- FHIR telecom (phone)
    AddressLine TEXT NULL,                  -- FHIR address.line
    AddressCity TEXT NULL,                  -- FHIR address.city
    AddressState TEXT NULL,                 -- FHIR address.state
    AddressPostalCode TEXT NULL,            -- FHIR address.postalCode
    AddressCountry TEXT NULL,               -- FHIR address.country
    GeneralPractitionerId TEXT NULL,        -- FHIR generalPractitioner reference
    ManagingOrganizationId TEXT NULL,       -- FHIR managingOrganization reference
    -- Extension: Insurance info (common extension pattern)
    ExtInsurancePolicyNumber TEXT NULL,
    ExtInsuranceGroupNumber TEXT NULL,
    ExtInsurancePayerId TEXT NULL,
    -- Contact for emergencies
    ContactName TEXT NULL,
    ContactPhone TEXT NULL,
    ContactRelationship TEXT NULL,
    -- Metadata
    LastUpdated TEXT NOT NULL,              -- FHIR meta.lastUpdated
    VersionId INTEGER NOT NULL DEFAULT 1    -- FHIR meta.versionId
);

-- Encounter resource - FHIR Encounter (https://hl7.org/fhir/encounter.html)
-- Syncs to Insurance.Claim with LQL transforms for codes/dates
CREATE TABLE IF NOT EXISTS Encounter (
    Id TEXT PRIMARY KEY,                    -- FHIR Resource ID (UUID)
    Identifier TEXT NULL,                   -- FHIR identifier.value
    Status TEXT NOT NULL CHECK (Status IN ('planned', 'arrived', 'triaged', 'in-progress', 'onleave', 'finished', 'cancelled')), -- FHIR status
    Class TEXT NOT NULL CHECK (Class IN ('AMB', 'EMER', 'FLD', 'HH', 'IMP', 'ACUTE', 'NONAC', 'OBSENC', 'PRENC', 'SS', 'VR')), -- FHIR class code
    Type TEXT NOT NULL,                     -- FHIR type[0].coding[0].code (CPT/HCPCS)
    TypeDisplay TEXT NOT NULL,              -- FHIR type[0].coding[0].display
    ServiceType TEXT NULL,                  -- FHIR serviceType
    Priority TEXT NULL CHECK (Priority IN ('A', 'EM', 'R', 'UR')), -- FHIR priority (asap, emergency, routine, urgent)
    SubjectId TEXT NOT NULL,                -- FHIR subject reference (Patient)
    ParticipantId TEXT NOT NULL,            -- FHIR participant[0].individual (Practitioner)
    ParticipantName TEXT NOT NULL,          -- Denormalized for convenience
    PeriodStart TEXT NOT NULL,              -- FHIR period.start (ISO 8601)
    PeriodEnd TEXT NULL,                    -- FHIR period.end
    ReasonCode TEXT NULL,                   -- FHIR reasonCode[0].coding[0].code (ICD-10)
    ReasonDisplay TEXT NULL,                -- FHIR reasonCode[0].coding[0].display
    DiagnosisCode TEXT NULL,                -- FHIR diagnosis[0].condition.code (ICD-10)
    DiagnosisDisplay TEXT NULL,             -- FHIR diagnosis[0].condition.display
    ServiceProviderId TEXT NOT NULL,        -- FHIR serviceProvider reference
    ServiceProviderName TEXT NOT NULL,      -- Denormalized
    -- Extension: Billing/claims
    ExtTotalCharge REAL NOT NULL,           -- Total charges for this encounter
    ExtClaimStatus TEXT NOT NULL DEFAULT 'draft' CHECK (ExtClaimStatus IN ('draft', 'submitted', 'approved', 'denied', 'paid')),
    ExtClaimSubmittedDate TEXT NULL,
    -- Metadata
    LastUpdated TEXT NOT NULL,
    VersionId INTEGER NOT NULL DEFAULT 1,
    FOREIGN KEY (SubjectId) REFERENCES Patient (Id)
);

-- MedicationRequest resource - FHIR MedicationRequest
CREATE TABLE IF NOT EXISTS MedicationRequest (
    Id TEXT PRIMARY KEY,
    Identifier TEXT NULL,
    Status TEXT NOT NULL CHECK (Status IN ('active', 'on-hold', 'cancelled', 'completed', 'entered-in-error', 'stopped', 'draft', 'unknown')),
    Intent TEXT NOT NULL CHECK (Intent IN ('proposal', 'plan', 'order', 'original-order', 'reflex-order', 'filler-order', 'instance-order', 'option')),
    MedicationCode TEXT NOT NULL,           -- FHIR medication.coding.code (RxNorm)
    MedicationDisplay TEXT NOT NULL,        -- FHIR medication.coding.display
    SubjectId TEXT NOT NULL,                -- FHIR subject (Patient)
    EncounterId TEXT NOT NULL,              -- FHIR encounter reference
    AuthoredOn TEXT NOT NULL,               -- FHIR authoredOn
    RequesterId TEXT NOT NULL,              -- FHIR requester (Practitioner)
    RequesterName TEXT NOT NULL,
    DosageText TEXT NOT NULL,               -- FHIR dosageInstruction.text
    DosageRoute TEXT NULL,                  -- FHIR dosageInstruction.route
    DosageFrequency TEXT NULL,              -- FHIR dosageInstruction.timing
    DispenseQuantity REAL NULL,             -- FHIR dispenseRequest.quantity
    DispenseUnit TEXT NULL,
    NumberOfRepeatsAllowed INTEGER DEFAULT 0, -- FHIR dispenseRequest.numberOfRepeatsAllowed
    ValidityPeriodStart TEXT NULL,
    ValidityPeriodEnd TEXT NULL,
    SubstitutionAllowed INTEGER DEFAULT 1,
    Note TEXT NULL,
    LastUpdated TEXT NOT NULL,
    VersionId INTEGER NOT NULL DEFAULT 1,
    FOREIGN KEY (SubjectId) REFERENCES Patient (Id),
    FOREIGN KEY (EncounterId) REFERENCES Encounter (Id)
);

-- Practitioner resource - FHIR Practitioner
CREATE TABLE IF NOT EXISTS Practitioner (
    Id TEXT PRIMARY KEY,
    Identifier TEXT NOT NULL UNIQUE,        -- NPI Number
    Active INTEGER NOT NULL DEFAULT 1,
    FamilyName TEXT NOT NULL,
    GivenName TEXT NOT NULL,
    TelecomEmail TEXT NULL,
    TelecomPhone TEXT NULL,
    QualificationCode TEXT NOT NULL,        -- Specialty code
    QualificationDisplay TEXT NOT NULL,     -- Specialty display
    QualificationIssuer TEXT NOT NULL,      -- License issuing state
    QualificationIdentifier TEXT NOT NULL,  -- License number
    LastUpdated TEXT NOT NULL,
    VersionId INTEGER NOT NULL DEFAULT 1
);

-- Organization resource - FHIR Organization (for facilities)
CREATE TABLE IF NOT EXISTS Organization (
    Id TEXT PRIMARY KEY,
    Identifier TEXT NOT NULL UNIQUE,        -- Facility code
    Active INTEGER NOT NULL DEFAULT 1,
    Type TEXT NOT NULL,                     -- hospital, clinic, pharmacy, etc.
    Name TEXT NOT NULL,
    TelecomPhone TEXT NULL,
    AddressLine TEXT NULL,
    AddressCity TEXT NULL,
    AddressState TEXT NULL,
    AddressPostalCode TEXT NULL,
    LastUpdated TEXT NOT NULL,
    VersionId INTEGER NOT NULL DEFAULT 1
);

-- Create indexes for common queries and sync operations
CREATE INDEX IF NOT EXISTS idx_patient_identifier ON Patient (Identifier);
CREATE INDEX IF NOT EXISTS idx_patient_insurance ON Patient (ExtInsurancePolicyNumber);
CREATE INDEX IF NOT EXISTS idx_patient_name ON Patient (FamilyName, GivenName);
CREATE INDEX IF NOT EXISTS idx_encounter_subject ON Encounter (SubjectId);
CREATE INDEX IF NOT EXISTS idx_encounter_period ON Encounter (PeriodStart);
CREATE INDEX IF NOT EXISTS idx_encounter_claim_status ON Encounter (ExtClaimStatus);
CREATE INDEX IF NOT EXISTS idx_encounter_participant ON Encounter (ParticipantId);
CREATE INDEX IF NOT EXISTS idx_medication_subject ON MedicationRequest (SubjectId);
CREATE INDEX IF NOT EXISTS idx_medication_encounter ON MedicationRequest (EncounterId);
CREATE INDEX IF NOT EXISTS idx_practitioner_identifier ON Practitioner (Identifier);
