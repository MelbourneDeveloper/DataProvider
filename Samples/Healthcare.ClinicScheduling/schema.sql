-- Healthcare.ClinicScheduling Schema
-- This service manages appointments, rooms, schedules - DIFFERENT schema from PatientRecords!
-- Patient data syncs from PatientRecords but mapped to different structure

CREATE TABLE IF NOT EXISTS ScheduledPatient (
    PatientId TEXT PRIMARY KEY,
    DisplayName TEXT NOT NULL,
    ContactPhone TEXT,
    ContactEmail TEXT,
    PrimaryInsurance TEXT,
    HasAllergies INTEGER NOT NULL DEFAULT 0,
    AllergyWarning TEXT,
    PreferredLanguage TEXT DEFAULT 'English',
    RequiresInterpreter INTEGER NOT NULL DEFAULT 0,
    MobilityAssistance TEXT,
    LastVisitDate TEXT,
    SyncedAt TEXT NOT NULL DEFAULT (datetime('now'))
);

CREATE TABLE IF NOT EXISTS Provider (
    ProviderId TEXT PRIMARY KEY,
    FullName TEXT NOT NULL,
    ProviderType TEXT NOT NULL CHECK (ProviderType IN ('Physician', 'NursePractitioner', 'PhysicianAssistant', 'Specialist')),
    PrimarySpecialty TEXT NOT NULL,
    ScheduleColor TEXT DEFAULT '#3498db',
    DefaultAppointmentMinutes INTEGER NOT NULL DEFAULT 30,
    IsAcceptingNewPatients INTEGER NOT NULL DEFAULT 1,
    SyncedAt TEXT NOT NULL DEFAULT (datetime('now'))
);

CREATE TABLE IF NOT EXISTS ExamRoom (
    RoomId TEXT PRIMARY KEY,
    RoomNumber TEXT NOT NULL UNIQUE,
    RoomType TEXT NOT NULL CHECK (RoomType IN ('Exam', 'Procedure', 'Consultation', 'Lab', 'Imaging')),
    Floor INTEGER NOT NULL,
    Building TEXT NOT NULL,
    Equipment TEXT,
    Capacity INTEGER DEFAULT 1,
    IsWheelchairAccessible INTEGER NOT NULL DEFAULT 1,
    IsActive INTEGER NOT NULL DEFAULT 1
);

CREATE TABLE IF NOT EXISTS Appointment (
    AppointmentId TEXT PRIMARY KEY,
    PatientId TEXT NOT NULL,
    ProviderId TEXT NOT NULL,
    RoomId TEXT,
    AppointmentDate TEXT NOT NULL,
    StartTime TEXT NOT NULL,
    EndTime TEXT NOT NULL,
    DurationMinutes INTEGER NOT NULL,
    AppointmentType TEXT NOT NULL CHECK (AppointmentType IN ('NewPatient', 'FollowUp', 'Urgent', 'Procedure', 'LabWork', 'Consultation', 'Telehealth')),
    Status TEXT NOT NULL CHECK (Status IN ('Scheduled', 'Confirmed', 'CheckedIn', 'InProgress', 'Completed', 'NoShow', 'Cancelled', 'Rescheduled')),
    ReasonForVisit TEXT NOT NULL,
    ChiefComplaint TEXT,
    Notes TEXT,
    InsuranceVerified INTEGER DEFAULT 0,
    CopayAmount REAL,
    CopayCollected INTEGER DEFAULT 0,
    CheckInTime TEXT,
    CheckOutTime TEXT,
    CreatedAt TEXT NOT NULL DEFAULT (datetime('now')),
    UpdatedAt TEXT NOT NULL DEFAULT (datetime('now')),
    CancelledAt TEXT,
    CancellationReason TEXT,
    FOREIGN KEY (PatientId) REFERENCES ScheduledPatient(PatientId),
    FOREIGN KEY (ProviderId) REFERENCES Provider(ProviderId),
    FOREIGN KEY (RoomId) REFERENCES ExamRoom(RoomId)
);

