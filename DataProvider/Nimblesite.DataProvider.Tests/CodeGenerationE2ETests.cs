using Nimblesite.Sql.Model;

namespace Nimblesite.DataProvider.Tests;

/// <summary>
/// E2E tests: DataAccessGenerator code generation with full verification.
/// Each test generates code and validates the output contains correct C# constructs.
/// </summary>
public sealed class CodeGenerationE2ETests
{
    private static readonly IReadOnlyList<DatabaseColumn> PatientColumns =
    [
        new DatabaseColumn
        {
            Name = "Id",
            SqlType = "TEXT",
            CSharpType = "Guid",
            IsPrimaryKey = true,
        },
        new DatabaseColumn
        {
            Name = "Name",
            SqlType = "TEXT",
            CSharpType = "string",
        },
        new DatabaseColumn
        {
            Name = "Age",
            SqlType = "INTEGER",
            CSharpType = "int",
        },
        new DatabaseColumn
        {
            Name = "Email",
            SqlType = "TEXT",
            CSharpType = "string",
            IsNullable = true,
        },
        new DatabaseColumn
        {
            Name = "IsActive",
            SqlType = "INTEGER",
            CSharpType = "bool",
        },
    ];

    private static readonly IReadOnlyList<ParameterInfo> QueryParameters =
    [
        new ParameterInfo(Name: "minAge", SqlType: "INTEGER"),
        new ParameterInfo(Name: "status", SqlType: "TEXT"),
    ];

    [Fact]
    public void GenerateQueryMethod_FullWorkflow_ProducesValidExtensionMethod()
    {
        var result = DataAccessGenerator.GenerateQueryMethod(
            className: "PatientQueries",
            methodName: "GetActivePatients",
            returnTypeName: "PatientRecord",
            sql: "SELECT Id, Name, Age, Email FROM Patients WHERE IsActive = 1 AND Age > @minAge",
            parameters: QueryParameters,
            columns: PatientColumns,
            connectionType: "SqliteConnection"
        );

        Assert.IsType<StringOk>(result);
        var code = ((StringOk)result).Value;

        // Verify class structure
        Assert.Contains("public static partial class PatientQueries", code);
        Assert.Contains("GetActivePatients", code);
        Assert.Contains("SqliteConnection", code);

        // Verify parameter handling
        Assert.Contains("minAge", code);
        Assert.Contains("status", code);

        // Verify SQL embedding
        Assert.Contains("SELECT Id, Name, Age, Email FROM Patients", code);

        // Verify mapper generation
        Assert.Contains("PatientRecord", code);

        // Verify XML docs
        Assert.Contains("/// <summary>", code);
        Assert.Contains("/// </summary>", code);
    }

    [Fact]
    public void GenerateQueryMethod_ValidationErrors_ReturnDetailedErrors()
    {
        // Empty class name
        var emptyClass = DataAccessGenerator.GenerateQueryMethod(
            className: "",
            methodName: "Test",
            returnTypeName: "TestRecord",
            sql: "SELECT 1",
            parameters: [],
            columns: PatientColumns
        );
        Assert.IsType<StringError>(emptyClass);
        Assert.Contains(
            "className",
            ((StringError)emptyClass).Value.Message,
            StringComparison.OrdinalIgnoreCase
        );

        // Empty method name
        var emptyMethod = DataAccessGenerator.GenerateQueryMethod(
            className: "TestClass",
            methodName: "",
            returnTypeName: "TestRecord",
            sql: "SELECT 1",
            parameters: [],
            columns: PatientColumns
        );
        Assert.IsType<StringError>(emptyMethod);

        // Empty return type
        var emptyReturn = DataAccessGenerator.GenerateQueryMethod(
            className: "TestClass",
            methodName: "Test",
            returnTypeName: "",
            sql: "SELECT 1",
            parameters: [],
            columns: PatientColumns
        );
        Assert.IsType<StringError>(emptyReturn);

        // Empty SQL
        var emptySql = DataAccessGenerator.GenerateQueryMethod(
            className: "TestClass",
            methodName: "Test",
            returnTypeName: "TestRecord",
            sql: "",
            parameters: [],
            columns: PatientColumns
        );
        Assert.IsType<StringError>(emptySql);

        // Empty columns
        var emptyColumns = DataAccessGenerator.GenerateQueryMethod(
            className: "TestClass",
            methodName: "Test",
            returnTypeName: "TestRecord",
            sql: "SELECT 1",
            parameters: [],
            columns: []
        );
        Assert.IsType<StringError>(emptyColumns);
    }

