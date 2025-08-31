using System.Data;
using Microsoft.Data.Sqlite;
using Results;
using Xunit;

namespace DataProvider.Tests;

/// <summary>
/// Tests for DbTransactionExtensions Query method to improve coverage
/// </summary>
public sealed class DbTransactionExtensionsTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly SqliteTransaction _transaction;

    public DbTransactionExtensionsTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        CreateSchema();
        _transaction = _connection.BeginTransaction();
    }

    private void CreateSchema()
    {
        using var command = new SqliteCommand(
            """
            CREATE TABLE TestTable (
                Id INTEGER PRIMARY KEY,
                Name TEXT NOT NULL,
                Value INTEGER NOT NULL
            );
            """,
            _connection
        );
        command.ExecuteNonQuery();
    }

    [Fact]
    public void Query_WithValidData_ReturnsResults()
    {
        _transaction.Execute(
            "INSERT INTO TestTable (Name, Value) VALUES (@name1, @value1), (@name2, @value2)",
            [
                new SqliteParameter("@name1", "Test1"),
                new SqliteParameter("@value1", 100),
                new SqliteParameter("@name2", "Test2"),
                new SqliteParameter("@value2", 200)
            ]
        );

        var result = _transaction.Query(
            "SELECT Name, Value FROM TestTable ORDER BY Name",
[],
            reader => new TestRecord
            {
                Name = reader.GetString(0),
                Value = reader.GetInt32(1)
            }
        );

        Assert.True(result is Result<IReadOnlyList<TestRecord>, SqlError>.Success);
        var records = ((Result<IReadOnlyList<TestRecord>, SqlError>.Success)result).Value;
        Assert.Equal(2, records.Count);
        Assert.Equal("Test1", records[0].Name);
        Assert.Equal(100, records[0].Value);
        Assert.Equal("Test2", records[1].Name);
        Assert.Equal(200, records[1].Value);
    }

    [Fact]
    public void Query_WithParameters_ReturnsFilteredResults()
    {
        _transaction.Execute(
            "INSERT INTO TestTable (Name, Value) VALUES (@name1, @value1), (@name2, @value2), (@name3, @value3)",
            [
                new SqliteParameter("@name1", "Alpha"),
                new SqliteParameter("@value1", 10),
                new SqliteParameter("@name2", "Beta"),
                new SqliteParameter("@value2", 20),
                new SqliteParameter("@name3", "Gamma"),
                new SqliteParameter("@value3", 30)
            ]
        );

        var result = _transaction.Query(
            "SELECT Name, Value FROM TestTable WHERE Value > @minValue ORDER BY Value",
            [new SqliteParameter("@minValue", 15)],
            reader => new TestRecord
            {
                Name = reader.GetString(0),
                Value = reader.GetInt32(1)
            }
        );

        Assert.True(result is Result<IReadOnlyList<TestRecord>, SqlError>.Success);
        var records = ((Result<IReadOnlyList<TestRecord>, SqlError>.Success)result).Value;
        Assert.Equal(2, records.Count);
        Assert.Equal("Beta", records[0].Name);
        Assert.Equal(20, records[0].Value);
        Assert.Equal("Gamma", records[1].Name);
        Assert.Equal(30, records[1].Value);
    }

    [Fact]
    public void Query_WithEmptyResult_ReturnsEmptyList()
    {
        var result = _transaction.Query(
            "SELECT Name, Value FROM TestTable WHERE 1=0",
[],
            reader => new TestRecord
            {
                Name = reader.GetString(0),
                Value = reader.GetInt32(1)
            }
        );

        Assert.True(result is Result<IReadOnlyList<TestRecord>, SqlError>.Success);
        var records = ((Result<IReadOnlyList<TestRecord>, SqlError>.Success)result).Value;
        Assert.Empty(records);
    }

    [Fact]
    public void Query_WithSqlError_ReturnsFailure()
    {
        var result = _transaction.Query(
            "SELECT InvalidColumn FROM NonExistentTable",
[],
            reader => new TestRecord
            {
                Name = reader.GetString(0),
                Value = reader.GetInt32(1)
            }
        );

        Assert.True(result is Result<IReadOnlyList<TestRecord>, SqlError>.Failure);
        var failure = (Result<IReadOnlyList<TestRecord>, SqlError>.Failure)result;
        Assert.NotNull(failure.ErrorValue.Message);
        Assert.Contains("no such table", failure.ErrorValue.Message);
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _connection?.Dispose();
    }

    private sealed record TestRecord
    {
        public string Name { get; init; } = "";
        public int Value { get; init; }
    }
}