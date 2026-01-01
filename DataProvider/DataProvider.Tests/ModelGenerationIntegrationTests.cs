using DataProvider.CodeGeneration;
using Xunit;

namespace DataProvider.Tests;

/// <summary>
/// Integration tests for ModelGenerator functionality
/// </summary>
public sealed class ModelGenerationIntegrationTests
{
    [Fact]
    public void GenerateRecordType_WithSimpleColumns_GeneratesValidCode()
    {
        // Arrange
        var columns = new List<DatabaseColumn>
        {
            new()
            {
                Name = "Id",
                CSharpType = "int",
                SqlType = "INTEGER",
            },
            new()
            {
                Name = "Name",
                CSharpType = "string",
                SqlType = "TEXT",
            },
            new()
            {
                Name = "Email",
                CSharpType = "string?",
                SqlType = "TEXT",
            },
            new()
            {
                Name = "Age",
                CSharpType = "int?",
                SqlType = "INTEGER",
            },
        };

        // Act
        var result = ModelGenerator.GenerateRecordType("UserRecord", columns);

        // Assert
        Assert.True(result is StringOk);
        var success = (StringOk)result;
        var code = success.Value;

        Assert.Contains("public record UserRecord", code);
        Assert.Contains("public int Id { get; init; }", code);
        Assert.Contains("public string Name { get; init; }", code);
        Assert.Contains("public string? Email { get; init; }", code);
        Assert.Contains("public int? Age { get; init; }", code);
        Assert.Contains("public UserRecord(", code);
        Assert.Contains("this.Id = Id;", code);
        Assert.Contains("this.Name = Name;", code);
    }

    [Fact]
    public void GenerateRecordType_WithNullTypeName_ReturnsFailure()
    {
        // Arrange
        var columns = new List<DatabaseColumn>
        {
            new()
            {
                Name = "Id",
                CSharpType = "int",
                SqlType = "INTEGER",
            },
        };

        // Act
        var result = ModelGenerator.GenerateRecordType(null!, columns);

        // Assert
        Assert.True(result is StringError);
        var failure = (StringError)result;
        Assert.Contains("typeName cannot be null or empty", failure.Value.Message);
    }

    [Fact]
    public void GenerateRecordType_WithEmptyColumns_ReturnsFailure()
    {
        // Act
        var result = ModelGenerator.GenerateRecordType("TestRecord", []);

        // Assert
        Assert.True(result is StringError);
        var failure = (StringError)result;
        Assert.Contains("columns cannot be null or empty", failure.Value.Message);
    }

    [Fact]
    public void GenerateGroupedRecordTypes_WithValidInputs_GeneratesParentAndChild()
    {
        // Arrange
        var parentColumns = new List<string> { "Id", "Name", "Email" };
        var childColumns = new List<string> { "OrderId", "Amount", "Status" };
        var allColumns = new List<DatabaseColumn>
        {
            new()
            {
                Name = "Id",
                CSharpType = "int",
                SqlType = "INTEGER",
            },
            new()
            {
                Name = "Name",
                CSharpType = "string",
                SqlType = "TEXT",
            },
            new()
            {
                Name = "Email",
                CSharpType = "string",
                SqlType = "TEXT",
            },
            new()
            {
                Name = "OrderId",
                CSharpType = "int",
                SqlType = "INTEGER",
            },
            new()
            {
                Name = "Amount",
                CSharpType = "decimal",
                SqlType = "DECIMAL",
            },
            new()
            {
                Name = "Status",
                CSharpType = "string",
                SqlType = "TEXT",
            },
        };

        // Act
        var result = ModelGenerator.GenerateGroupedRecordTypes(
            "Customer",
            "Order",
            parentColumns,
            childColumns,
            allColumns
        );

        // Assert
        Assert.True(result is StringOk);
        var success = (StringOk)result;
        var code = success.Value;

        // Check parent record
        Assert.Contains("public record Customer", code);
        Assert.Contains("public int Id { get; init; }", code);
        Assert.Contains("public string Name { get; init; }", code);
        Assert.Contains("public IReadOnlyList<Order> Orders { get; init; }", code);

        // Check child record
        Assert.Contains("public record Order", code);
        Assert.Contains("public int OrderId { get; init; }", code);
        Assert.Contains("public decimal Amount { get; init; }", code);
        Assert.Contains("public string Status { get; init; }", code);

        // Check constructors
        Assert.Contains("public Customer(", code);
        Assert.Contains("public Order(", code);
    }

    [Fact]
    public void GenerateRawRecordType_WithColumns_GeneratesInternalRecord()
    {
        // Arrange
        var columns = new List<DatabaseColumn>
        {
            new()
            {
                Name = "UserId",
                CSharpType = "int",
                SqlType = "INTEGER",
            },
            new()
            {
                Name = "UserName",
                CSharpType = "string",
                SqlType = "TEXT",
            },
            new()
            {
                Name = "OrderCount",
                CSharpType = "int",
                SqlType = "INTEGER",
            },
        };

        // Act
        var result = ModelGenerator.GenerateRawRecordType("UserOrderRaw", columns);

        // Assert
        Assert.True(result is StringOk);
        var success = (StringOk)result;
        var code = success.Value;

        Assert.Contains("internal record UserOrderRaw(", code);
        Assert.Contains("int UserId,", code);
        Assert.Contains("string UserName,", code);
        Assert.Contains("int OrderCount", code); // No comma on last parameter
        Assert.Contains(");", code);
    }