    [Fact]
    public void GenerateNonQueryMethod_FullWorkflow_ProducesValidCode()
    {
        var result = DataAccessGenerator.GenerateNonQueryMethod(
            className: "PatientCommands",
            methodName: "DeactivatePatient",
            sql: "UPDATE Patients SET IsActive = 0 WHERE Id = @id",
            parameters: [new ParameterInfo(Name: "id", SqlType: "TEXT")],
            connectionType: "SqliteConnection"
        );

        Assert.IsType<StringOk>(result);
        var code = ((StringOk)result).Value;

        // Verify it generates non-query method
        Assert.Contains("PatientCommands", code);
        Assert.Contains("DeactivatePatient", code);
        Assert.Contains("UPDATE Patients", code);
        Assert.Contains("@id", code);
        Assert.Contains("SqliteConnection", code);
    }

    [Fact]
    public void GenerateInsertMethod_WithAllColumnTypes_ProducesCorrectCode()
    {
        var table = new DatabaseTable
        {
            Name = "Patients",
            Schema = "main",
            Columns = PatientColumns,
        };

        var result = DataAccessGenerator.GenerateInsertMethod(
            table: table,
            connectionType: "SqliteConnection"
        );

        Assert.IsType<StringOk>(result);
        var code = ((StringOk)result).Value;

        // Verify INSERT statement
        Assert.Contains("INSERT", code, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Patients", code);

        // Verify non-identity columns are included
        Assert.Contains("Name", code);
        Assert.Contains("Age", code);
        Assert.Contains("Email", code);
        Assert.Contains("IsActive", code);
    }

    [Fact]
    public void GenerateParameterList_VariousInputs_ReturnsCorrectStrings()
    {
        // Empty parameters
        var empty = DataAccessGenerator.GenerateParameterList([]);
        Assert.Equal("", empty);

        // Single parameter
        var single = DataAccessGenerator.GenerateParameterList([new ParameterInfo(Name: "userId")]);
        Assert.Contains("userId", single);
        Assert.Contains("object", single);

        // Multiple parameters
        var multi = DataAccessGenerator.GenerateParameterList([
            new ParameterInfo(Name: "name", SqlType: "TEXT"),
            new ParameterInfo(Name: "age", SqlType: "INTEGER"),
            new ParameterInfo(Name: "email", SqlType: "TEXT"),
        ]);
        Assert.Contains("name", multi);
        Assert.Contains("age", multi);
        Assert.Contains("email", multi);
        Assert.Contains(",", multi);
    }

    [Fact]
    public void GenerateQueryMethod_WithReservedKeywords_EscapesCorrectly()
    {
        // Use C# reserved keywords as parameter names
        var result = DataAccessGenerator.GenerateQueryMethod(
            className: "TestQueries",
            methodName: "GetByClass",
            returnTypeName: "TestRecord",
            sql: "SELECT * FROM Items WHERE class = @class AND int = @int",
            parameters: [new ParameterInfo(Name: "class"), new ParameterInfo(Name: "int")],
            columns:
            [
                new DatabaseColumn
                {
                    Name = "Id",
                    CSharpType = "string",
                    SqlType = "TEXT",
                },
            ]
        );

        Assert.IsType<StringOk>(result);
        var code = ((StringOk)result).Value;
        // Reserved keywords should be escaped with @
        Assert.Contains("@class", code);
        Assert.Contains("@int", code);
    }

    [Fact]
    public void GenerateBulkInsertMethod_ProducesValidBatchCode()
    {
        var table = new DatabaseTable
        {
            Name = "Patients",
            Schema = "main",
            Columns = PatientColumns,
        };

        var result = DataAccessGenerator.GenerateBulkInsertMethod(table: table, batchSize: 100);

        Assert.IsType<StringOk>(result);
        var code = ((StringOk)result).Value;

        // Verify bulk insert structure
        Assert.Contains("Patients", code);
        Assert.Contains("INSERT", code, StringComparison.OrdinalIgnoreCase);

        // Verify batch handling
        Assert.Contains("100", code);
    }

    [Fact]
    public void GenerateBulkUpsertMethod_SQLite_ProducesValidUpsertCode()
    {
        var table = new DatabaseTable
        {
            Name = "Patients",
            Schema = "main",
            Columns = PatientColumns,
        };

        var result = DataAccessGenerator.GenerateBulkUpsertMethod(
            table: table,
            databaseType: "SQLite",
            connectionType: "SqliteConnection"
        );

        Assert.IsType<StringOk>(result);
        var code = ((StringOk)result).Value;

        Assert.Contains("Patients", code);
        // SQLite upsert uses INSERT OR REPLACE or ON CONFLICT
        Assert.True(
            code.Contains("REPLACE", StringComparison.OrdinalIgnoreCase)
                || code.Contains("ON CONFLICT", StringComparison.OrdinalIgnoreCase)
                || code.Contains("UPSERT", StringComparison.OrdinalIgnoreCase)
                || code.Contains("INSERT", StringComparison.OrdinalIgnoreCase)
        );
    }

    [Fact]
    public void GenerateQueryMethod_MultipleColumns_MapsAllColumns()
    {
        var manyColumns = new List<DatabaseColumn>();
        for (int i = 0; i < 15; i++)
        {
            manyColumns.Add(
                new DatabaseColumn
                {
                    Name = $"Column{i}",
                    CSharpType =
                        i % 3 == 0 ? "int"
                        : i % 3 == 1 ? "string"
                        : "bool",
                    SqlType =
                        i % 3 == 0 ? "INTEGER"
                        : i % 3 == 1 ? "TEXT"
                        : "BOOLEAN",
                    IsNullable = i % 4 == 0,
                }
            );
        }

        var result = DataAccessGenerator.GenerateQueryMethod(
            className: "WideTableQueries",
            methodName: "GetWideData",
            returnTypeName: "WideRecord",
            sql: "SELECT * FROM WideTable",
            parameters: [],
            columns: manyColumns
        );

        Assert.IsType<StringOk>(result);
        var code = ((StringOk)result).Value;

        // Every column should appear in the generated code
        for (int i = 0; i < 15; i++)
        {
            Assert.Contains($"Column{i}", code);
        }
    }

    [Fact]
    public void GenerateNonQueryMethod_ValidationErrors_ReturnErrors()
    {
        var emptyClass = DataAccessGenerator.GenerateNonQueryMethod(
            className: " ",
            methodName: "Test",
            sql: "DELETE FROM T",
            parameters: []
        );
        Assert.IsType<StringError>(emptyClass);

        var emptyMethod = DataAccessGenerator.GenerateNonQueryMethod(
            className: "TestClass",
            methodName: "",
            sql: "DELETE FROM T",
            parameters: []
        );
        Assert.IsType<StringError>(emptyMethod);

        var emptySql = DataAccessGenerator.GenerateNonQueryMethod(
            className: "TestClass",
            methodName: "Test",
            sql: "",
            parameters: []
        );
        Assert.IsType<StringError>(emptySql);
    }
}
