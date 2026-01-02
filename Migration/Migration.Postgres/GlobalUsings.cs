global using System.Data;
global using System.Text;
global using Microsoft.Extensions.Logging;
global using Npgsql;
// Type aliases
global using SchemaResult = Outcome.Result<Migration.SchemaDefinition, Migration.MigrationError>;
global using TableResult = Outcome.Result<Migration.TableDefinition, Migration.MigrationError>;
