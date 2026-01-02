using System.Collections.Immutable;
using System.Globalization;
using Conduit;

namespace Clinical.Api;

// ============================================================================
// CONDUIT QUERY/COMMAND RECORDS
// ============================================================================

/// <summary>Query to get patients with filters.</summary>
public sealed record GetPatientsQuery(
    bool? Active,
    string? FamilyName,
    string? GivenName,
    string? Gender
);

/// <summary>Query to get patient by ID.</summary>
public sealed record GetPatientByIdQuery(string Id);

/// <summary>Result of GetPatientById.</summary>
public sealed record GetPatientByIdResult(GetPatientById? Patient, bool Found);

/// <summary>Command to create a patient.</summary>
public sealed record CreatePatientCommand(CreatePatientRequest Request);

/// <summary>Result of created patient.</summary>
public sealed record CreatedPatient(
    string Id,
    bool Active,
    string GivenName,
    string FamilyName,
    string? BirthDate,
    string? Gender,
    string? Phone,
    string? Email,
    string? AddressLine,
    string? City,
    string? State,
    string? PostalCode,
    string? Country,
    string LastUpdated,
    long VersionId
);

/// <summary>Command to update a patient.</summary>
public sealed record UpdatePatientCommand(string Id, UpdatePatientRequest Request);

/// <summary>Result of updated patient.</summary>
public sealed record UpdatedPatient(CreatedPatient? Patient, bool Found);

/// <summary>Query to search patients.</summary>
public sealed record SearchPatientsQuery(string Query);

/// <summary>Query to get encounters by patient.</summary>
public sealed record GetEncountersQuery(string PatientId);

/// <summary>Command to create an encounter.</summary>
public sealed record CreateEncounterCommand(string PatientId, CreateEncounterRequest Request);

/// <summary>Result of created encounter.</summary>
public sealed record CreatedEncounter(
    string Id,
    string Status,
    string Class,
    string PatientId,
    string? PractitionerId,
    string? ServiceType,
    string? ReasonCode,
    string PeriodStart,
    string? PeriodEnd,
    string? Notes,
    string LastUpdated,
    long VersionId
);

/// <summary>Query to get conditions by patient.</summary>
public sealed record GetConditionsQuery(string PatientId);

/// <summary>Command to create a condition.</summary>
public sealed record CreateConditionCommand(string PatientId, CreateConditionRequest Request);

/// <summary>Result of created condition.</summary>
public sealed record CreatedCondition(
    string Id,
    string ClinicalStatus,
    string? VerificationStatus,
    string? Category,
    string? Severity,
    string CodeSystem,
    string CodeValue,
    string CodeDisplay,
    string SubjectReference,
    string? EncounterReference,
    string? OnsetDateTime,
    string RecordedDate,
    string? RecorderReference,
    string? NoteText,
    string LastUpdated,
    long VersionId
);

/// <summary>Query to get medications by patient.</summary>
public sealed record GetMedicationsQuery(string PatientId);

/// <summary>Command to create a medication.</summary>
public sealed record CreateMedicationCommand(
    string PatientId,
    CreateMedicationRequestRequest Request
);

/// <summary>Result of created medication.</summary>
public sealed record CreatedMedication(
    string Id,
    string Status,
    string Intent,
    string PatientId,
    string PractitionerId,
    string? EncounterId,
    string MedicationCode,
    string MedicationDisplay,
    string? DosageInstruction,
    double? Quantity,
    string? Unit,
    int Refills,
    string AuthoredOn,
    string LastUpdated,
    long VersionId
);

/// <summary>Query to get sync changes.</summary>
public sealed record GetSyncChangesQuery(long FromVersion, int Limit);

/// <summary>Result of sync changes.</summary>
public sealed record SyncChangesResult(ImmutableList<SyncLogEntry> Changes);

/// <summary>Query to get sync origin.</summary>
public sealed record GetSyncOriginQuery();

