using DataProvider.CodeGeneration;
using Xunit;

namespace DataProvider.Tests;

/// <summary>
/// Tests for bulk insert and upsert code generation
/// </summary>
public sealed class BulkOperationsTests
{
    private static DatabaseTable CreateTestTable() =>
        new()
        {
            Schema = "public",
            Name = "Products",
            Columns = new List<DatabaseColumn>
            {
                new()
                {
                    Name = "Id",
                    CSharpType = "Guid",
                    IsPrimaryKey = true,
                    IsIdentity = false,
                },
                new()
                {
                    Name = "Name",
                    CSharpType = "string",
                    IsNullable = false,
                },
                new()
                {
                    Name = "Price",
                    CSharpType = "decimal",
                    IsNullable = false,
                },
                new()
                {
                    Name = "Category",
                    CSharpType = "string",
                    IsNullable = true,
                },
            }.AsReadOnly(),
        };

    private static DatabaseTable CreateTableWithIdentity() =>
        new()
        {
            Schema = "public",
            Name = "Orders",
            Columns = new List<DatabaseColumn>
            {
                new()
                {
                    Name = "Id",
                    CSharpType = "int",
                    IsPrimaryKey = true,
                    IsIdentity = true,
                },
                new()
                {
                    Name = "CustomerId",
                    CSharpType = "Guid",
                    IsNullable = false,
                },
                new()
                {
                    Name = "Total",
                    CSharpType = "decimal",
                    IsNullable = false,
                },
            }.AsReadOnly(),
        };

    [Fact]
    public void GenerateBulkInsertMethod_WithValidTable_ReturnsSuccess()
    {
        // Arrange
        var table = CreateTestTable();

        // Act
        var result = DataAccessGenerator.GenerateBulkInsertMethod(table);

        // Assert
        Assert.True(result is StringOk);
        var code = ((StringOk)result).Value;
        Assert.Contains("BulkInsertProductsAsync", code);
        Assert.Contains("IEnumerable<(", code);
        Assert.Contains("INSERT INTO Products", code);
        Assert.Contains("batchSize", code);
    }

    [Fact]
    public void GenerateBulkInsertMethod_WithNullTable_ReturnsError()
    {
        // Act
        var result = DataAccessGenerator.GenerateBulkInsertMethod(null!);

        // Assert
        Assert.True(result is StringError);
        var error = ((StringError)result).Value;
        Assert.Contains("table cannot be null", error.Message);
    }

    [Fact]
    public void GenerateBulkInsertMethod_WithIdentityColumn_ExcludesIdentityFromInsert()
    {
        // Arrange
        var table = CreateTableWithIdentity();

        // Act
        var result = DataAccessGenerator.GenerateBulkInsertMethod(table);

        // Assert
        Assert.True(result is StringOk);
        var code = ((StringOk)result).Value;
        Assert.Contains("BulkInsertOrdersAsync", code);
        Assert.Contains("CustomerId", code);
        Assert.Contains("Total", code);
        Assert.DoesNotContain("Id Id", code);
    }

    [Fact]
    public void GenerateBulkInsertMethod_GeneratesBatchHelper()
    {
        // Arrange
        var table = CreateTestTable();

        // Act
        var result = DataAccessGenerator.GenerateBulkInsertMethod(table);

        // Assert
        Assert.True(result is StringOk);
        var code = ((StringOk)result).Value;
        Assert.Contains("ExecuteBulkInsertProductsBatchAsync", code);
        Assert.Contains("StringBuilder", code);
        Assert.Contains("parameters.Add", code);
    }

    [Fact]
    public void GenerateBulkInsertMethod_WithCustomBatchSize_UsesBatchSize()
    {
        // Arrange
        var table = CreateTestTable();

        // Act
        var result = DataAccessGenerator.GenerateBulkInsertMethod(table, batchSize: 500);

        // Assert
        Assert.True(result is StringOk);
        var code = ((StringOk)result).Value;
        Assert.Contains("const int batchSize = 500;", code);
    }

    [Fact]
    public void GenerateBulkUpsertMethod_WithValidTable_ReturnsSuccess()
    {
        // Arrange
        var table = CreateTestTable();

        // Act
        var result = DataAccessGenerator.GenerateBulkUpsertMethod(table);

        // Assert
        Assert.True(result is StringOk);
        var code = ((StringOk)result).Value;
        Assert.Contains("BulkUpsertProductsAsync", code);
        Assert.Contains("INSERT OR REPLACE INTO Products", code);
    }

