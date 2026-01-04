using Microsoft.Data.Sqlite;
using Xunit;

namespace Sync.SQLite.Tests;

/// <summary>
/// Integration tests for ChangeApplierSQLite.
/// Tests applying sync changes (insert, update, delete) to SQLite database.
/// NO MOCKS - real file-based SQLite databases only! NO :memory:!
/// </summary>
public sealed class ChangeApplierIntegrationTests : IDisposable
{
    private readonly SqliteConnection _db;
    private readonly string _dbPath;
    private readonly string _originId = Guid.NewGuid().ToString();
    private const string Timestamp = "2025-01-01T00:00:00.000Z";

    /// <summary>
    /// Initializes test with file-based SQLite database.
    /// </summary>
    public ChangeApplierIntegrationTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"change_applier_{Guid.NewGuid():N}.db");
        _db = new SqliteConnection($"Data Source={_dbPath}");
        _db.Open();
        SyncSchema.CreateSchema(_db);
        SyncSchema.SetOriginId(_db, _originId);

        // Create test table
        using var cmd = _db.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE Person (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                Age INTEGER,
                Email TEXT
            );
            """;
        cmd.ExecuteNonQuery();
    }

    #region Insert Operations

    [Fact]
    public void ApplyChange_Insert_CreatesNewRecord()
    {
        // Arrange
        var entry = new SyncLogEntry(
            Version: 1,
            TableName: "Person",
            PkValue: "{\"Id\":\"p1\"}",
            Operation: SyncOperation.Insert,
            Payload: "{\"Id\":\"p1\",\"Name\":\"Alice\",\"Age\":30,\"Email\":\"alice@example.com\"}",
            Origin: "remote-origin",
            Timestamp: Timestamp
        );

        // Act
        var result = ChangeApplierSQLite.ApplyChange(_db, entry);

        // Assert
        Assert.True(result is BoolSyncOk);
        Assert.True(((BoolSyncOk)result).Value);

        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT Name, Age, Email FROM Person WHERE Id = 'p1'";
        using var reader = cmd.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal("Alice", reader.GetString(0));
        Assert.Equal(30, reader.GetInt64(1));
        Assert.Equal("alice@example.com", reader.GetString(2));
    }

    [Fact]
    public void ApplyChange_Insert_WithNullPayload_ReturnsError()
    {
        // Arrange
        var entry = new SyncLogEntry(
            Version: 1,
            TableName: "Person",
            PkValue: "{\"Id\":\"p1\"}",
            Operation: SyncOperation.Insert,
            Payload: null,
            Origin: "remote-origin",
            Timestamp: Timestamp
        );

        // Act
        var result = ChangeApplierSQLite.ApplyChange(_db, entry);

        // Assert
        Assert.True(result is BoolSyncError);
    }

    [Fact]
    public void ApplyChange_Insert_WithEmptyPayload_ReturnsError()
    {
        // Arrange
        var entry = new SyncLogEntry(
            Version: 1,
            TableName: "Person",
            PkValue: "{\"Id\":\"p1\"}",
            Operation: SyncOperation.Insert,
            Payload: "{}",
            Origin: "remote-origin",
            Timestamp: Timestamp
        );

        // Act
        var result = ChangeApplierSQLite.ApplyChange(_db, entry);

        // Assert
        Assert.True(result is BoolSyncError);
    }

    [Fact]
    public void ApplyChange_Insert_DuplicateKey_ReplacesExisting()
    {
        // Arrange - Insert first record
        var entry1 = new SyncLogEntry(
            Version: 1,
            TableName: "Person",
            PkValue: "{\"Id\":\"p1\"}",
            Operation: SyncOperation.Insert,
            Payload: "{\"Id\":\"p1\",\"Name\":\"Alice\",\"Age\":30}",
            Origin: "remote-origin",
            Timestamp: Timestamp
        );
        ChangeApplierSQLite.ApplyChange(_db, entry1);

        // Act - Insert with same PK (should replace)
        var entry2 = new SyncLogEntry(
            Version: 2,
            TableName: "Person",
            PkValue: "{\"Id\":\"p1\"}",
            Operation: SyncOperation.Insert,
            Payload: "{\"Id\":\"p1\",\"Name\":\"Alice Updated\",\"Age\":31}",
            Origin: "remote-origin",
            Timestamp: Timestamp
        );
        var result = ChangeApplierSQLite.ApplyChange(_db, entry2);

        // Assert
        Assert.True(result is BoolSyncOk);

        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT Name, Age FROM Person WHERE Id = 'p1'";
        using var reader = cmd.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal("Alice Updated", reader.GetString(0));
        Assert.Equal(31, reader.GetInt64(1));
    }

    #endregion

    #region Update Operations

    [Fact]
    public void ApplyChange_Update_ModifiesExistingRecord()
    {
        // Arrange - Insert first
        InsertPerson("p1", "Alice", 30);

        var entry = new SyncLogEntry(
            Version: 2,
            TableName: "Person",
            PkValue: "{\"Id\":\"p1\"}",
            Operation: SyncOperation.Update,
            Payload: "{\"Id\":\"p1\",\"Name\":\"Alice Updated\",\"Age\":31,\"Email\":\"alice.new@example.com\"}",
            Origin: "remote-origin",
            Timestamp: Timestamp
        );

        // Act
        var result = ChangeApplierSQLite.ApplyChange(_db, entry);

        // Assert
        Assert.True(result is BoolSyncOk);

        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT Name, Age, Email FROM Person WHERE Id = 'p1'";
        using var reader = cmd.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal("Alice Updated", reader.GetString(0));
        Assert.Equal(31, reader.GetInt64(1));
        Assert.Equal("alice.new@example.com", reader.GetString(2));
    }

    [Fact]
    public void ApplyChange_Update_NonExistingRecord_InsertsAsUpsert()
    {
        // Arrange
        var entry = new SyncLogEntry(
            Version: 1,
            TableName: "Person",
            PkValue: "{\"Id\":\"p1\"}",
            Operation: SyncOperation.Update,
            Payload: "{\"Id\":\"p1\",\"Name\":\"Alice\",\"Age\":30}",
            Origin: "remote-origin",
            Timestamp: Timestamp
        );

        // Act
        var result = ChangeApplierSQLite.ApplyChange(_db, entry);

        // Assert
        Assert.True(result is BoolSyncOk);

        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Person WHERE Id = 'p1'";
        Assert.Equal(1L, cmd.ExecuteScalar());
    }

    #endregion

    #region Delete Operations

    [Fact]
    public void ApplyChange_Delete_RemovesExistingRecord()
    {
        // Arrange
        InsertPerson("p1", "Alice", 30);

        var entry = new SyncLogEntry(
            Version: 2,
            TableName: "Person",
            PkValue: "{\"Id\":\"p1\"}",
            Operation: SyncOperation.Delete,
            Payload: null,
            Origin: "remote-origin",
            Timestamp: Timestamp
        );

        // Act
        var result = ChangeApplierSQLite.ApplyChange(_db, entry);

        // Assert
        Assert.True(result is BoolSyncOk);

        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Person WHERE Id = 'p1'";
        Assert.Equal(0L, cmd.ExecuteScalar());
    }

    [Fact]
    public void ApplyChange_Delete_NonExistingRecord_StillSucceeds()
    {
        // Arrange
        var entry = new SyncLogEntry(
            Version: 1,
            TableName: "Person",
            PkValue: "{\"Id\":\"nonexistent\"}",
            Operation: SyncOperation.Delete,
            Payload: null,
            Origin: "remote-origin",
            Timestamp: Timestamp
        );

        // Act
        var result = ChangeApplierSQLite.ApplyChange(_db, entry);

        // Assert
        Assert.True(result is BoolSyncOk);
    }

    [Fact]
    public void ApplyChange_Delete_InvalidPkValue_ReturnsError()
    {
        // Arrange
        var entry = new SyncLogEntry(
            Version: 1,
            TableName: "Person",
            PkValue: "{}",
            Operation: SyncOperation.Delete,
            Payload: null,
            Origin: "remote-origin",
            Timestamp: Timestamp
        );

        // Act
        var result = ChangeApplierSQLite.ApplyChange(_db, entry);

        // Assert
        Assert.True(result is BoolSyncError);
    }

    #endregion

    #region JSON Type Handling

    [Fact]
    public void ApplyChange_Insert_HandlesStringValues()
    {
        // Arrange
        var entry = new SyncLogEntry(
            Version: 1,
            TableName: "Person",
            PkValue: "{\"Id\":\"p1\"}",
            Operation: SyncOperation.Insert,
            Payload: "{\"Id\":\"p1\",\"Name\":\"Alice\",\"Email\":\"alice@example.com\"}",
            Origin: "remote-origin",
            Timestamp: Timestamp
        );

        // Act
        var result = ChangeApplierSQLite.ApplyChange(_db, entry);

        // Assert
        Assert.True(result is BoolSyncOk);
    }

    [Fact]
    public void ApplyChange_Insert_HandlesIntegerValues()
    {
        // Arrange
        var entry = new SyncLogEntry(
            Version: 1,
            TableName: "Person",
            PkValue: "{\"Id\":\"p1\"}",
            Operation: SyncOperation.Insert,
            Payload: "{\"Id\":\"p1\",\"Name\":\"Alice\",\"Age\":25}",
            Origin: "remote-origin",
            Timestamp: Timestamp
        );

        // Act
        var result = ChangeApplierSQLite.ApplyChange(_db, entry);

        // Assert
        Assert.True(result is BoolSyncOk);

        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT Age FROM Person WHERE Id = 'p1'";
        Assert.Equal(25L, cmd.ExecuteScalar());
    }

    [Fact]
    public void ApplyChange_Insert_HandlesNullValues()
    {
        // Arrange
        var entry = new SyncLogEntry(
            Version: 1,
            TableName: "Person",
            PkValue: "{\"Id\":\"p1\"}",
            Operation: SyncOperation.Insert,
            Payload: "{\"Id\":\"p1\",\"Name\":\"Alice\",\"Email\":null}",
            Origin: "remote-origin",
            Timestamp: Timestamp
        );

        // Act
        var result = ChangeApplierSQLite.ApplyChange(_db, entry);

        // Assert
        Assert.True(result is BoolSyncOk);

        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT Email FROM Person WHERE Id = 'p1'";
        Assert.True(cmd.ExecuteScalar() is DBNull);
    }

    [Fact]
    public void ApplyChange_Insert_HandlesBooleanValues()
    {
        // Arrange - Create table with boolean-like column
        using var createCmd = _db.CreateCommand();
        createCmd.CommandText = "CREATE TABLE Settings (Id TEXT PRIMARY KEY, Enabled INTEGER)";
        createCmd.ExecuteNonQuery();

        var entry = new SyncLogEntry(
            Version: 1,
            TableName: "Settings",
            PkValue: "{\"Id\":\"s1\"}",
            Operation: SyncOperation.Insert,
            Payload: "{\"Id\":\"s1\",\"Enabled\":true}",
            Origin: "remote-origin",
            Timestamp: Timestamp
        );

        // Act
        var result = ChangeApplierSQLite.ApplyChange(_db, entry);

        // Assert
        Assert.True(result is BoolSyncOk);

        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT Enabled FROM Settings WHERE Id = 's1'";
        Assert.Equal(1L, cmd.ExecuteScalar());
    }

    [Fact]
    public void ApplyChange_Insert_HandlesFalseBooleanValues()
    {
        // Arrange
        using var createCmd = _db.CreateCommand();
        createCmd.CommandText = "CREATE TABLE Settings (Id TEXT PRIMARY KEY, Enabled INTEGER)";
        createCmd.ExecuteNonQuery();

        var entry = new SyncLogEntry(
            Version: 1,
            TableName: "Settings",
            PkValue: "{\"Id\":\"s1\"}",
            Operation: SyncOperation.Insert,
            Payload: "{\"Id\":\"s1\",\"Enabled\":false}",
            Origin: "remote-origin",
            Timestamp: Timestamp
        );

        // Act
        var result = ChangeApplierSQLite.ApplyChange(_db, entry);

        // Assert
        Assert.True(result is BoolSyncOk);

        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT Enabled FROM Settings WHERE Id = 's1'";
        Assert.Equal(0L, cmd.ExecuteScalar());
    }

    [Fact]
    public void ApplyChange_Insert_HandlesDoubleValues()
    {
        // Arrange
        using var createCmd = _db.CreateCommand();
        createCmd.CommandText = "CREATE TABLE Product (Id TEXT PRIMARY KEY, Price REAL)";
        createCmd.ExecuteNonQuery();

        var entry = new SyncLogEntry(
            Version: 1,
            TableName: "Product",
            PkValue: "{\"Id\":\"prod1\"}",
            Operation: SyncOperation.Insert,
            Payload: "{\"Id\":\"prod1\",\"Price\":99.99}",
            Origin: "remote-origin",
            Timestamp: Timestamp
        );

        // Act
        var result = ChangeApplierSQLite.ApplyChange(_db, entry);

        // Assert
        Assert.True(result is BoolSyncOk);

        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT Price FROM Product WHERE Id = 'prod1'";
        var price = Convert.ToDouble(
            cmd.ExecuteScalar(),
            System.Globalization.CultureInfo.InvariantCulture
        );
        Assert.Equal(99.99, price, precision: 2);
    }

    #endregion

    #region Foreign Key Handling

    [Fact]
    public void ApplyChange_Insert_WithForeignKeyViolation_ReturnsFalse()
    {
        // Arrange - Create FK relationship
        using var cmd = _db.CreateCommand();
        cmd.CommandText = """
            PRAGMA foreign_keys = ON;
            CREATE TABLE Department (Id TEXT PRIMARY KEY, Name TEXT);
            CREATE TABLE Employee (
                Id TEXT PRIMARY KEY,
                Name TEXT,
                DeptId TEXT REFERENCES Department(Id)
            );
            """;
        cmd.ExecuteNonQuery();

        var entry = new SyncLogEntry(
            Version: 1,
            TableName: "Employee",
            PkValue: "{\"Id\":\"e1\"}",
            Operation: SyncOperation.Insert,
            Payload: "{\"Id\":\"e1\",\"Name\":\"John\",\"DeptId\":\"nonexistent\"}",
            Origin: "remote-origin",
            Timestamp: Timestamp
        );

        // Act
        var result = ChangeApplierSQLite.ApplyChange(_db, entry);

        // Assert - FK violation returns false (for deferred retry)
        Assert.True(result is BoolSyncOk);
        Assert.False(((BoolSyncOk)result).Value);
    }

    [Fact]
    public void ApplyChange_Insert_WithValidForeignKey_Succeeds()
    {
        // Arrange - Create FK relationship and parent record
        using var cmd = _db.CreateCommand();
        cmd.CommandText = """
            PRAGMA foreign_keys = ON;
            CREATE TABLE Department (Id TEXT PRIMARY KEY, Name TEXT);
            INSERT INTO Department (Id, Name) VALUES ('d1', 'Engineering');
            CREATE TABLE Employee (
                Id TEXT PRIMARY KEY,
                Name TEXT,
                DeptId TEXT REFERENCES Department(Id)
            );
            """;
        cmd.ExecuteNonQuery();

        var entry = new SyncLogEntry(
            Version: 1,
            TableName: "Employee",
            PkValue: "{\"Id\":\"e1\"}",
            Operation: SyncOperation.Insert,
            Payload: "{\"Id\":\"e1\",\"Name\":\"John\",\"DeptId\":\"d1\"}",
            Origin: "remote-origin",
            Timestamp: Timestamp
        );

        // Act
        var result = ChangeApplierSQLite.ApplyChange(_db, entry);

        // Assert
        Assert.True(result is BoolSyncOk);
        Assert.True(((BoolSyncOk)result).Value);
    }

    #endregion

    #region Batch Operations

    [Fact]
    public void ApplyChange_MultipleBatches_AllSucceed()
    {
        // Arrange
        var entries = new[]
        {
            new SyncLogEntry(
                1,
                "Person",
                "{\"Id\":\"p1\"}",
                SyncOperation.Insert,
                "{\"Id\":\"p1\",\"Name\":\"Alice\"}",
                "origin",
                Timestamp
            ),
            new SyncLogEntry(
                2,
                "Person",
                "{\"Id\":\"p2\"}",
                SyncOperation.Insert,
                "{\"Id\":\"p2\",\"Name\":\"Bob\"}",
                "origin",
                Timestamp
            ),
            new SyncLogEntry(
                3,
                "Person",
                "{\"Id\":\"p3\"}",
                SyncOperation.Insert,
                "{\"Id\":\"p3\",\"Name\":\"Charlie\"}",
                "origin",
                Timestamp
            ),
        };

        // Act
        var results = entries.Select(e => ChangeApplierSQLite.ApplyChange(_db, e)).ToList();

        // Assert
        Assert.All(
            results,
            r =>
            {
                Assert.True(r is BoolSyncOk);
                Assert.True(((BoolSyncOk)r).Value);
            }
        );

        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Person";
        Assert.Equal(3L, cmd.ExecuteScalar());
    }

    #endregion

    #region Helpers

    private void InsertPerson(string id, string name, int age)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "INSERT INTO Person (Id, Name, Age) VALUES (@id, @name, @age)";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@name", name);
        cmd.Parameters.AddWithValue("@age", age);
        cmd.ExecuteNonQuery();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _db.Close();
        _db.Dispose();
        if (File.Exists(_dbPath))
        {
            try { File.Delete(_dbPath); }
            catch { /* File may be locked */ }
        }
    }

    #endregion
}