/// <summary>Result of sync origin.</summary>
public sealed record SyncOriginResult(string OriginId);

// ============================================================================
// HANDLER IMPLEMENTATIONS
// ============================================================================

/// <summary>
/// Conduit handlers for Clinical API endpoints.
/// </summary>
public static class ClinicalHandlers
{
    /// <summary>
    /// Handles GetPatients request.
    /// </summary>
    public static async Task<Result<ImmutableList<GetPatients>, ConduitError>> HandleGetPatients(
        GetPatientsQuery request,
        Func<SqliteConnection> getConn,
        CancellationToken _
    )
    {
        try
        {
            using var conn = getConn();
            var result = await conn.GetPatientsAsync(
                    active: request.Active.HasValue
                        ? (request.Active.Value ? 1L : 0L)
                        : DBNull.Value,
                    familyName: request.FamilyName ?? (object)DBNull.Value,
                    givenName: request.GivenName ?? (object)DBNull.Value,
                    gender: request.Gender ?? (object)DBNull.Value
                )
                .ConfigureAwait(false);

            return result switch
            {
                GetPatientsOk ok => new Result<ImmutableList<GetPatients>, ConduitError>.Ok<
                    ImmutableList<GetPatients>,
                    ConduitError
                >(ok.Value),
                GetPatientsError err => new Result<ImmutableList<GetPatients>, ConduitError>.Error<
                    ImmutableList<GetPatients>,
                    ConduitError
                >(new ConduitErrorHandlerFailed("GetPatients", err.Value.Message, null)),
            };
        }
        catch (Exception ex)
        {
            return new Result<ImmutableList<GetPatients>, ConduitError>.Error<
                ImmutableList<GetPatients>,
                ConduitError
            >(ConduitErrorHandlerFailed.FromException("GetPatients", ex));
        }
    }

    /// <summary>
    /// Handles GetPatientById request.
    /// </summary>
    public static async Task<Result<GetPatientByIdResult, ConduitError>> HandleGetPatientById(
        GetPatientByIdQuery request,
        Func<SqliteConnection> getConn,
        CancellationToken _
    )
    {
        try
        {
            using var conn = getConn();
            var result = await conn.GetPatientByIdAsync(id: request.Id).ConfigureAwait(false);

            return result switch
            {
                GetPatientByIdOk ok when ok.Value.Count > 0 => new Result<
                    GetPatientByIdResult,
                    ConduitError
                >.Ok<GetPatientByIdResult, ConduitError>(
                    new GetPatientByIdResult(Patient: ok.Value[0], Found: true)
                ),
                GetPatientByIdOk => new Result<GetPatientByIdResult, ConduitError>.Ok<
                    GetPatientByIdResult,
                    ConduitError
                >(new GetPatientByIdResult(Patient: null, Found: false)),
                GetPatientByIdError err => new Result<GetPatientByIdResult, ConduitError>.Error<
                    GetPatientByIdResult,
                    ConduitError
                >(new ConduitErrorHandlerFailed("GetPatientById", err.Value.Message, null)),
            };
        }
        catch (Exception ex)
        {
            return new Result<GetPatientByIdResult, ConduitError>.Error<
                GetPatientByIdResult,
                ConduitError
            >(ConduitErrorHandlerFailed.FromException("GetPatientById", ex));
        }
    }