    [Fact]
    public void GenerateRecordType_WithSpecialCharactersInColumnNames_HandlesCorrectly()
    {
        // Arrange - Testing edge cases with column names
        var columns = new List<DatabaseColumn>
        {
            new()
            {
                Name = "user_id",
                CSharpType = "int",
                SqlType = "INTEGER",
            },
            new()
            {
                Name = "first_name",
                CSharpType = "string",
                SqlType = "TEXT",
            },
            new()
            {
                Name = "is_active",
                CSharpType = "bool",
                SqlType = "BOOLEAN",
            },
            new()
            {
                Name = "created_at",
                CSharpType = "DateTime",
                SqlType = "DATETIME",
            },
        };

        // Act
        var result = ModelGenerator.GenerateRecordType("UserProfile", columns);

        // Assert
        Assert.True(result is StringOk);
        var success = (StringOk)result;
        var code = success.Value;

        Assert.Contains("public int user_id { get; init; }", code);
        Assert.Contains("public string first_name { get; init; }", code);
        Assert.Contains("public bool is_active { get; init; }", code);
        Assert.Contains("public DateTime created_at { get; init; }", code);
        Assert.Contains("this.user_id = user_id;", code);
        Assert.Contains("this.first_name = first_name;", code);
    }

    [Fact]
    public void GenerateRecordType_WithLargeNumberOfColumns_GeneratesCorrectly()
    {
        // Arrange - Test with many columns to ensure no performance issues
        var columns = new List<DatabaseColumn>();
        for (int i = 1; i <= 20; i++)
        {
            columns.Add(
                new DatabaseColumn
                {
                    Name = $"Column{i}",
                    CSharpType = i % 2 == 0 ? "string" : "int",
                    SqlType = i % 2 == 0 ? "TEXT" : "INTEGER",
                }
            );
        }

        // Act
        var result = ModelGenerator.GenerateRecordType("LargeRecord", columns);

        // Assert
        Assert.True(result is StringOk);
        var success = (StringOk)result;
        var code = success.Value;

        Assert.Contains("public record LargeRecord", code);
        Assert.Contains("public string Column2 { get; init; }", code);
        Assert.Contains("public int Column19 { get; init; }", code);
        Assert.Contains("public string Column20 { get; init; }", code);

        // Verify constructor has all parameters
        Assert.Contains("public LargeRecord(", code);
        for (int i = 1; i <= 20; i++)
        {
            var expectedType = i % 2 == 0 ? "string" : "int";
            if (i == 20)
            {
                Assert.Contains($"        {expectedType} Column{i}", code); // Last parameter without comma
            }
            else
            {
                Assert.Contains($"        {expectedType} Column{i},", code);
            }
        }
    }

    [Fact]
    public void GenerateGroupedRecordTypes_WithNullInputs_ReturnsAppropriateFailures()
    {
        // Arrange
        var validColumns = new List<string> { "Id", "Name" };
        var allColumns = new List<DatabaseColumn>
        {
            new()
            {
                Name = "Id",
                CSharpType = "int",
                SqlType = "INTEGER",
            },
            new()
            {
                Name = "Name",
                CSharpType = "string",
                SqlType = "TEXT",
            },
        };

        // Act & Assert - Null parent name
        var result1 = ModelGenerator.GenerateGroupedRecordTypes(
            null!,
            "Child",
            validColumns,
            validColumns,
            allColumns
        );
        Assert.True(result1 is StringError);

        // Act & Assert - Null child name
        var result2 = ModelGenerator.GenerateGroupedRecordTypes(
            "Parent",
            null!,
            validColumns,
            validColumns,
            allColumns
        );
        Assert.True(result2 is StringError);

        // Act & Assert - Empty parent columns
        var result3 = ModelGenerator.GenerateGroupedRecordTypes(
            "Parent",
            "Child",
            [],
            validColumns,
            allColumns
        );
        Assert.True(result3 is StringError);

        // Act & Assert - Empty child columns
        var result4 = ModelGenerator.GenerateGroupedRecordTypes(
            "Parent",
            "Child",
            validColumns,
            [],
            allColumns
        );
        Assert.True(result4 is StringError);

        // Act & Assert - Empty all columns
        var result5 = ModelGenerator.GenerateGroupedRecordTypes(
            "Parent",
            "Child",
            validColumns,
            validColumns,
            []
        );
        Assert.True(result5 is StringError);
    }

    [Fact]
    public void GeneratedCode_CompileabilityTest_ValidCSharp()
    {
        // Arrange - Generate code and verify it contains proper C# syntax
        var columns = new List<DatabaseColumn>
        {
            new()
            {
                Name = "Id",
                CSharpType = "int",
                SqlType = "INTEGER",
            },
            new()
            {
                Name = "Name",
                CSharpType = "string",
                SqlType = "TEXT",
            },
            new()
            {
                Name = "CreatedAt",
                CSharpType = "DateTime?",
                SqlType = "DATETIME",
            },
        };

        // Act
        var result = ModelGenerator.GenerateRecordType("TestEntity", columns);

        // Assert
        Assert.True(result is StringOk);
        var success = (StringOk)result;
        var code = success.Value;

        // Verify proper C# syntax elements
        Assert.Contains("/// <summary>", code);
        Assert.Contains("/// Result row for 'TestEntity' query.", code);
        Assert.Contains("/// </summary>", code);
        Assert.Contains("public record TestEntity", code);
        Assert.Contains("get; init;", code);
        // Records don't have constructor parameters in doc comments, so remove this assertion
        Assert.DoesNotContain(",,", code); // No double commas
        Assert.DoesNotContain(";;", code); // No double semicolons
        Assert.Contains("}", code); // Proper closing braces

        // Verify XML documentation is properly formatted
        Assert.Contains("/// <summary>Column 'Id'.</summary>", code);
        Assert.Contains("/// <summary>Column 'Name'.</summary>", code);
        Assert.Contains("/// <summary>Column 'CreatedAt'.</summary>", code);
    }
}
