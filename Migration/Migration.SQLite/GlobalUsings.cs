global using System.Data;
global using System.Text;
global using Microsoft.Data.Sqlite;
global using Microsoft.Extensions.Logging;
// Type aliases
global using SchemaResult = Outcome.Result<Migration.SchemaDefinition, Migration.MigrationError>;
global using TableResult = Outcome.Result<Migration.TableDefinition, Migration.MigrationError>;