    /// <summary>
    /// Handles CreatePatient request.
    /// </summary>
    public static async Task<Result<CreatedPatient, ConduitError>> HandleCreatePatient(
        CreatePatientCommand request,
        Func<SqliteConnection> getConn,
        CancellationToken ct
    )
    {
        try
        {
            using var conn = getConn();
            var transaction = await conn.BeginTransactionAsync(ct).ConfigureAwait(false);
            await using var _ = transaction.ConfigureAwait(false);

            var id = Guid.NewGuid().ToString();
            var now = DateTime.UtcNow.ToString(
                "yyyy-MM-ddTHH:mm:ss.fffZ",
                CultureInfo.InvariantCulture
            );

            var result = await transaction
                .Insertfhir_PatientAsync(
                    id: id,
                    active: request.Request.Active ? 1L : 0L,
                    givenname: request.Request.GivenName,
                    familyname: request.Request.FamilyName,
                    birthdate: request.Request.BirthDate,
                    gender: request.Request.Gender,
                    phone: request.Request.Phone,
                    email: request.Request.Email,
                    addressline: request.Request.AddressLine,
                    city: request.Request.City,
                    state: request.Request.State,
                    postalcode: request.Request.PostalCode,
                    country: request.Request.Country,
                    lastupdated: now,
                    versionid: 1L
                )
                .ConfigureAwait(false);

            return result switch
            {
                InsertOk => await CommitAndReturnPatient(transaction, id, request.Request, now)
                    .ConfigureAwait(false),
                InsertError err => new Result<CreatedPatient, ConduitError>.Error<
                    CreatedPatient,
                    ConduitError
                >(new ConduitErrorHandlerFailed("CreatePatient", err.Value.Message, null)),
            };
        }
        catch (Exception ex)
        {
            return new Result<CreatedPatient, ConduitError>.Error<CreatedPatient, ConduitError>(
                ConduitErrorHandlerFailed.FromException("CreatePatient", ex)
            );
        }
    }

    /// <summary>
    /// Handles UpdatePatient request.
    /// </summary>
    public static async Task<Result<UpdatedPatient, ConduitError>> HandleUpdatePatient(
        UpdatePatientCommand request,
        Func<SqliteConnection> getConn,
        CancellationToken ct
    )
    {
        try
        {
            using var conn = getConn();

            var existingResult = await conn.GetPatientByIdAsync(id: request.Id)
                .ConfigureAwait(false);

            if (existingResult is GetPatientByIdOk { Value.Count: 0 })
            {
                return new Result<UpdatedPatient, ConduitError>.Ok<UpdatedPatient, ConduitError>(
                    new UpdatedPatient(Patient: null, Found: false)
                );
            }

            if (existingResult is GetPatientByIdError fetchErr)
            {
                return new Result<UpdatedPatient, ConduitError>.Error<UpdatedPatient, ConduitError>(
                    new ConduitErrorHandlerFailed("UpdatePatient", fetchErr.Value.Message, null)
                );
            }

            var existing = ((GetPatientByIdOk)existingResult).Value[0];
            var newVersionId = existing.VersionId + 1;

            var transaction = await conn.BeginTransactionAsync(ct).ConfigureAwait(false);
            await using var _ = transaction.ConfigureAwait(false);

            var now = DateTime.UtcNow.ToString(
                "yyyy-MM-ddTHH:mm:ss.fffZ",
                CultureInfo.InvariantCulture
            );

            var result = await transaction
                .Updatefhir_PatientAsync(
                    id: request.Id,
                    active: request.Request.Active ? 1L : 0L,
                    givenname: request.Request.GivenName,
                    familyname: request.Request.FamilyName,
                    birthdate: request.Request.BirthDate ?? string.Empty,
                    gender: request.Request.Gender ?? string.Empty,
                    phone: request.Request.Phone ?? string.Empty,
                    email: request.Request.Email ?? string.Empty,
                    addressline: request.Request.AddressLine ?? string.Empty,
                    city: request.Request.City ?? string.Empty,
                    state: request.Request.State ?? string.Empty,
                    postalcode: request.Request.PostalCode ?? string.Empty,
                    country: request.Request.Country ?? string.Empty,
                    lastupdated: now,
                    versionid: newVersionId
                )
                .ConfigureAwait(false);

            return result switch
            {
                UpdateOk => await CommitAndReturnUpdatedPatient(
                        transaction,
                        request.Id,
                        request.Request,
                        now,
                        newVersionId
                    )
                    .ConfigureAwait(false),
                UpdateError err => new Result<UpdatedPatient, ConduitError>.Error<
                    UpdatedPatient,
                    ConduitError
                >(new ConduitErrorHandlerFailed("UpdatePatient", err.Value.Message, null)),
            };
        }
        catch (Exception ex)
        {
            return new Result<UpdatedPatient, ConduitError>.Error<UpdatedPatient, ConduitError>(
                ConduitErrorHandlerFailed.FromException("UpdatePatient", ex)
            );
        }
    }

