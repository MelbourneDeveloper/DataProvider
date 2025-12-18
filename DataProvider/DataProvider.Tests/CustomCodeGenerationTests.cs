using System.Collections.Frozen;
using DataProvider.CodeGeneration;
using DataProvider.SQLite;
using Outcome;
using Selecta;
using Xunit;

#pragma warning disable CA1307 // Specify StringComparison for clarity

namespace DataProvider.Tests;

/// <summary>
/// Tests demonstrating custom code generation with completely different output styles
/// </summary>
public class CustomCodeGenerationTests
{
    private static readonly List<DatabaseColumn> TestColumns = new()
    {
        new DatabaseColumn
        {
            Name = "Id",
            CSharpType = "int",
            SqlType = "INTEGER",
            IsNullable = false,
            IsPrimaryKey = true,
            IsIdentity = true,
            IsComputed = false,
        },
        new DatabaseColumn
        {
            Name = "Name",
            CSharpType = "string",
            SqlType = "TEXT",
            IsNullable = false,
            IsPrimaryKey = false,
            IsIdentity = false,
            IsComputed = false,
        },
        new DatabaseColumn
        {
            Name = "Email",
            CSharpType = "string?",
            SqlType = "TEXT",
            IsNullable = true,
            IsPrimaryKey = false,
            IsIdentity = false,
            IsComputed = false,
        },
    };

    private static readonly SelectStatement TestStatement = new()
    {
        Parameters = new[] { new ParameterInfo("userId", "INTEGER") }.ToFrozenSet(),
    };

    private static Task<Result<IReadOnlyList<DatabaseColumn>, SqlError>> MockGetColumnMetadata(
        string connectionString,
        string sql,
        IEnumerable<ParameterInfo> parameters
    ) =>
        Task.FromResult<Result<IReadOnlyList<DatabaseColumn>, SqlError>>(
            new Result<IReadOnlyList<DatabaseColumn>, SqlError>.Ok<IReadOnlyList<DatabaseColumn>, SqlError>(TestColumns)
        );

    [Fact]
    public void CustomModelGenerator_GeneratesClassesInsteadOfRecords()
    {
        // Custom model generator that creates mutable classes instead of records
        static Result<string, SqlError> CustomModelGenerator(
            string typeName,
            IReadOnlyList<DatabaseColumn> columns
        )
        {
            var code =
                $@"/// <summary>
/// Mutable data class for {typeName} (Custom Style)
/// </summary>
public class {typeName}Data
{{
{string.Join("\n", columns.Select(c => $"    public {c.CSharpType} {c.Name} {{ get; set; }}"))}

    public {typeName}Data() {{ }}
    
    public {typeName}Data Clone() => new {typeName}Data
    {{
{string.Join(",\n", columns.Select(c => $"        {c.Name} = this.{c.Name}"))}
    }};
}}";
            return new Result<string, SqlError>.Ok<string, SqlError>(code);
        }

        var config = new CodeGenerationConfig(MockGetColumnMetadata)
        {
            GenerateModelType = CustomModelGenerator,
            TargetNamespace = "CustomGenerated",
        };

        var result = SqliteCodeGenerator.GenerateCodeWithMetadata(
            "User",
            "SELECT Id, Name, Email FROM Users WHERE Id = @userId",
            TestStatement,
            "test.db",
            TestColumns,
            false,
            null,
            config
        );

        Assert.True(result is Result<string, SqlError>.Ok<string, SqlError>);
        var generatedCode = (result as Result<string, SqlError>.Ok<string, SqlError>)!.Value;

        var expectedCode =
            @"using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Outcome;

namespace CustomGenerated;

/// <summary>
/// Extension methods for 'User'.
/// </summary>
public static partial class UserExtensions
{
    /// <summary>
    /// Executes 'User.sql' and maps results.
    /// </summary>
    /// <param name=""connection"">The open SqliteConnection connection.</param>
    /// <param name=""userId"">Query parameter.</param>
    /// <returns>Result of records or SQL error.</returns>
    public static async Task<Result<ImmutableList<User>, SqlError>> UserAsync(this SqliteConnection connection, object userId)
    {
        const string sql = @""SELECT Id, Name, Email FROM Users WHERE Id = @userId"";

        try
        {
            var results = ImmutableList.CreateBuilder<User>();

            using (var command = new SqliteCommand(sql, connection))
            {
                command.Parameters.AddWithValue(""@userId"", userId ?? (object)DBNull.Value);

                using (var reader = await command.ExecuteReaderAsync().ConfigureAwait(false))
                {
                    while (await reader.ReadAsync().ConfigureAwait(false))
                    {
                        var item = new User(
                            reader.IsDBNull(0) ? default(int) : (int)reader.GetValue(0),
                            reader.IsDBNull(1) ? default(string) : (string)reader.GetValue(1),
                            reader.IsDBNull(2) ? null : (string?)reader.GetValue(2)
                        );
                        results.Add(item);
                    }
                }
            }

            return new Result<ImmutableList<User>, SqlError>.Ok(results.ToImmutable());
        }
        catch (Exception ex)
        {
            return new Result<ImmutableList<User>, SqlError>.Error(new SqlError(""Database error"", ex));
        }
    }
}

/// <summary>
/// Mutable data class for User (Custom Style)
/// </summary>
public class UserData
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string? Email { get; set; }

    public UserData() { }
    
    public UserData Clone() => new UserData
    {
        Id = this.Id,
        Name = this.Name,
        Email = this.Email
    };
}";

