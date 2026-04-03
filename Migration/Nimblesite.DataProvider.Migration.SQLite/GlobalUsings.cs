global using System.Data;
global using System.Text;
global using Microsoft.Data.Sqlite;
global using Microsoft.Extensions.Logging;
// Type aliases
global using SchemaResult = Outcome.Result<Nimblesite.DataProvider.Migration.Core.SchemaDefinition, Nimblesite.DataProvider.Migration.Core.Nimblesite.DataProvider.Migration.CoreError>;
global using TableResult = Outcome.Result<Nimblesite.DataProvider.Migration.Core.TableDefinition, Nimblesite.DataProvider.Migration.Core.Nimblesite.DataProvider.Migration.CoreError>;