    /// <summary>
    /// Handles SearchPatients request.
    /// </summary>
    public static async Task<
        Result<ImmutableList<SearchPatients>, ConduitError>
    > HandleSearchPatients(
        SearchPatientsQuery request,
        Func<SqliteConnection> getConn,
        CancellationToken _
    )
    {
        try
        {
            using var conn = getConn();
            var result = await conn.SearchPatientsAsync(term: $"%{request.Query}%")
                .ConfigureAwait(false);

            return result switch
            {
                SearchPatientsOk ok => new Result<ImmutableList<SearchPatients>, ConduitError>.Ok<
                    ImmutableList<SearchPatients>,
                    ConduitError
                >(ok.Value),
                SearchPatientsError err => new Result<
                    ImmutableList<SearchPatients>,
                    ConduitError
                >.Error<ImmutableList<SearchPatients>, ConduitError>(
                    new ConduitErrorHandlerFailed("SearchPatients", err.Value.Message, null)
                ),
            };
        }
        catch (Exception ex)
        {
            return new Result<ImmutableList<SearchPatients>, ConduitError>.Error<
                ImmutableList<SearchPatients>,
                ConduitError
            >(ConduitErrorHandlerFailed.FromException("SearchPatients", ex));
        }
    }

    /// <summary>
    /// Handles GetEncountersByPatient request.
    /// </summary>
    public static async Task<
        Result<ImmutableList<GetEncountersByPatient>, ConduitError>
    > HandleGetEncounters(
        GetEncountersQuery request,
        Func<SqliteConnection> getConn,
        CancellationToken _
    )
    {
        try
        {
            using var conn = getConn();
            var result = await conn.GetEncountersByPatientAsync(patientId: request.PatientId)
                .ConfigureAwait(false);

            return result switch
            {
                GetEncountersOk ok => new Result<
                    ImmutableList<GetEncountersByPatient>,
                    ConduitError
                >.Ok<ImmutableList<GetEncountersByPatient>, ConduitError>(ok.Value),
                GetEncountersError err => new Result<
                    ImmutableList<GetEncountersByPatient>,
                    ConduitError
                >.Error<ImmutableList<GetEncountersByPatient>, ConduitError>(
                    new ConduitErrorHandlerFailed("GetEncounters", err.Value.Message, null)
                ),
            };
        }
        catch (Exception ex)
        {
            return new Result<ImmutableList<GetEncountersByPatient>, ConduitError>.Error<
                ImmutableList<GetEncountersByPatient>,
                ConduitError
            >(ConduitErrorHandlerFailed.FromException("GetEncounters", ex));
        }
    }

