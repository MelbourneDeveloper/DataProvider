// Global usings for DataProvider.Tests
global using SqlError = Selecta.SqlError;

#pragma warning disable IDE0005
// Result type aliases for tests
global using IntOk = Outcome.Result<int, Selecta.SqlError>.Ok<int, Selecta.SqlError>;
global using IntError = Outcome.Result<int, Selecta.SqlError>.Error<int, Selecta.SqlError>;
global using StringOk = Outcome.Result<string, Selecta.SqlError>.Ok<string, Selecta.SqlError>;
global using StringError = Outcome.Result<string, Selecta.SqlError>.Error<string, Selecta.SqlError>;
global using NullableStringOk = Outcome.Result<string?, Selecta.SqlError>.Ok<string?, Selecta.SqlError>;
global using NullableStringError = Outcome.Result<string?, Selecta.SqlError>.Error<string?, Selecta.SqlError>;
#pragma warning restore IDE0005