        // This is a snapshot/golden test - ensure exact match with generated output
        expectedCode =
            @"using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Outcome;

namespace CustomGenerated;

/// <summary>
/// Extension methods for 'User'.
/// </summary>
public static partial class UserExtensions
{
    /// <summary>
    /// Executes 'User.sql' and maps results.
    /// </summary>
    /// <param name=""connection"">Open SqliteConnection connection.</param>
    /// <param name=""userId"">Query parameter.</param>
    /// <returns>Result of records or SQL error.</returns>
    public static async Task<Result<ImmutableList<User>, SqlError>> UserAsync(this SqliteConnection connection, object userId)
    {
        const string sql = @""SELECT Id, Name, Email FROM Users WHERE Id = @userId"";

        try
        {
            var results = ImmutableList.CreateBuilder<User>();

            using (var command = new SqliteCommand(sql, connection))
            {
                command.Parameters.AddWithValue(""@userId"", userId ?? (object)DBNull.Value);

                using (var reader = await command.ExecuteReaderAsync().ConfigureAwait(false))
                {
                    while (await reader.ReadAsync().ConfigureAwait(false))
                    {
                        var item = new User(
                            reader.IsDBNull(0) ? default(int) : (int)reader.GetValue(0),
                            reader.IsDBNull(1) ? default(string) : (string)reader.GetValue(1),
                            reader.IsDBNull(2) ? null : (string?)reader.GetValue(2)
                        );
                        results.Add(item);
                    }
                }
            }

            return new Result<ImmutableList<User>, SqlError>.Ok(results.ToImmutable());
        }
        catch (Exception ex)
        {
            return new Result<ImmutableList<User>, SqlError>.Error(new SqlError(""Database error"", ex));
        }
    }
}

/// <summary>
/// Mutable data class for User (Custom Style)
/// </summary>
public class UserData
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string? Email { get; set; }

    public UserData() { }
    