    /// <summary>
    /// Handles CreateEncounter request.
    /// </summary>
    public static async Task<Result<CreatedEncounter, ConduitError>> HandleCreateEncounter(
        CreateEncounterCommand request,
        Func<SqliteConnection> getConn,
        CancellationToken ct
    )
    {
        try
        {
            using var conn = getConn();
            var transaction = await conn.BeginTransactionAsync(ct).ConfigureAwait(false);
            await using var _ = transaction.ConfigureAwait(false);

            var id = Guid.NewGuid().ToString();
            var now = DateTime.UtcNow.ToString(
                "yyyy-MM-ddTHH:mm:ss.fffZ",
                CultureInfo.InvariantCulture
            );

            var result = await transaction
                .Insertfhir_EncounterAsync(
                    id: id,
                    status: request.Request.Status,
                    @class: request.Request.Class,
                    patientid: request.PatientId,
                    practitionerid: request.Request.PractitionerId,
                    servicetype: request.Request.ServiceType,
                    reasoncode: request.Request.ReasonCode,
                    periodstart: request.Request.PeriodStart,
                    periodend: request.Request.PeriodEnd,
                    notes: request.Request.Notes,
                    lastupdated: now,
                    versionid: 1L
                )
                .ConfigureAwait(false);

            return result switch
            {
                InsertOk => await CommitAndReturnEncounter(
                        transaction,
                        id,
                        request.PatientId,
                        request.Request,
                        now
                    )
                    .ConfigureAwait(false),
                InsertError err => new Result<CreatedEncounter, ConduitError>.Error<
                    CreatedEncounter,
                    ConduitError
                >(new ConduitErrorHandlerFailed("CreateEncounter", err.Value.Message, null)),
            };
        }
        catch (Exception ex)
        {
            return new Result<CreatedEncounter, ConduitError>.Error<CreatedEncounter, ConduitError>(
                ConduitErrorHandlerFailed.FromException("CreateEncounter", ex)
            );
        }
    }

    /// <summary>
    /// Handles GetConditionsByPatient request.
    /// </summary>
    public static async Task<
        Result<ImmutableList<GetConditionsByPatient>, ConduitError>
    > HandleGetConditions(
        GetConditionsQuery request,
        Func<SqliteConnection> getConn,
        CancellationToken _
    )
    {
        try
        {
            using var conn = getConn();
            var result = await conn.GetConditionsByPatientAsync(patientId: request.PatientId)
                .ConfigureAwait(false);

            return result switch
            {
                GetConditionsOk ok => new Result<
                    ImmutableList<GetConditionsByPatient>,
                    ConduitError
                >.Ok<ImmutableList<GetConditionsByPatient>, ConduitError>(ok.Value),
                GetConditionsError err => new Result<
                    ImmutableList<GetConditionsByPatient>,
                    ConduitError
                >.Error<ImmutableList<GetConditionsByPatient>, ConduitError>(
                    new ConduitErrorHandlerFailed("GetConditions", err.Value.Message, null)
                ),
            };
        }
        catch (Exception ex)
        {
            return new Result<ImmutableList<GetConditionsByPatient>, ConduitError>.Error<
                ImmutableList<GetConditionsByPatient>,
                ConduitError
            >(ConduitErrorHandlerFailed.FromException("GetConditions", ex));
        }
    }

    /// <summary>
    /// Handles CreateCondition request.
    /// </summary>
    public static async Task<Result<CreatedCondition, ConduitError>> HandleCreateCondition(
        CreateConditionCommand request,
        Func<SqliteConnection> getConn,
        CancellationToken ct
    )
    {
        try
        {
            using var conn = getConn();
            var transaction = await conn.BeginTransactionAsync(ct).ConfigureAwait(false);
            await using var _ = transaction.ConfigureAwait(false);

            var id = Guid.NewGuid().ToString();
            var now = DateTime.UtcNow.ToString(
                "yyyy-MM-ddTHH:mm:ss.fffZ",
                CultureInfo.InvariantCulture
            );
            var recordedDate = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            var result = await transaction
                .Insertfhir_ConditionAsync(
                    id: id,
                    clinicalstatus: request.Request.ClinicalStatus,
                    verificationstatus: request.Request.VerificationStatus,
                    category: request.Request.Category,
                    severity: request.Request.Severity,
                    codesystem: request.Request.CodeSystem,
                    codevalue: request.Request.CodeValue,
                    codedisplay: request.Request.CodeDisplay,
                    subjectreference: request.PatientId,
                    encounterreference: request.Request.EncounterReference,
                    onsetdatetime: request.Request.OnsetDateTime,
                    recordeddate: recordedDate,
                    recorderreference: request.Request.RecorderReference,
                    notetext: request.Request.NoteText,
                    lastupdated: now,
                    versionid: 1L
                )
                .ConfigureAwait(false);

            return result switch
            {
                InsertOk => await CommitAndReturnCondition(
                        transaction,
                        id,
                        request.PatientId,
                        request.Request,
                        recordedDate,
                        now
                    )
                    .ConfigureAwait(false),
                InsertError err => new Result<CreatedCondition, ConduitError>.Error<
                    CreatedCondition,
                    ConduitError
                >(new ConduitErrorHandlerFailed("CreateCondition", err.Value.Message, null)),
            };
        }
        catch (Exception ex)
        {
            return new Result<CreatedCondition, ConduitError>.Error<CreatedCondition, ConduitError>(
                ConduitErrorHandlerFailed.FromException("CreateCondition", ex)
            );
        }
    }

