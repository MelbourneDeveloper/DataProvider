global using System.Data;
global using Microsoft.Extensions.Logging;
global using MigrationApplyResult = Outcome.Result<bool, Migration.MigrationError>;
global using OperationsResult = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Migration.SchemaOperation>,
    Migration.MigrationError
>;
// Type aliases for Result types per CLAUDE.md
