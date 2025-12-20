using Microsoft.Data.Sqlite;
using Outcome;
using Selecta;
using Xunit;

namespace DataProvider.Tests;

/// <summary>
/// Tests for DbConnectionExtensions methods to improve coverage
/// </summary>
public sealed class DbConnectionExtensionsTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public DbConnectionExtensionsTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        CreateSchema();
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
            INSERT INTO TestTable (Name, Value) VALUES ('Alpha', 10);
            INSERT INTO TestTable (Name, Value) VALUES ('Beta', 20);
            INSERT INTO TestTable (Name, Value) VALUES ('Gamma', 30);
            """,
            _connection
        );
        command.ExecuteNonQuery();
    }

    [Fact]
    public void Query_WithNullConnection_ReturnsError()
    {
        SqliteConnection? nullConnection = null;

        var result = nullConnection!.Query(
            "SELECT * FROM TestTable",
            null,
            reader => new TestRecord { Name = reader.GetString(1), Value = reader.GetInt32(2) }
        );

        Assert.True(
            result
                is Result<IReadOnlyList<TestRecord>, SqlError>.Error<
                    IReadOnlyList<TestRecord>,
                    SqlError
                >
        );
        var error = (
            (Result<IReadOnlyList<TestRecord>, SqlError>.Error<
                IReadOnlyList<TestRecord>,
                SqlError
            >)result
        ).Value;
        Assert.Contains("null", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Query_WithNullSql_ReturnsError()
    {
        var result = _connection.Query(
            null!,
            null,
            reader => new TestRecord { Name = reader.GetString(1), Value = reader.GetInt32(2) }
        );

        Assert.True(
            result
                is Result<IReadOnlyList<TestRecord>, SqlError>.Error<
                    IReadOnlyList<TestRecord>,
                    SqlError
                >
        );
        var error = (
            (Result<IReadOnlyList<TestRecord>, SqlError>.Error<
                IReadOnlyList<TestRecord>,
                SqlError
            >)result
        ).Value;
        Assert.Contains("null", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Query_WithEmptySql_ReturnsError()
    {
        var result = _connection.Query(
            "   ",
            null,
            reader => new TestRecord { Name = reader.GetString(1), Value = reader.GetInt32(2) }
        );

        Assert.True(
            result
                is Result<IReadOnlyList<TestRecord>, SqlError>.Error<
                    IReadOnlyList<TestRecord>,
                    SqlError
                >
        );
    }

    [Fact]
    public void Query_WithValidSql_ReturnsResults()
    {
        var result = _connection.Query(
            "SELECT Id, Name, Value FROM TestTable ORDER BY Name",
            null,
            reader => new TestRecord { Name = reader.GetString(1), Value = reader.GetInt32(2) }
        );

        Assert.True(
            result
                is Result<IReadOnlyList<TestRecord>, SqlError>.Ok<
                    IReadOnlyList<TestRecord>,
                    SqlError
                >
        );
        var records = (
            (Result<IReadOnlyList<TestRecord>, SqlError>.Ok<
                IReadOnlyList<TestRecord>,
                SqlError
            >)result
        ).Value;
        Assert.Equal(3, records.Count);
        Assert.Equal("Alpha", records[0].Name);
    }

    [Fact]
    public void Query_WithParameters_ReturnsFilteredResults()
    {
        var result = _connection.Query(
            "SELECT Id, Name, Value FROM TestTable WHERE Value > @minValue ORDER BY Value",
            [new SqliteParameter("@minValue", 15)],
            reader => new TestRecord { Name = reader.GetString(1), Value = reader.GetInt32(2) }
        );

        Assert.True(
            result
                is Result<IReadOnlyList<TestRecord>, SqlError>.Ok<
                    IReadOnlyList<TestRecord>,
                    SqlError
                >
        );
        var records = (
            (Result<IReadOnlyList<TestRecord>, SqlError>.Ok<
                IReadOnlyList<TestRecord>,
                SqlError
            >)result
        ).Value;
        Assert.Equal(2, records.Count);
        Assert.Equal("Beta", records[0].Name);
        Assert.Equal("Gamma", records[1].Name);
    }

    [Fact]
    public void Query_WithNullMapper_ReturnsEmptyResults()
    {
        var result = _connection.Query<TestRecord>(
            "SELECT Id, Name, Value FROM TestTable",
            null,
            null
        );

        Assert.True(
            result
                is Result<IReadOnlyList<TestRecord>, SqlError>.Ok<
                    IReadOnlyList<TestRecord>,
                    SqlError
                >
        );
        var records = (
            (Result<IReadOnlyList<TestRecord>, SqlError>.Ok<
                IReadOnlyList<TestRecord>,
                SqlError
            >)result
        ).Value;
        Assert.Empty(records);
    }

    [Fact]
    public void Query_WithInvalidSql_ReturnsError()
    {
        var result = _connection.Query(
            "SELECT * FROM NonExistentTable",
            null,
            reader => new TestRecord { Name = reader.GetString(1), Value = reader.GetInt32(2) }
        );

        Assert.True(
            result
                is Result<IReadOnlyList<TestRecord>, SqlError>.Error<
                    IReadOnlyList<TestRecord>,
                    SqlError
                >
        );
    }

    [Fact]
    public void Execute_WithNullConnection_ReturnsError()
    {
        SqliteConnection? nullConnection = null;

        var result = nullConnection!.Execute(
            "INSERT INTO TestTable (Name, Value) VALUES ('Test', 100)"
        );

        Assert.True(result is IntError);
        var error = ((IntError)result).Value;
        Assert.Contains("null", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Execute_WithNullSql_ReturnsError()
    {
        var result = _connection.Execute(null!);

        Assert.True(result is IntError);
        var error = ((IntError)result).Value;
        Assert.Contains("null", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Execute_WithEmptySql_ReturnsError()
    {
        var result = _connection.Execute("   ");

        Assert.True(result is IntError);
    }

    [Fact]
    public void Execute_WithValidSql_ReturnsRowsAffected()
    {
        var result = _connection.Execute(
            "INSERT INTO TestTable (Name, Value) VALUES (@name, @value)",
            [new SqliteParameter("@name", "Delta"), new SqliteParameter("@value", 40)]
        );

        Assert.True(result is IntOk);
        var rowsAffected = ((IntOk)result).Value;
        Assert.Equal(1, rowsAffected);
    }

    [Fact]
    public void Execute_WithInvalidSql_ReturnsError()
    {
        var result = _connection.Execute("INSERT INTO NonExistentTable (Name) VALUES ('Test')");

        Assert.True(result is IntError);
    }

    [Fact]
    public void Scalar_WithNullConnection_ReturnsError()
    {
        SqliteConnection? nullConnection = null;

        var result = nullConnection!.Scalar<string>("SELECT Name FROM TestTable LIMIT 1");

        Assert.False(result is StringOk);
    }

    [Fact]
    public void Scalar_WithNullSql_ReturnsError()
    {
        var result = _connection.Scalar<string>(null!);

        Assert.False(result is StringOk);
    }

    [Fact]
    public void Scalar_WithEmptySql_ReturnsError()
    {
        var result = _connection.Scalar<string>("   ");

        Assert.False(result is StringOk);
    }

    [Fact]
    public void Scalar_WithValidSql_ReturnsValue()
    {
        var result = _connection.Scalar<string>("SELECT Name FROM TestTable ORDER BY Name LIMIT 1");

        Assert.True(result is StringOk);
        var name = ((StringOk)result).Value;
        Assert.Equal("Alpha", name);
    }

    [Fact]
    public void Scalar_WithParameters_ReturnsFilteredValue()
    {
        var result = _connection.Scalar<string>(
            "SELECT Name FROM TestTable WHERE Value > @minValue ORDER BY Value LIMIT 1",
            [new SqliteParameter("@minValue", 15)]
        );

        Assert.True(result is StringOk);
        var name = ((StringOk)result).Value;
        Assert.Equal("Beta", name);
    }

    [Fact]
    public void Scalar_WithInvalidSql_ReturnsError()
    {
        var result = _connection.Scalar<string>("SELECT Name FROM NonExistentTable");

        Assert.False(result is StringOk);
    }

    public void Dispose() => _connection?.Dispose();

    private sealed record TestRecord
    {
        public string Name { get; init; } = "";
        public int Value { get; init; }
    }
}