    /// <summary>
    /// Handles GetMedicationsByPatient request.
    /// </summary>
    public static async Task<
        Result<ImmutableList<GetMedicationsByPatient>, ConduitError>
    > HandleGetMedications(
        GetMedicationsQuery request,
        Func<SqliteConnection> getConn,
        CancellationToken _
    )
    {
        try
        {
            using var conn = getConn();
            var result = await conn.GetMedicationsByPatientAsync(patientId: request.PatientId)
                .ConfigureAwait(false);

            return result switch
            {
                GetMedicationsOk ok => new Result<
                    ImmutableList<GetMedicationsByPatient>,
                    ConduitError
                >.Ok<ImmutableList<GetMedicationsByPatient>, ConduitError>(ok.Value),
                GetMedicationsError err => new Result<
                    ImmutableList<GetMedicationsByPatient>,
                    ConduitError
                >.Error<ImmutableList<GetMedicationsByPatient>, ConduitError>(
                    new ConduitErrorHandlerFailed("GetMedications", err.Value.Message, null)
                ),
            };
        }
        catch (Exception ex)
        {
            return new Result<ImmutableList<GetMedicationsByPatient>, ConduitError>.Error<
                ImmutableList<GetMedicationsByPatient>,
                ConduitError
            >(ConduitErrorHandlerFailed.FromException("GetMedications", ex));
        }
    }

    /// <summary>
    /// Handles CreateMedicationRequest request.
    /// </summary>
    public static async Task<Result<CreatedMedication, ConduitError>> HandleCreateMedication(
        CreateMedicationCommand request,
        Func<SqliteConnection> getConn,
        CancellationToken ct
    )
    {
        try
        {
            using var conn = getConn();
            var transaction = await conn.BeginTransactionAsync(ct).ConfigureAwait(false);
            await using var _ = transaction.ConfigureAwait(false);

            var id = Guid.NewGuid().ToString();
            var now = DateTime.UtcNow.ToString(
                "yyyy-MM-ddTHH:mm:ss.fffZ",
                CultureInfo.InvariantCulture
            );

            var result = await transaction
                .Insertfhir_MedicationRequestAsync(
                    id: id,
                    status: request.Request.Status,
                    intent: request.Request.Intent,
                    patientid: request.PatientId,
                    practitionerid: request.Request.PractitionerId,
                    encounterid: request.Request.EncounterId,
                    medicationcode: request.Request.MedicationCode,
                    medicationdisplay: request.Request.MedicationDisplay,
                    dosageinstruction: request.Request.DosageInstruction,
                    quantity: request.Request.Quantity,
                    unit: request.Request.Unit,
                    refills: request.Request.Refills,
                    authoredon: now,
                    lastupdated: now,
                    versionid: 1L
                )
                .ConfigureAwait(false);

            return result switch
            {
                InsertOk => await CommitAndReturnMedication(
                        transaction,
                        id,
                        request.PatientId,
                        request.Request,
                        now
                    )
                    .ConfigureAwait(false),
                InsertError err => new Result<CreatedMedication, ConduitError>.Error<
                    CreatedMedication,
                    ConduitError
                >(new ConduitErrorHandlerFailed("CreateMedication", err.Value.Message, null)),
            };
        }
        catch (Exception ex)
        {
            return new Result<CreatedMedication, ConduitError>.Error<
                CreatedMedication,
                ConduitError
            >(ConduitErrorHandlerFailed.FromException("CreateMedication", ex));
        }
    }

