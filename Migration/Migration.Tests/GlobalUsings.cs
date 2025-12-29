global using Microsoft.Data.Sqlite;
global using Microsoft.Extensions.Logging;
global using Microsoft.Extensions.Logging.Abstractions;
global using Migration.Postgres;
global using Migration.SQLite;
global using Npgsql;
global using Testcontainers.PostgreSql;
global using Xunit;
global using MigrationApplyResultError = Outcome.Result<bool, Migration.MigrationError>.Error<
    bool,
    Migration.MigrationError
>;
global using MigrationApplyResultOk = Outcome.Result<bool, Migration.MigrationError>.Ok<
    bool,
    Migration.MigrationError
>;
global using OperationsResult = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Migration.SchemaOperation>,
    Migration.MigrationError
>;
global using OperationsResultOk = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Migration.SchemaOperation>,
    Migration.MigrationError
>.Ok<System.Collections.Generic.IReadOnlyList<Migration.SchemaOperation>, Migration.MigrationError>;
// Type aliases for Result types per CLAUDE.md
global using SchemaResultOk = Outcome.Result<
    Migration.SchemaDefinition,
    Migration.MigrationError
>.Ok<Migration.SchemaDefinition, Migration.MigrationError>;