    [Fact]
    public void GenerateBulkUpsertMethod_ForPostgres_UsesOnConflict()
    {
        // Arrange
        var table = CreateTestTable();

        // Act
        var result = DataAccessGenerator.GenerateBulkUpsertMethod(
            table,
            databaseType: "Postgres",
            connectionType: "NpgsqlConnection"
        );

        // Assert
        Assert.True(result is StringOk);
        var code = ((StringOk)result).Value;
        Assert.Contains("ON CONFLICT", code);
        Assert.Contains("DO UPDATE SET", code);
        Assert.Contains("EXCLUDED.", code);
        Assert.Contains("NpgsqlCommand", code);
    }

    [Fact]
    public void GenerateBulkUpsertMethod_ForSQLite_UsesReplaceInto()
    {
        // Arrange
        var table = CreateTestTable();

        // Act
        var result = DataAccessGenerator.GenerateBulkUpsertMethod(table, databaseType: "SQLite");

        // Assert
        Assert.True(result is StringOk);
        var code = ((StringOk)result).Value;
        Assert.Contains("INSERT OR REPLACE INTO", code);
        Assert.DoesNotContain("ON CONFLICT", code);
    }

    [Fact]
    public void GenerateBulkUpsertMethod_WithNullTable_ReturnsError()
    {
        // Act
        var result = DataAccessGenerator.GenerateBulkUpsertMethod(null!);

        // Assert
        Assert.True(result is StringError);
        var error = ((StringError)result).Value;
        Assert.Contains("table cannot be null", error.Message);
    }

    [Fact]
    public void GenerateBulkUpsertMethod_WithNoPrimaryKey_ReturnsEmpty()
    {
        // Arrange
        var table = new DatabaseTable
        {
            Schema = "public",
            Name = "Logs",
            Columns = new List<DatabaseColumn>
            {
                new()
                {
                    Name = "Message",
                    CSharpType = "string",
                    IsPrimaryKey = false,
                },
                new()
                {
                    Name = "Timestamp",
                    CSharpType = "DateTime",
                    IsPrimaryKey = false,
                },
            }.AsReadOnly(),
        };

        // Act
        var result = DataAccessGenerator.GenerateBulkUpsertMethod(table);

        // Assert
        Assert.True(result is StringOk);
        var code = ((StringOk)result).Value;
        Assert.Empty(code);
    }

    [Fact]
    public void GenerateBulkInsertMethod_IncludesErrorHandling()
    {
        // Arrange
        var table = CreateTestTable();

        // Act
        var result = DataAccessGenerator.GenerateBulkInsertMethod(table);

        // Assert
        Assert.True(result is StringOk);
        var code = ((StringOk)result).Value;
        Assert.Contains("try", code);
        Assert.Contains("catch (Exception ex)", code);
        Assert.Contains("Result<int, SqlError>.Error", code);
        Assert.Contains("Bulk insert failed", code);
    }

    [Fact]
    public void GenerateBulkUpsertMethod_IncludesErrorHandling()
    {
        // Arrange
        var table = CreateTestTable();

        // Act
        var result = DataAccessGenerator.GenerateBulkUpsertMethod(table);

        // Assert
        Assert.True(result is StringOk);
        var code = ((StringOk)result).Value;
        Assert.Contains("try", code);
        Assert.Contains("catch (Exception ex)", code);
        Assert.Contains("Result<int, SqlError>.Error", code);
        Assert.Contains("Bulk upsert failed", code);
    }

    [Fact]
    public void GenerateBulkInsertMethod_GeneratesParameterizedQueries()
    {
        // Arrange
        var table = CreateTestTable();

        // Act
        var result = DataAccessGenerator.GenerateBulkInsertMethod(table);

        // Assert
        Assert.True(result is StringOk);
        var code = ((StringOk)result).Value;
        Assert.Contains("@p", code);
        Assert.Contains("AddWithValue", code);
        Assert.Contains("DBNull.Value", code);
    }