    /// <summary>
    /// Handles GetSyncChanges request.
    /// </summary>
    public static Task<Result<SyncChangesResult, ConduitError>> HandleGetSyncChanges(
        GetSyncChangesQuery request,
        Func<SqliteConnection> getConn,
        CancellationToken _
    )
    {
        try
        {
            using var conn = getConn();
            var result = SyncLogRepository.FetchChanges(
                conn,
                fromVersion: request.FromVersion,
                batchSize: request.Limit
            );

            return Task.FromResult<Result<SyncChangesResult, ConduitError>>(
                result switch
                {
                    SyncLogListOk ok => new Result<SyncChangesResult, ConduitError>.Ok<
                        SyncChangesResult,
                        ConduitError
                    >(new SyncChangesResult(Changes: [.. ok.Value])),
                    SyncLogListError err => new Result<SyncChangesResult, ConduitError>.Error<
                        SyncChangesResult,
                        ConduitError
                    >(
                        new ConduitErrorHandlerFailed(
                            "GetSyncChanges",
                            SyncHelpers.ToMessage(err.Value),
                            null
                        )
                    ),
                }
            );
        }
        catch (Exception ex)
        {
            return Task.FromResult<Result<SyncChangesResult, ConduitError>>(
                new Result<SyncChangesResult, ConduitError>.Error<SyncChangesResult, ConduitError>(
                    ConduitErrorHandlerFailed.FromException("GetSyncChanges", ex)
                )
            );
        }
    }

    /// <summary>
    /// Handles GetSyncOrigin request.
    /// </summary>
    public static Task<Result<SyncOriginResult, ConduitError>> HandleGetSyncOrigin(
        GetSyncOriginQuery _,
        Func<SqliteConnection> getConn,
        CancellationToken __
    )
    {
        try
        {
            using var conn = getConn();
            var result = SyncSchema.GetOriginId(conn);

            return Task.FromResult<Result<SyncOriginResult, ConduitError>>(
                result switch
                {
                    StringSyncOk ok => new Result<SyncOriginResult, ConduitError>.Ok<
                        SyncOriginResult,
                        ConduitError
                    >(new SyncOriginResult(OriginId: ok.Value)),
                    StringSyncError err => new Result<SyncOriginResult, ConduitError>.Error<
                        SyncOriginResult,
                        ConduitError
                    >(
                        new ConduitErrorHandlerFailed(
                            "GetSyncOrigin",
                            SyncHelpers.ToMessage(err.Value),
                            null
                        )
                    ),
                }
            );
        }
        catch (Exception ex)
        {
            return Task.FromResult<Result<SyncOriginResult, ConduitError>>(
                new Result<SyncOriginResult, ConduitError>.Error<SyncOriginResult, ConduitError>(
                    ConduitErrorHandlerFailed.FromException("GetSyncOrigin", ex)
                )
            );
        }
    }

    private static async Task<Result<CreatedPatient, ConduitError>> CommitAndReturnPatient(
        System.Data.Common.DbTransaction transaction,
        string id,
        CreatePatientRequest request,
        string now
    )
    {
        await transaction.CommitAsync().ConfigureAwait(false);
        return new Result<CreatedPatient, ConduitError>.Ok<CreatedPatient, ConduitError>(
            new CreatedPatient(
                Id: id,
                Active: request.Active,
                GivenName: request.GivenName,
                FamilyName: request.FamilyName,
                BirthDate: request.BirthDate,
                Gender: request.Gender,
                Phone: request.Phone,
                Email: request.Email,
                AddressLine: request.AddressLine,
                City: request.City,
                State: request.State,
                PostalCode: request.PostalCode,
                Country: request.Country,
                LastUpdated: now,
                VersionId: 1L
            )
        );
    }

