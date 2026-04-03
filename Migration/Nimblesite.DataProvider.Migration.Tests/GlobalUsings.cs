global using Microsoft.Data.Sqlite;
global using Microsoft.Extensions.Logging;
global using Microsoft.Extensions.Logging.Abstractions;
global using Nimblesite.DataProvider.Migration.Postgres;
global using Nimblesite.DataProvider.Migration.SQLite;
global using Npgsql;
global using Testcontainers.PostgreSql;
global using Xunit;
global using Nimblesite.DataProvider.Migration.CoreApplyResultError = Outcome.Result<bool, Nimblesite.DataProvider.Migration.Core.Nimblesite.DataProvider.Migration.CoreError>.Error<
    bool,
    Nimblesite.DataProvider.Migration.Core.Nimblesite.DataProvider.Migration.CoreError
>;
global using Nimblesite.DataProvider.Migration.CoreApplyResultOk = Outcome.Result<bool, Nimblesite.DataProvider.Migration.Core.Nimblesite.DataProvider.Migration.CoreError>.Ok<
    bool,
    Nimblesite.DataProvider.Migration.Core.Nimblesite.DataProvider.Migration.CoreError
>;
global using OperationsResultOk = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Nimblesite.DataProvider.Migration.Core.SchemaOperation>,
    Nimblesite.DataProvider.Migration.Core.Nimblesite.DataProvider.Migration.CoreError
>.Ok<System.Collections.Generic.IReadOnlyList<Nimblesite.DataProvider.Migration.Core.SchemaOperation>, Nimblesite.DataProvider.Migration.Core.Nimblesite.DataProvider.Migration.CoreError>;
// Type aliases for Result types per CLAUDE.md
global using SchemaResultOk = Outcome.Result<
    Nimblesite.DataProvider.Migration.Core.SchemaDefinition,
    Nimblesite.DataProvider.Migration.Core.Nimblesite.DataProvider.Migration.CoreError
>.Ok<Nimblesite.DataProvider.Migration.Core.SchemaDefinition, Nimblesite.DataProvider.Migration.Core.Nimblesite.DataProvider.Migration.CoreError>;
