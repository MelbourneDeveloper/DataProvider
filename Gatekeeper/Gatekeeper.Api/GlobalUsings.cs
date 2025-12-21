#pragma warning disable IDE0005 // Using directive is unnecessary (some are unused but needed for tests)

global using System;
global using System.Collections.Immutable;
global using System.Globalization;
global using System.Text.Json;
global using Fido2NetLib;
global using Fido2NetLib.Objects;
global using Generated;
global using Microsoft.Data.Sqlite;
global using Microsoft.Extensions.Logging;
global using Outcome;
global using Selecta;
global using CheckResourceGrantOk = Outcome.Result<
    System.Collections.Immutable.ImmutableList<Generated.CheckResourceGrant>,
    Selecta.SqlError
>.Ok<System.Collections.Immutable.ImmutableList<Generated.CheckResourceGrant>, Selecta.SqlError>;
global using GetChallengeByIdOk = Outcome.Result<
    System.Collections.Immutable.ImmutableList<Generated.GetChallengeById>,
    Selecta.SqlError
>.Ok<System.Collections.Immutable.ImmutableList<Generated.GetChallengeById>, Selecta.SqlError>;
global using GetSessionByIdOk = Outcome.Result<
    System.Collections.Immutable.ImmutableList<Generated.GetSessionById>,
    Selecta.SqlError
>.Ok<System.Collections.Immutable.ImmutableList<Generated.GetSessionById>, Selecta.SqlError>;
// Query result type aliases
global using GetUserByEmailOk = Outcome.Result<
    System.Collections.Immutable.ImmutableList<Generated.GetUserByEmail>,
    Selecta.SqlError
>.Ok<System.Collections.Immutable.ImmutableList<Generated.GetUserByEmail>, Selecta.SqlError>;
// Additional query result type aliases
global using GetCredentialByIdOk = Outcome.Result<
    System.Collections.Immutable.ImmutableList<Generated.GetCredentialById>,
    Selecta.SqlError
>.Ok<System.Collections.Immutable.ImmutableList<Generated.GetCredentialById>, Selecta.SqlError>;
global using GetUserByIdOk = Outcome.Result<
    System.Collections.Immutable.ImmutableList<Generated.GetUserById>,
    Selecta.SqlError
>.Ok<System.Collections.Immutable.ImmutableList<Generated.GetUserById>, Selecta.SqlError>;
global using GetUserCredentialsOk = Outcome.Result<
    System.Collections.Immutable.ImmutableList<Generated.GetUserCredentials>,
    Selecta.SqlError
>.Ok<System.Collections.Immutable.ImmutableList<Generated.GetUserCredentials>, Selecta.SqlError>;
global using GetUserPermissionsOk = Outcome.Result<
    System.Collections.Immutable.ImmutableList<Generated.GetUserPermissions>,
    Selecta.SqlError
>.Ok<System.Collections.Immutable.ImmutableList<Generated.GetUserPermissions>, Selecta.SqlError>;
global using GetUserRolesOk = Outcome.Result<
    System.Collections.Immutable.ImmutableList<Generated.GetUserRoles>,
    Selecta.SqlError
>.Ok<System.Collections.Immutable.ImmutableList<Generated.GetUserRoles>, Selecta.SqlError>;
// Insert result type alias
global using InsertOk = Outcome.Result<long, Selecta.SqlError>.Ok<long, Selecta.SqlError>;
