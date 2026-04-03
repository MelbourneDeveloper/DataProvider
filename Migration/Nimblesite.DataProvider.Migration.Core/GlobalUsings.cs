global using System.Data;
global using Microsoft.Extensions.Logging;
global using Nimblesite.DataProvider.Migration.CoreApplyResult = Outcome.Result<bool, Nimblesite.DataProvider.Migration.Core.Nimblesite.DataProvider.Migration.CoreError>;
global using OperationsResult = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Nimblesite.DataProvider.Migration.Core.SchemaOperation>,
    Nimblesite.DataProvider.Migration.Core.Nimblesite.DataProvider.Migration.CoreError
>;
// Type aliases for Result types per CLAUDE.md