    private static async Task<Result<UpdatedPatient, ConduitError>> CommitAndReturnUpdatedPatient(
        System.Data.Common.DbTransaction transaction,
        string id,
        UpdatePatientRequest request,
        string now,
        long versionId
    )
    {
        await transaction.CommitAsync().ConfigureAwait(false);
        return new Result<UpdatedPatient, ConduitError>.Ok<UpdatedPatient, ConduitError>(
            new UpdatedPatient(
                Patient: new CreatedPatient(
                    Id: id,
                    Active: request.Active,
                    GivenName: request.GivenName,
                    FamilyName: request.FamilyName,
                    BirthDate: request.BirthDate,
                    Gender: request.Gender,
                    Phone: request.Phone,
                    Email: request.Email,
                    AddressLine: request.AddressLine,
                    City: request.City,
                    State: request.State,
                    PostalCode: request.PostalCode,
                    Country: request.Country,
                    LastUpdated: now,
                    VersionId: versionId
                ),
                Found: true
            )
        );
    }

    private static async Task<Result<CreatedEncounter, ConduitError>> CommitAndReturnEncounter(
        System.Data.Common.DbTransaction transaction,
        string id,
        string patientId,
        CreateEncounterRequest request,
        string now
    )
    {
        await transaction.CommitAsync().ConfigureAwait(false);
        return new Result<CreatedEncounter, ConduitError>.Ok<CreatedEncounter, ConduitError>(
            new CreatedEncounter(
                Id: id,
                Status: request.Status,
                Class: request.Class,
                PatientId: patientId,
                PractitionerId: request.PractitionerId,
                ServiceType: request.ServiceType,
                ReasonCode: request.ReasonCode,
                PeriodStart: request.PeriodStart,
                PeriodEnd: request.PeriodEnd,
                Notes: request.Notes,
                LastUpdated: now,
                VersionId: 1L
            )
        );
    }

    private static async Task<Result<CreatedCondition, ConduitError>> CommitAndReturnCondition(
        System.Data.Common.DbTransaction transaction,
        string id,
        string patientId,
        CreateConditionRequest request,
        string recordedDate,
        string now
    )
    {
        await transaction.CommitAsync().ConfigureAwait(false);
        return new Result<CreatedCondition, ConduitError>.Ok<CreatedCondition, ConduitError>(
            new CreatedCondition(
                Id: id,
                ClinicalStatus: request.ClinicalStatus,
                VerificationStatus: request.VerificationStatus,
                Category: request.Category,
                Severity: request.Severity,
                CodeSystem: request.CodeSystem,
                CodeValue: request.CodeValue,
                CodeDisplay: request.CodeDisplay,
                SubjectReference: patientId,
                EncounterReference: request.EncounterReference,
                OnsetDateTime: request.OnsetDateTime,
                RecordedDate: recordedDate,
                RecorderReference: request.RecorderReference,
                NoteText: request.NoteText,
                LastUpdated: now,
                VersionId: 1L
            )
        );
    }

    private static async Task<Result<CreatedMedication, ConduitError>> CommitAndReturnMedication(
        System.Data.Common.DbTransaction transaction,
        string id,
        string patientId,
        CreateMedicationRequestRequest request,
        string now
    )
    {
        await transaction.CommitAsync().ConfigureAwait(false);
        return new Result<CreatedMedication, ConduitError>.Ok<CreatedMedication, ConduitError>(
            new CreatedMedication(
                Id: id,
                Status: request.Status,
                Intent: request.Intent,
                PatientId: patientId,
                PractitionerId: request.PractitionerId,
                EncounterId: request.EncounterId,
                MedicationCode: request.MedicationCode,
                MedicationDisplay: request.MedicationDisplay,
                DosageInstruction: request.DosageInstruction,
                Quantity: request.Quantity,
                Unit: request.Unit,
                Refills: request.Refills,
                AuthoredOn: now,
                LastUpdated: now,
                VersionId: 1L
            )
        );
    }
}
