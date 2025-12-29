#pragma warning disable IDE0005 // Using directive is unnecessary

global using System.Net;
global using System.Net.Http.Json;
global using System.Text.Json;
global using Generated;
global using Microsoft.AspNetCore.Mvc.Testing;
global using Selecta;
global using Xunit;
global using GetRolePermissionsError = Outcome.Result<
    System.Collections.Immutable.ImmutableList<Generated.GetRolePermissions>,
    Selecta.SqlError
>.Error<System.Collections.Immutable.ImmutableList<Generated.GetRolePermissions>, Selecta.SqlError>;
global using GetRolePermissionsOk = Outcome.Result<
    System.Collections.Immutable.ImmutableList<Generated.GetRolePermissions>,
    Selecta.SqlError
>.Ok<System.Collections.Immutable.ImmutableList<Generated.GetRolePermissions>, Selecta.SqlError>;
global using GetPermissionByCodeOk = Outcome.Result<
    System.Collections.Immutable.ImmutableList<Generated.GetPermissionByCode>,
    Selecta.SqlError
>.Ok<System.Collections.Immutable.ImmutableList<Generated.GetPermissionByCode>, Selecta.SqlError>;
global using GetPermissionByCodeError = Outcome.Result<
    System.Collections.Immutable.ImmutableList<Generated.GetPermissionByCode>,
    Selecta.SqlError
>.Error<System.Collections.Immutable.ImmutableList<Generated.GetPermissionByCode>, Selecta.SqlError>;
global using GetSessionRevokedOk = Outcome.Result<
    System.Collections.Immutable.ImmutableList<Generated.GetSessionRevoked>,
    Selecta.SqlError
>.Ok<System.Collections.Immutable.ImmutableList<Generated.GetSessionRevoked>, Selecta.SqlError>;
global using GetSessionRevokedError = Outcome.Result<
    System.Collections.Immutable.ImmutableList<Generated.GetSessionRevoked>,
    Selecta.SqlError
>.Error<System.Collections.Immutable.ImmutableList<Generated.GetSessionRevoked>, Selecta.SqlError>;