CREATE TABLE IF NOT EXISTS ProviderSchedule (
    ScheduleId TEXT PRIMARY KEY,
    ProviderId TEXT NOT NULL,
    DayOfWeek INTEGER NOT NULL CHECK (DayOfWeek BETWEEN 0 AND 6),
    StartTime TEXT NOT NULL,
    EndTime TEXT NOT NULL,
    IsAvailable INTEGER NOT NULL DEFAULT 1,
    EffectiveFrom TEXT NOT NULL,
    EffectiveTo TEXT,
    FOREIGN KEY (ProviderId) REFERENCES Provider(ProviderId)
);

CREATE TABLE IF NOT EXISTS ProviderTimeOff (
    TimeOffId TEXT PRIMARY KEY,
    ProviderId TEXT NOT NULL,
    StartDate TEXT NOT NULL,
    EndDate TEXT NOT NULL,
    Reason TEXT,
    IsApproved INTEGER NOT NULL DEFAULT 0,
    CreatedAt TEXT NOT NULL DEFAULT (datetime('now')),
    FOREIGN KEY (ProviderId) REFERENCES Provider(ProviderId)
);

CREATE TABLE IF NOT EXISTS WaitlistEntry (
    WaitlistId TEXT PRIMARY KEY,
    PatientId TEXT NOT NULL,
    ProviderId TEXT,
    RequestedDate TEXT,
    AppointmentType TEXT NOT NULL,
    Priority INTEGER NOT NULL DEFAULT 5 CHECK (Priority BETWEEN 1 AND 10),
    ReasonForVisit TEXT NOT NULL,
    PreferredTimes TEXT,
    AddedAt TEXT NOT NULL DEFAULT (datetime('now')),
    NotifiedAt TEXT,
    Status TEXT NOT NULL CHECK (Status IN ('Waiting', 'Notified', 'Scheduled', 'Expired', 'Cancelled')),
    FOREIGN KEY (PatientId) REFERENCES ScheduledPatient(PatientId),
    FOREIGN KEY (ProviderId) REFERENCES Provider(ProviderId)
);

CREATE TABLE IF NOT EXISTS AppointmentReminder (
    ReminderId TEXT PRIMARY KEY,
    AppointmentId TEXT NOT NULL,
    ReminderType TEXT NOT NULL CHECK (ReminderType IN ('SMS', 'Email', 'Phone', 'Portal')),
    ScheduledFor TEXT NOT NULL,
    SentAt TEXT,
    DeliveryStatus TEXT CHECK (DeliveryStatus IN ('Pending', 'Sent', 'Delivered', 'Failed', 'Bounced')),
    ResponseReceived INTEGER DEFAULT 0,
    ConfirmationStatus TEXT CHECK (ConfirmationStatus IN ('Pending', 'Confirmed', 'Declined', 'NoResponse')),
    FOREIGN KEY (AppointmentId) REFERENCES Appointment(AppointmentId)
);

-- Indexes for scheduling queries
CREATE INDEX IF NOT EXISTS idx_appointment_date ON Appointment(AppointmentDate);
CREATE INDEX IF NOT EXISTS idx_appointment_patient ON Appointment(PatientId);
CREATE INDEX IF NOT EXISTS idx_appointment_provider ON Appointment(ProviderId);
CREATE INDEX IF NOT EXISTS idx_appointment_status ON Appointment(Status);
CREATE INDEX IF NOT EXISTS idx_provider_schedule ON ProviderSchedule(ProviderId, DayOfWeek);
CREATE INDEX IF NOT EXISTS idx_waitlist_patient ON WaitlistEntry(PatientId);
CREATE INDEX IF NOT EXISTS idx_waitlist_priority ON WaitlistEntry(Priority, AddedAt);
CREATE INDEX IF NOT EXISTS idx_reminder_scheduled ON AppointmentReminder(ScheduledFor);