    public UserData Clone() => new UserData
    {
        Id = this.Id,
        Name = this.Name,
        Email = this.Email
    };
}";

        Assert.Equal(expectedCode, generatedCode, ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void CustomDataAccessGenerator_GeneratesFluentAPI()
    {
        // Custom data access generator that creates fluent API instead of extension methods
        static Result<string, SqlError> CustomDataAccessGenerator(
            string className,
            string methodName,
            string sql,
            IReadOnlyList<ParameterInfo> parameters,
            IReadOnlyList<DatabaseColumn> columns,
            string connectionType
        )
        {
            var paramList = string.Join(
                ", ",
                parameters.Select(p => $"{p.SqlType.ToLowerInvariant()} {p.Name}")
            );

            var code =
                $@"/// <summary>
/// Fluent query builder for {methodName} (Custom Style)
/// </summary>
public static class {methodName}QueryBuilder
{{
    public static {methodName}Query Create() => new {methodName}Query();
}}

public class {methodName}Query
{{
    private readonly Dictionary<string, object> _parameters = new();
    
{string.Join("\n", parameters.Select(p => $@"    public {methodName}Query With{p.Name.ToTitleCase()}({p.SqlType.ToLowerInvariant()} {p.Name})
    {{
        _parameters[""{p.Name}""] = {p.Name};
        return this;
    }}"))}

    public async Task<List<{methodName}Data>> ExecuteAsync({connectionType} connection)
    {{
        const string sql = @""{sql.Replace("\"", "\"\"")}"";
        var results = new List<{methodName}Data>();
        
        using var command = new SqliteCommand(sql, connection);
        foreach (var param in _parameters)
            command.Parameters.AddWithValue($""@{{param.Key}}"", param.Value);
            
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {{
            results.Add(new {methodName}Data
            {{
{string.Join(",\n", columns.Select((c, i) => $"                {c.Name} = reader.IsDBNull({i}) ? {(c.IsNullable ? "null" : $"default({c.CSharpType})")} : ({c.CSharpType})reader.GetValue({i})"))}
            }});
        }}
        
        return results;
    }}
}}
";
            return new Result<string, SqlError>.Ok<string, SqlError>(code);
        }

        var config = new CodeGenerationConfig(MockGetColumnMetadata)
        {
            GenerateDataAccessMethod = CustomDataAccessGenerator,
            GenerateModelType = (typeName, columns) =>
                new Result<string, SqlError>.Ok<string, SqlError>($"// Model for {typeName} would be here"),
            TargetNamespace = "FluentGenerated",
        };

        var result = SqliteCodeGenerator.GenerateCodeWithMetadata(
            "User",
            "SELECT Id, Name, Email FROM Users WHERE Id = @userId",
            TestStatement,
            "test.db",
            TestColumns,
            false,
            null,
            config
        );

        Assert.True(result is Result<string, SqlError>.Ok<string, SqlError>);
        var generatedCode = (result as Result<string, SqlError>.Ok<string, SqlError>)!.Value;

        var expectedCode =
            @"using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Outcome;

namespace FluentGenerated;

/// <summary>
/// Fluent query builder for User (Custom Style)
/// </summary>
public static class UserQueryBuilder
{
    public static UserQuery Create() => new UserQuery();
}

public class UserQuery
{
    private readonly Dictionary<string, object> _parameters = new();
    
    public UserQuery WithUserId(integer userId)
    {
        _parameters[""userId""] = userId;
        return this;
    }

    public async Task<List<UserData>> ExecuteAsync(SqliteConnection connection)
    {
        const string sql = @""SELECT Id, Name, Email FROM Users WHERE Id = @userId"";
        var results = new List<UserData>();
        
        using var command = new SqliteCommand(sql, connection);
        foreach (var param in _parameters)
            command.Parameters.AddWithValue($""@{param.Key}"", param.Value);
            
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new UserData
            {
                Id = reader.IsDBNull(0) ? default(int) : (int)reader.GetValue(0),
                Name = reader.IsDBNull(1) ? default(string) : (string)reader.GetValue(1),
                Email = reader.IsDBNull(2) ? null : (string?)reader.GetValue(2)
            });
        }
        
        return results;
    }
}

// Model for User would be here";

        Assert.Equal(expectedCode, generatedCode, ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void CustomSourceFileGenerator_GeneratesModernMinimalAPI()
    {
        // Custom source file generator that creates minimal API endpoints
        static Result<string, SqlError> CustomSourceFileGenerator(
            string namespaceName,
            string modelCode,
            string dataAccessCode
        )
        {
            var code =
                $@"// <auto-generated />
#nullable enable
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;

namespace {namespaceName}.Controllers;

[ApiController]
[Route(""api/[controller]"")]
public class DataController : ControllerBase
{{
    private readonly string _connectionString;
    
    public DataController(IConfiguration config)
    {{
        _connectionString = config.GetConnectionString(""Default"") ?? throw new InvalidOperationException();
    }}
    
{dataAccessCode}
}}

// Data Transfer Objects
{modelCode}";
            return new Result<string, SqlError>.Ok<string, SqlError>(code);
        }

        static Result<string, SqlError> ApiModelGenerator(
            string typeName,
            IReadOnlyList<DatabaseColumn> columns
        )
        {
            var code =
                $@"public record {typeName}Dto(
{string.Join(",\n", columns.Select(c => $"    {c.CSharpType} {c.Name}"))});

public record Create{typeName}Request(
{string.Join(",\n", columns.Where(c => !c.IsIdentity).Select(c => $"    {c.CSharpType} {c.Name}"))});";
            return new Result<string, SqlError>.Ok<string, SqlError>(code);
        }

        static Result<string, SqlError> ApiDataAccessGenerator(
            string className,
            string methodName,
            string sql,
            IReadOnlyList<ParameterInfo> parameters,
            IReadOnlyList<DatabaseColumn> columns,
            string connectionType
        )
        {
            var paramList = string.Join(
                ", ",
                parameters.Select(p => $"[FromQuery] {p.SqlType.ToLowerInvariant()} {p.Name}")
            );

            var code =
                $@"    [HttpGet(""{methodName.ToLowerInvariant()}"")]
    public async Task<ActionResult<List<{methodName}Dto>>> Get{methodName}({paramList})
    {{
        const string sql = @""{sql.Replace("\"", "\"\"")}"";
        var results = new List<{methodName}Dto>();
        
        using var connection = new {connectionType}(_connectionString);
        await connection.OpenAsync();
        
        using var command = new SqliteCommand(sql, connection);
{string.Join("\n", parameters.Select(p => $"        command.Parameters.AddWithValue(\"@{p.Name}\", {p.Name});"))}
        
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {{
            results.Add(new {methodName}Dto(
{string.Join(",\n", columns.Select((c, i) => $"                reader.IsDBNull({i}) ? default({c.CSharpType}) : ({c.CSharpType})reader.GetValue({i})"))}
            ));
        }}
        
        return Ok(results);
    }}";
            return new Result<string, SqlError>.Ok<string, SqlError>(code);
        }

        var config = new CodeGenerationConfig(MockGetColumnMetadata)
        {
            GenerateSourceFile = CustomSourceFileGenerator,
            GenerateModelType = ApiModelGenerator,
            GenerateDataAccessMethod = ApiDataAccessGenerator,
            TargetNamespace = "MyAPI",
        };

        var result = SqliteCodeGenerator.GenerateCodeWithMetadata(
            "User",
            "SELECT Id, Name, Email FROM Users WHERE Id = @userId",
            TestStatement,
            "test.db",
            TestColumns,
            false,
            null,
            config
        );

        Assert.True(result is Result<string, SqlError>.Ok<string, SqlError>);
        var generatedCode = (result as Result<string, SqlError>.Ok<string, SqlError>)!.Value;

        var expectedCode =
            @"// <auto-generated />
#nullable enable
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;

namespace MyAPI.Controllers;

[ApiController]
[Route(""api/[controller]"")]
public class DataController : ControllerBase
{
    private readonly string _connectionString;
    
    public DataController(IConfiguration config)
    {
        _connectionString = config.GetConnectionString(""Default"") ?? throw new InvalidOperationException();
    }
    
    [HttpGet(""user"")]
    public async Task<ActionResult<List<UserDto>>> GetUser([FromQuery] integer userId)
    {
        const string sql = @""SELECT Id, Name, Email FROM Users WHERE Id = @userId"";
        var results = new List<UserDto>();
        
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        
        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue(""@userId"", userId);
        
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new UserDto(
                reader.IsDBNull(0) ? default(int) : (int)reader.GetValue(0),
                reader.IsDBNull(1) ? default(string) : (string)reader.GetValue(1),
                reader.IsDBNull(2) ? default(string?) : (string?)reader.GetValue(2)
            ));
        }
        
        return Ok(results);
    }
}

// Data Transfer Objects
public record UserDto(
    int Id,
    string Name,
    string? Email);

public record CreateUserRequest(
    string Name,
    string? Email);";

        Assert.Equal(expectedCode, generatedCode, ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void CustomTableOperationGenerator_GeneratesRepositoryPattern()
    {
        var customTableGenerator = new RepositoryPatternTableOperationGenerator();

        var table = new DatabaseTable { Name = "Users", Columns = TestColumns };

        var config = new TableConfig { GenerateInsert = true, GenerateUpdate = true };

        var result = customTableGenerator.GenerateTableOperations(table, config);

        Assert.True(result is Result<string, SqlError>.Ok<string, SqlError>);
        var generatedCode = (result as Result<string, SqlError>.Ok<string, SqlError>)!.Value;

        var expectedCode =
            @"using Microsoft.Data.Sqlite;

namespace Generated.Repositories;

public interface IUsersRepository
{
    Task<UserEntity> CreateAsync(UserEntity entity);
    Task<UserEntity> UpdateAsync(UserEntity entity);
    Task<UserEntity?> GetByIdAsync(int id);
    Task<List<UserEntity>> GetAllAsync();
}

public class UsersRepository : IUsersRepository
{
    private readonly string _connectionString;
    
    public UsersRepository(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public async Task<UserEntity> CreateAsync(UserEntity entity)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        
        var sql = ""INSERT INTO Users (Name, Email) VALUES (@Name, @Email); SELECT last_insert_rowid()"";
        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue(""@Name"", entity.Name);
        command.Parameters.AddWithValue(""@Email"", entity.Email);
        
        var newId = Convert.ToInt32(await command.ExecuteScalarAsync());
        return entity with { Id = newId };
    }
    public async Task<UserEntity> UpdateAsync(UserEntity entity)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        
        var sql = ""UPDATE Users SET Name = @Name, Email = @Email WHERE Id = @Id"";
        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue(""@Id"", entity.Id);
        command.Parameters.AddWithValue(""@Name"", entity.Name);
        command.Parameters.AddWithValue(""@Email"", entity.Email);
        
        await command.ExecuteNonQueryAsync();
        return entity;
    }

    public async Task<UserEntity?> GetByIdAsync(int id)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        
        var sql = ""SELECT Id, Name, Email FROM Users WHERE Id = @id"";
        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue(""@id"", id);
        
        using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;
        
        return new UserEntity(
            reader.IsDBNull(0) ? default(int) : (int)reader.GetValue(0),
            reader.IsDBNull(1) ? default(string) : (string)reader.GetValue(1),
            reader.IsDBNull(2) ? null : (string?)reader.GetValue(2)
        );
    }
    
    public async Task<List<UserEntity>> GetAllAsync()
    {
        var results = new List<UserEntity>();
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        
        var sql = ""SELECT Id, Name, Email FROM Users"";
        using var command = new SqliteCommand(sql, connection);
        
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new UserEntity(
                reader.IsDBNull(0) ? default(int) : (int)reader.GetValue(0),
                reader.IsDBNull(1) ? default(string) : (string)reader.GetValue(1),
                reader.IsDBNull(2) ? null : (string?)reader.GetValue(2)
            ));
        }
        
        return results;
    }
}

