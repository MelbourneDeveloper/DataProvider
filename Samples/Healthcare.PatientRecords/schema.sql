-- Healthcare.PatientRecords Schema - FHIR R4 Compliant
-- Based on FHIR R4 Patient, Practitioner, Condition, MedicationStatement, AllergyIntolerance, Observation resources
-- See: https://hl7.org/fhir/R4/

-- FHIR Patient Resource (https://hl7.org/fhir/R4/patient.html)
CREATE TABLE IF NOT EXISTS Patient (
    Id TEXT PRIMARY KEY,                          -- FHIR: Resource.id
    ResourceType TEXT NOT NULL DEFAULT 'Patient', -- FHIR: Resource.resourceType
    Active INTEGER NOT NULL DEFAULT 1,            -- FHIR: Patient.active

    -- FHIR: Patient.identifier[] - flattened for simplicity
    MrnSystem TEXT DEFAULT 'urn:oid:2.16.840.1.113883.4.1',
    MrnValue TEXT NOT NULL UNIQUE,                -- Medical Record Number
    SsnSystem TEXT DEFAULT 'http://hl7.org/fhir/sid/us-ssn',
    SsnValue TEXT,                                -- SSN (encrypted in production)

    -- FHIR: Patient.name[] - primary name
    NameFamily TEXT NOT NULL,                     -- FHIR: HumanName.family
    NameGiven TEXT NOT NULL,                      -- FHIR: HumanName.given (first name)
    NameMiddle TEXT,                              -- FHIR: HumanName.given (middle)
    NamePrefix TEXT,                              -- FHIR: HumanName.prefix (Dr., Mr., etc.)
    NameSuffix TEXT,                              -- FHIR: HumanName.suffix (Jr., III, etc.)

    -- FHIR: Patient.telecom[] - flattened
    TelecomPhone TEXT,                            -- FHIR: ContactPoint where system='phone'
    TelecomEmail TEXT,                            -- FHIR: ContactPoint where system='email'
    TelecomPhoneUse TEXT DEFAULT 'home',          -- FHIR: ContactPoint.use

    -- FHIR: Patient.gender (required binding)
    Gender TEXT NOT NULL CHECK (Gender IN ('male', 'female', 'other', 'unknown')),

    -- FHIR: Patient.birthDate
    BirthDate TEXT NOT NULL,                      -- FHIR: date format YYYY-MM-DD

    -- FHIR: Patient.deceased[x]
    DeceasedBoolean INTEGER DEFAULT 0,
    DeceasedDateTime TEXT,

    -- FHIR: Patient.address[] - primary address
    AddressLine1 TEXT,
    AddressLine2 TEXT,
    AddressCity TEXT,
    AddressState TEXT,
    AddressPostalCode TEXT,
    AddressCountry TEXT DEFAULT 'US',
    AddressUse TEXT DEFAULT 'home',               -- FHIR: Address.use

    -- FHIR: Patient.maritalStatus
    MaritalStatus TEXT CHECK (MaritalStatus IN ('A', 'D', 'I', 'L', 'M', 'P', 'S', 'T', 'U', 'W', NULL)),

    -- FHIR: Patient.communication[]
    CommunicationLanguage TEXT DEFAULT 'en',      -- FHIR: CodeableConcept
    CommunicationPreferred INTEGER DEFAULT 1,

    -- FHIR: Patient.generalPractitioner[] - reference to Practitioner
    GeneralPractitionerId TEXT,

    -- FHIR: Patient.managingOrganization
    ManagingOrganizationId TEXT,

    -- FHIR: Meta
    MetaVersionId TEXT DEFAULT '1',
    MetaLastUpdated TEXT NOT NULL DEFAULT (datetime('now')),
    MetaSource TEXT,

    FOREIGN KEY (GeneralPractitionerId) REFERENCES Practitioner(Id)
);

-- FHIR Practitioner Resource (https://hl7.org/fhir/R4/practitioner.html)
CREATE TABLE IF NOT EXISTS Practitioner (
    Id TEXT PRIMARY KEY,
    ResourceType TEXT NOT NULL DEFAULT 'Practitioner',
    Active INTEGER NOT NULL DEFAULT 1,

    -- FHIR: Practitioner.identifier[]
    NpiSystem TEXT DEFAULT 'http://hl7.org/fhir/sid/us-npi',
    NpiValue TEXT NOT NULL UNIQUE,                -- National Provider Identifier

    -- FHIR: Practitioner.name[]
    NameFamily TEXT NOT NULL,
    NameGiven TEXT NOT NULL,
    NamePrefix TEXT,                              -- Dr., etc.
    NameSuffix TEXT,                              -- MD, DO, NP, etc.

    -- FHIR: Practitioner.telecom[]
    TelecomPhone TEXT,
    TelecomEmail TEXT NOT NULL,

    -- FHIR: Practitioner.gender
    Gender TEXT CHECK (Gender IN ('male', 'female', 'other', 'unknown')),

    -- FHIR: Practitioner.qualification[] - primary specialty
    QualificationCode TEXT NOT NULL,              -- SNOMED CT specialty code
    QualificationDisplay TEXT NOT NULL,           -- Human readable specialty
    QualificationIssuer TEXT,                     -- Licensing board
    QualificationPeriodStart TEXT,
    QualificationPeriodEnd TEXT,

    -- FHIR: Meta
    MetaVersionId TEXT DEFAULT '1',
    MetaLastUpdated TEXT NOT NULL DEFAULT (datetime('now'))
);

-- FHIR Organization Resource (simplified)
CREATE TABLE IF NOT EXISTS Organization (
    Id TEXT PRIMARY KEY,
    ResourceType TEXT NOT NULL DEFAULT 'Organization',
    Active INTEGER NOT NULL DEFAULT 1,
    Name TEXT NOT NULL,
    TypeCode TEXT,                                -- FHIR: Organization.type
    TypeDisplay TEXT,
    TelecomPhone TEXT,
    TelecomEmail TEXT,
    AddressLine1 TEXT,
    AddressCity TEXT,
    AddressState TEXT,
    AddressPostalCode TEXT,
    MetaLastUpdated TEXT NOT NULL DEFAULT (datetime('now'))
);

-- FHIR Condition Resource (https://hl7.org/fhir/R4/condition.html)
CREATE TABLE IF NOT EXISTS Condition (
    Id TEXT PRIMARY KEY,
    ResourceType TEXT NOT NULL DEFAULT 'Condition',

    -- FHIR: Condition.clinicalStatus (required)
    ClinicalStatusCode TEXT NOT NULL CHECK (ClinicalStatusCode IN ('active', 'recurrence', 'relapse', 'inactive', 'remission', 'resolved')),

    -- FHIR: Condition.verificationStatus
    VerificationStatusCode TEXT CHECK (VerificationStatusCode IN ('unconfirmed', 'provisional', 'differential', 'confirmed', 'refuted', 'entered-in-error')),

    -- FHIR: Condition.category[]
    CategoryCode TEXT DEFAULT 'problem-list-item', -- encounter-diagnosis | problem-list-item

    -- FHIR: Condition.severity
    SeverityCode TEXT CHECK (SeverityCode IN ('24484000', '6736007', '255604002', NULL)), -- SNOMED: severe, moderate, mild
    SeverityDisplay TEXT,

    -- FHIR: Condition.code (required) - ICD-10 or SNOMED CT
    CodeSystem TEXT NOT NULL DEFAULT 'http://hl7.org/fhir/sid/icd-10-cm',
    CodeValue TEXT NOT NULL,                      -- ICD-10 code
    CodeDisplay TEXT NOT NULL,                    -- Human readable condition name

    -- FHIR: Condition.bodySite[]
    BodySiteCode TEXT,                            -- SNOMED body site
    BodySiteDisplay TEXT,

    -- FHIR: Condition.subject (required)
    SubjectReference TEXT NOT NULL,               -- Reference to Patient

    -- FHIR: Condition.encounter
    EncounterReference TEXT,

    -- FHIR: Condition.onset[x]
    OnsetDateTime TEXT,
    OnsetAge INTEGER,
    OnsetString TEXT,

    -- FHIR: Condition.abatement[x]
    AbatementDateTime TEXT,
    AbatementAge INTEGER,
    AbatementString TEXT,

    -- FHIR: Condition.recordedDate
    RecordedDate TEXT NOT NULL DEFAULT (date('now')),

    -- FHIR: Condition.recorder
    RecorderReference TEXT,                       -- Reference to Practitioner

    -- FHIR: Condition.asserter
    AsserterReference TEXT,                       -- Reference to Practitioner

    -- FHIR: Condition.note[]
    NoteText TEXT,
    NoteTime TEXT,
    NoteAuthor TEXT,

    -- FHIR: Meta
    MetaVersionId TEXT DEFAULT '1',
    MetaLastUpdated TEXT NOT NULL DEFAULT (datetime('now')),

    FOREIGN KEY (SubjectReference) REFERENCES Patient(Id),
    FOREIGN KEY (RecorderReference) REFERENCES Practitioner(Id),
    FOREIGN KEY (AsserterReference) REFERENCES Practitioner(Id)
);

-- FHIR AllergyIntolerance Resource (https://hl7.org/fhir/R4/allergyintolerance.html)
CREATE TABLE IF NOT EXISTS AllergyIntolerance (
    Id TEXT PRIMARY KEY,
    ResourceType TEXT NOT NULL DEFAULT 'AllergyIntolerance',

    -- FHIR: AllergyIntolerance.clinicalStatus
    ClinicalStatusCode TEXT CHECK (ClinicalStatusCode IN ('active', 'inactive', 'resolved')),

    -- FHIR: AllergyIntolerance.verificationStatus
    VerificationStatusCode TEXT CHECK (VerificationStatusCode IN ('unconfirmed', 'confirmed', 'refuted', 'entered-in-error')),

    -- FHIR: AllergyIntolerance.type
    AllergyType TEXT CHECK (AllergyType IN ('allergy', 'intolerance')),

    -- FHIR: AllergyIntolerance.category[]
    Category TEXT CHECK (Category IN ('food', 'medication', 'environment', 'biologic')),

    -- FHIR: AllergyIntolerance.criticality
    Criticality TEXT CHECK (Criticality IN ('low', 'high', 'unable-to-assess')),

    -- FHIR: AllergyIntolerance.code (what the patient is allergic to)
    CodeSystem TEXT DEFAULT 'http://www.nlm.nih.gov/research/umls/rxnorm',
    CodeValue TEXT NOT NULL,
    CodeDisplay TEXT NOT NULL,

    -- FHIR: AllergyIntolerance.patient (required)
    PatientReference TEXT NOT NULL,

    -- FHIR: AllergyIntolerance.encounter
    EncounterReference TEXT,

    -- FHIR: AllergyIntolerance.onset[x]
    OnsetDateTime TEXT,
    OnsetString TEXT,

    -- FHIR: AllergyIntolerance.recordedDate
    RecordedDate TEXT DEFAULT (date('now')),

    -- FHIR: AllergyIntolerance.recorder
    RecorderReference TEXT,

    -- FHIR: AllergyIntolerance.asserter
    AsserterReference TEXT,

    -- FHIR: AllergyIntolerance.lastOccurrence
    LastOccurrence TEXT,

    -- FHIR: AllergyIntolerance.note[]
    NoteText TEXT,

    -- FHIR: AllergyIntolerance.reaction[] - primary reaction
    ReactionManifestationCode TEXT,               -- SNOMED code for reaction
    ReactionManifestationDisplay TEXT,            -- e.g., "Anaphylaxis", "Hives"
    ReactionSeverity TEXT CHECK (ReactionSeverity IN ('mild', 'moderate', 'severe')),

    -- FHIR: Meta
    MetaVersionId TEXT DEFAULT '1',
    MetaLastUpdated TEXT NOT NULL DEFAULT (datetime('now')),

    FOREIGN KEY (PatientReference) REFERENCES Patient(Id),
    FOREIGN KEY (RecorderReference) REFERENCES Practitioner(Id)
);

-- FHIR MedicationStatement Resource (https://hl7.org/fhir/R4/medicationstatement.html)
CREATE TABLE IF NOT EXISTS MedicationStatement (
    Id TEXT PRIMARY KEY,
    ResourceType TEXT NOT NULL DEFAULT 'MedicationStatement',

    -- FHIR: MedicationStatement.status (required)
    Status TEXT NOT NULL CHECK (Status IN ('active', 'completed', 'entered-in-error', 'intended', 'stopped', 'on-hold', 'unknown', 'not-taken')),

    -- FHIR: MedicationStatement.statusReason[]
    StatusReasonCode TEXT,
    StatusReasonDisplay TEXT,

    -- FHIR: MedicationStatement.category
    CategoryCode TEXT DEFAULT 'outpatient',

    -- FHIR: MedicationStatement.medication[x] - using CodeableConcept
    MedicationCodeSystem TEXT DEFAULT 'http://www.nlm.nih.gov/research/umls/rxnorm',
    MedicationCodeValue TEXT NOT NULL,            -- RxNorm code
    MedicationCodeDisplay TEXT NOT NULL,          -- Drug name

    -- FHIR: MedicationStatement.subject (required)
    SubjectReference TEXT NOT NULL,

    -- FHIR: MedicationStatement.context
    ContextReference TEXT,                        -- Encounter reference

    -- FHIR: MedicationStatement.effective[x]
    EffectiveDateTime TEXT,
    EffectivePeriodStart TEXT,
    EffectivePeriodEnd TEXT,

    -- FHIR: MedicationStatement.dateAsserted
    DateAsserted TEXT DEFAULT (date('now')),

    -- FHIR: MedicationStatement.informationSource
    InformationSourceReference TEXT,              -- Who reported this

    -- FHIR: MedicationStatement.reasonCode[]
    ReasonCodeValue TEXT,                         -- SNOMED or ICD-10
    ReasonCodeDisplay TEXT,

    -- FHIR: MedicationStatement.note[]
    NoteText TEXT,

    -- FHIR: MedicationStatement.dosage[]
    DosageText TEXT,                              -- "Take 1 tablet by mouth daily"
    DosageRoute TEXT,                             -- SNOMED route code
    DosageDoseValue REAL,
    DosageDoseUnit TEXT,
    DosageFrequencyValue INTEGER,
    DosageFrequencyPeriod REAL,
    DosageFrequencyPeriodUnit TEXT,               -- d, wk, mo

    -- FHIR: Meta
    MetaVersionId TEXT DEFAULT '1',
    MetaLastUpdated TEXT NOT NULL DEFAULT (datetime('now')),

    FOREIGN KEY (SubjectReference) REFERENCES Patient(Id)
);

-- FHIR Observation Resource (https://hl7.org/fhir/R4/observation.html) - for lab results
CREATE TABLE IF NOT EXISTS Observation (
    Id TEXT PRIMARY KEY,
    ResourceType TEXT NOT NULL DEFAULT 'Observation',

    -- FHIR: Observation.status (required)
    Status TEXT NOT NULL CHECK (Status IN ('registered', 'preliminary', 'final', 'amended', 'corrected', 'cancelled', 'entered-in-error', 'unknown')),

    -- FHIR: Observation.category[]
    CategoryCode TEXT DEFAULT 'laboratory',
    CategoryDisplay TEXT DEFAULT 'Laboratory',

    -- FHIR: Observation.code (required) - LOINC
    CodeSystem TEXT NOT NULL DEFAULT 'http://loinc.org',
    CodeValue TEXT NOT NULL,                      -- LOINC code
    CodeDisplay TEXT NOT NULL,                    -- Test name

    -- FHIR: Observation.subject
    SubjectReference TEXT NOT NULL,

    -- FHIR: Observation.encounter
    EncounterReference TEXT,

    -- FHIR: Observation.effective[x]
    EffectiveDateTime TEXT NOT NULL,

    -- FHIR: Observation.issued
    Issued TEXT,

    -- FHIR: Observation.performer[]
    PerformerReference TEXT,                      -- Lab/Practitioner

    -- FHIR: Observation.value[x] - using Quantity
    ValueQuantityValue REAL,
    ValueQuantityUnit TEXT,
    ValueQuantitySystem TEXT DEFAULT 'http://unitsofmeasure.org',
    ValueQuantityCode TEXT,                       -- UCUM code
    ValueString TEXT,                             -- For non-numeric results

    -- FHIR: Observation.interpretation[]
    InterpretationCode TEXT CHECK (InterpretationCode IN ('N', 'A', 'AA', 'HH', 'LL', 'H', 'L', 'HU', 'LU', NULL)),
    InterpretationDisplay TEXT,

    -- FHIR: Observation.note[]
    NoteText TEXT,

    -- FHIR: Observation.referenceRange[]
    ReferenceRangeLow REAL,
    ReferenceRangeHigh REAL,
    ReferenceRangeText TEXT,

    -- FHIR: Meta
    MetaVersionId TEXT DEFAULT '1',
    MetaLastUpdated TEXT NOT NULL DEFAULT (datetime('now')),

    FOREIGN KEY (SubjectReference) REFERENCES Patient(Id),
    FOREIGN KEY (PerformerReference) REFERENCES Practitioner(Id)
);

-- Indexes for FHIR resource queries
CREATE INDEX IF NOT EXISTS idx_patient_mrn ON Patient(MrnValue);
CREATE INDEX IF NOT EXISTS idx_patient_name ON Patient(NameFamily, NameGiven);
CREATE INDEX IF NOT EXISTS idx_patient_dob ON Patient(BirthDate);
CREATE INDEX IF NOT EXISTS idx_patient_gp ON Patient(GeneralPractitionerId);
CREATE INDEX IF NOT EXISTS idx_condition_patient ON Condition(SubjectReference);
CREATE INDEX IF NOT EXISTS idx_condition_code ON Condition(CodeValue);
CREATE INDEX IF NOT EXISTS idx_condition_clinical ON Condition(ClinicalStatusCode);
CREATE INDEX IF NOT EXISTS idx_allergy_patient ON AllergyIntolerance(PatientReference);
CREATE INDEX IF NOT EXISTS idx_allergy_criticality ON AllergyIntolerance(Criticality);
CREATE INDEX IF NOT EXISTS idx_medication_patient ON MedicationStatement(SubjectReference);
CREATE INDEX IF NOT EXISTS idx_medication_status ON MedicationStatement(Status);
CREATE INDEX IF NOT EXISTS idx_observation_patient ON Observation(SubjectReference);
CREATE INDEX IF NOT EXISTS idx_observation_code ON Observation(CodeValue);
CREATE INDEX IF NOT EXISTS idx_observation_effective ON Observation(EffectiveDateTime);
CREATE INDEX IF NOT EXISTS idx_practitioner_npi ON Practitioner(NpiValue);