    [Fact]
    public void GenerateBulkUpsertMethod_IncludesAllColumns()
    {
        // Arrange
        var table = CreateTestTable();

        // Act
        var result = DataAccessGenerator.GenerateBulkUpsertMethod(table, databaseType: "Postgres");

        // Assert
        Assert.True(result is StringOk);
        var code = ((StringOk)result).Value;
        Assert.Contains("Id", code);
        Assert.Contains("Name", code);
        Assert.Contains("Price", code);
        Assert.Contains("Category", code);
    }

    [Fact]
    public void GenerateBulkInsertMethod_GeneratesProperTupleType()
    {
        // Arrange
        var table = CreateTestTable();

        // Act
        var result = DataAccessGenerator.GenerateBulkInsertMethod(table);

        // Assert
        Assert.True(result is StringOk);
        var code = ((StringOk)result).Value;
        Assert.Contains("Guid Id", code);
        Assert.Contains("string Name", code);
        Assert.Contains("decimal Price", code);
        Assert.Contains("string Category", code);
    }

    [Fact]
    public void GenerateBulkInsertMethod_HandlesNullableTypes()
    {
        // Arrange
        var table = CreateTestTable();

        // Act
        var result = DataAccessGenerator.GenerateBulkInsertMethod(table);

        // Assert
        Assert.True(result is StringOk);
        var code = ((StringOk)result).Value;
        Assert.Contains("?? (object)DBNull.Value", code);
    }

    [Fact]
    public void GenerateBulkUpsertMethod_Postgres_UpdatesNonKeyColumns()
    {
        // Arrange
        var table = CreateTestTable();

        // Act
        var result = DataAccessGenerator.GenerateBulkUpsertMethod(table, databaseType: "Postgres");

        // Assert
        Assert.True(result is StringOk);
        var code = ((StringOk)result).Value;
        Assert.Contains("Name = EXCLUDED.Name", code);
        Assert.Contains("Price = EXCLUDED.Price", code);
        Assert.Contains("Category = EXCLUDED.Category", code);
        Assert.DoesNotContain("Id = EXCLUDED.Id", code);
    }

    [Fact]
    public void GenerateBulkInsertMethod_GeneratesAsyncMethods()
    {
        // Arrange
        var table = CreateTestTable();

        // Act
        var result = DataAccessGenerator.GenerateBulkInsertMethod(table);

        // Assert
        Assert.True(result is StringOk);
        var code = ((StringOk)result).Value;
        Assert.Contains("async Task<Result<int, SqlError>>", code);
        Assert.Contains("await", code);
        Assert.Contains("ConfigureAwait(false)", code);
    }

    [Fact]
    public void GenerateBulkInsertMethod_UsesExtensionMethodPattern()
    {
        // Arrange
        var table = CreateTestTable();

        // Act
        var result = DataAccessGenerator.GenerateBulkInsertMethod(table);

        // Assert
        Assert.True(result is StringOk);
        var code = ((StringOk)result).Value;
        Assert.Contains("this IDbTransaction transaction", code);
    }

    [Fact]
    public void GenerateBulkUpsertMethod_WithCompositeKey_HandlesCorrectly()
    {
        // Arrange
        var table = new DatabaseTable
        {
            Schema = "public",
            Name = "OrderItems",
            Columns = new List<DatabaseColumn>
            {
                new()
                {
                    Name = "OrderId",
                    CSharpType = "Guid",
                    IsPrimaryKey = true,
                },
                new()
                {
                    Name = "ProductId",
                    CSharpType = "Guid",
                    IsPrimaryKey = true,
                },
                new()
                {
                    Name = "Quantity",
                    CSharpType = "int",
                    IsNullable = false,
                },
                new()
                {
                    Name = "UnitPrice",
                    CSharpType = "decimal",
                    IsNullable = false,
                },
            }.AsReadOnly(),
        };

        // Act
        var result = DataAccessGenerator.GenerateBulkUpsertMethod(table, databaseType: "Postgres");

        // Assert
        Assert.True(result is StringOk);
        var code = ((StringOk)result).Value;
        Assert.Contains("ON CONFLICT (OrderId, ProductId)", code);
        Assert.Contains("Quantity = EXCLUDED.Quantity", code);
        Assert.Contains("UnitPrice = EXCLUDED.UnitPrice", code);
    }
}