public record UserEntity(
    int Id,
    string Name,
    string? Email);";

        Assert.Equal(expectedCode, generatedCode, ignoreLineEndingDifferences: true);
    }
}

/// <summary>
/// Custom table operation generator that creates repository pattern instead of extension methods
/// </summary>
internal sealed class RepositoryPatternTableOperationGenerator : ITableOperationGenerator
{
    public Result<string, SqlError> GenerateTableOperations(DatabaseTable table, TableConfig config)
    {
        if (table == null)
            return new Result<string, SqlError>.Error<string, SqlError>(new SqlError("table cannot be null"));

        if (config == null)
            return new Result<string, SqlError>.Error<string, SqlError>(new SqlError("config cannot be null"));

        var entityName = $"{table.Name.TrimEnd('s')}Entity";
        var repositoryInterface = $"I{table.Name}Repository";
        var repositoryClass = $"{table.Name}Repository";

        var code =
            $@"using Microsoft.Data.Sqlite;

namespace Generated.Repositories;

public interface {repositoryInterface}
{{
{(config.GenerateInsert ? $"    Task<{entityName}> CreateAsync({entityName} entity);" : "")}
{(config.GenerateUpdate ? $"    Task<{entityName}> UpdateAsync({entityName} entity);" : "")}
    Task<{entityName}?> GetByIdAsync(int id);
    Task<List<{entityName}>> GetAllAsync();
}}

public class {repositoryClass} : {repositoryInterface}
{{
    private readonly string _connectionString;
    
    public {repositoryClass}(string connectionString)
    {{
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }}

{(config.GenerateInsert ? GenerateRepositoryInsertMethod(table, entityName) : "")}
{(config.GenerateUpdate ? GenerateRepositoryUpdateMethod(table, entityName) : "")}

    public async Task<{entityName}?> GetByIdAsync(int id)
    {{
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        
        var sql = ""SELECT {string.Join(", ", table.Columns.Select(c => c.Name))} FROM {table.Name} WHERE Id = @id"";
        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue(""@id"", id);
        
        using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;
        
        return new {entityName}(
{string.Join(",\n", table.Columns.Select((c, i) => $"            reader.IsDBNull({i}) ? {(c.IsNullable ? "null" : $"default({c.CSharpType})")} : ({c.CSharpType})reader.GetValue({i})"))}
        );
    }}
    
    public async Task<List<{entityName}>> GetAllAsync()
    {{
        var results = new List<{entityName}>();
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        
        var sql = ""SELECT {string.Join(", ", table.Columns.Select(c => c.Name))} FROM {table.Name}"";
        using var command = new SqliteCommand(sql, connection);
        
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {{
            results.Add(new {entityName}(
{string.Join(",\n", table.Columns.Select((c, i) => $"                reader.IsDBNull({i}) ? {(c.IsNullable ? "null" : $"default({c.CSharpType})")} : ({c.CSharpType})reader.GetValue({i})"))}
            ));
        }}
        
        return results;
    }}
}}

public record {entityName}(
{string.Join(",\n", table.Columns.Select(c => $"    {c.CSharpType} {c.Name}"))});";

        return new Result<string, SqlError>.Ok<string, SqlError>(code);
    }

