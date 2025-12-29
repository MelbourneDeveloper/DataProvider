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
