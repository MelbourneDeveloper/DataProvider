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

// Query result type aliases
global using GetUserByEmailOk = Outcome.Result<
    System.Collections.Immutable.ImmutableList<Generated.GetUserByEmail>,
    Selecta.SqlError
>.Ok<System.Collections.Immutable.ImmutableList<Generated.GetUserByEmail>, Selecta.SqlError>;

global using GetUserCredentialsOk = Outcome.Result<
    System.Collections.Immutable.ImmutableList<Generated.GetUserCredentials>,
    Selecta.SqlError
>.Ok<System.Collections.Immutable.ImmutableList<Generated.GetUserCredentials>, Selecta.SqlError>;

global using GetSessionByIdOk = Outcome.Result<
    System.Collections.Immutable.ImmutableList<Generated.GetSessionById>,
    Selecta.SqlError
>.Ok<System.Collections.Immutable.ImmutableList<Generated.GetSessionById>, Selecta.SqlError>;

global using GetUserRolesOk = Outcome.Result<
    System.Collections.Immutable.ImmutableList<Generated.GetUserRoles>,
    Selecta.SqlError
>.Ok<System.Collections.Immutable.ImmutableList<Generated.GetUserRoles>, Selecta.SqlError>;

global using GetUserPermissionsOk = Outcome.Result<
    System.Collections.Immutable.ImmutableList<Generated.GetUserPermissions>,
    Selecta.SqlError
>.Ok<System.Collections.Immutable.ImmutableList<Generated.GetUserPermissions>, Selecta.SqlError>;

global using CheckResourceGrantOk = Outcome.Result<
    System.Collections.Immutable.ImmutableList<Generated.CheckResourceGrant>,
    Selecta.SqlError
>.Ok<System.Collections.Immutable.ImmutableList<Generated.CheckResourceGrant>, Selecta.SqlError>;