    public Result<string, SqlError> GenerateInsertMethod(DatabaseTable table) =>
        new Result<string, SqlError>.Ok<string, SqlError>("// Insert handled by repository pattern");

    public Result<string, SqlError> GenerateUpdateMethod(DatabaseTable table) =>
        new Result<string, SqlError>.Ok<string, SqlError>("// Update handled by repository pattern");

    private static string GenerateRepositoryInsertMethod(DatabaseTable table, string entityName)
    {
        var insertableColumns = table.InsertableColumns;
        var columnNames = string.Join(", ", insertableColumns.Select(c => c.Name));
        var parameterNames = string.Join(", ", insertableColumns.Select(c => $"@{c.Name}"));

        return $@"    public async Task<{entityName}> CreateAsync({entityName} entity)
    {{
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        
        var sql = ""INSERT INTO {table.Name} ({columnNames}) VALUES ({parameterNames}); SELECT last_insert_rowid()"";
        using var command = new SqliteCommand(sql, connection);
{string.Join("\n", insertableColumns.Select(c => $"        command.Parameters.AddWithValue(\"@{c.Name}\", entity.{c.Name});"))}
        
        var newId = Convert.ToInt32(await command.ExecuteScalarAsync());
        return entity with {{ Id = newId }};
    }}";
    }

    private static string GenerateRepositoryUpdateMethod(DatabaseTable table, string entityName)
    {
        var updateableColumns = table.UpdateableColumns;
        var primaryKeyColumns = table.PrimaryKeyColumns;
        var setClause = string.Join(", ", updateableColumns.Select(c => $"{c.Name} = @{c.Name}"));
        var whereClause = string.Join(
            " AND ",
            primaryKeyColumns.Select(c => $"{c.Name} = @{c.Name}")
        );

        return $@"    public async Task<{entityName}> UpdateAsync({entityName} entity)
    {{
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        
        var sql = ""UPDATE {table.Name} SET {setClause} WHERE {whereClause}"";
        using var command = new SqliteCommand(sql, connection);
{string.Join("\n", table.Columns.Select(c => $"        command.Parameters.AddWithValue(\"@{c.Name}\", entity.{c.Name});"))}
        
        await command.ExecuteNonQueryAsync();
        return entity;
    }}";
    }
}

internal static class StringExtensions
{
    public static string ToTitleCase(this string input) =>
        string.IsNullOrEmpty(input)
            ? input
            : char.ToUpper(input[0], System.Globalization.CultureInfo.InvariantCulture)
                + input[1..];
}
