// Global usings for DataProvider.Tests
global using IntError = Outcome.Result<int, Selecta.SqlError>.Error<int, Selecta.SqlError>;
// Result type aliases for tests
global using IntOk = Outcome.Result<int, Selecta.SqlError>.Ok<int, Selecta.SqlError>;
global using NullableStringOk = Outcome.Result<string?, Selecta.SqlError>.Ok<
    string?,
    Selecta.SqlError
>;
global using SqlError = Selecta.SqlError;
global using StringError = Outcome.Result<string, Selecta.SqlError>.Error<string, Selecta.SqlError>;
global using StringOk = Outcome.Result<string, Selecta.SqlError>.Ok<string, Selecta.SqlError>;
