using Microsoft.Data.Sqlite;
using Xunit;

namespace Nimblesite.Sync.SQLite.Tests;

/// <summary>
/// Real end-to-end integration tests that sync data between two SQLite databases.
/// No mocks - actual database operations with triggers and change capture.
/// </summary>
public sealed class EndToEndSyncTests : IDisposable
{
    private readonly SqliteConnection _sourceDb;
    private readonly SqliteConnection _targetDb;
    private readonly string _sourceOrigin = Guid.NewGuid().ToString();
    private readonly string _targetOrigin = Guid.NewGuid().ToString();
    private readonly string _sourceDbPath;
    private readonly string _targetDbPath;

    public EndToEndSyncTests()
    {
        _sourceDbPath = Path.Combine(Path.GetTempPath(), $"e2e_source_{Guid.NewGuid()}.db");
        _targetDbPath = Path.Combine(Path.GetTempPath(), $"e2e_target_{Guid.NewGuid()}.db");
        _sourceDb = CreateDatabase(_sourceDbPath);
        _targetDb = CreateDatabase(_targetDbPath);

        SetupSchema(_sourceDb, _sourceOrigin);
        SetupSchema(_targetDb, _targetOrigin);
    }

    [Fact]
    public void Nimblesite.Sync.Core_InsertInSource_AppearsInTarget()
    {
        // Arrange: Insert a person in source
        InsertPerson(_sourceDb, "p1", "Alice", "alice@example.com");

        // Act: Fetch changes from source, apply to target
        var changes = FetchChangesFromSource();
        Assert.Single(changes);

        ApplyChangesToTarget(changes);

        // Assert: Person exists in target
        var person = GetPerson(_targetDb, "p1");
        Assert.NotNull(person);
        Assert.Equal("Alice", person.Value.Name);
        Assert.Equal("alice@example.com", person.Value.Email);
    }

    [Fact]
    public void Nimblesite.Sync.Core_UpdateInSource_UpdatesTarget()
    {
        // Arrange: Insert then update in source
        InsertPerson(_sourceDb, "p1", "Alice", "alice@example.com");
        UpdatePerson(_sourceDb, "p1", "Alice Updated", "alice.updated@example.com");

        // Act: Nimblesite.Sync.Core all changes
        var changes = FetchChangesFromSource();
        Assert.Equal(2, changes.Count);

        ApplyChangesToTarget(changes);

        // Assert: Target has updated values
        var person = GetPerson(_targetDb, "p1");
        Assert.NotNull(person);
        Assert.Equal("Alice Updated", person.Value.Name);
        Assert.Equal("alice.updated@example.com", person.Value.Email);
    }

    [Fact]
    public void Nimblesite.Sync.Core_DeleteInSource_DeletesFromTarget()
    {
        // Arrange: Insert in both, then delete from source
        InsertPerson(_sourceDb, "p1", "Alice", "alice@example.com");
        var changes1 = FetchChangesFromSource();
        ApplyChangesToTarget(changes1);

        // Verify target has the person
        Assert.NotNull(GetPerson(_targetDb, "p1"));

        // Delete from source
        DeletePerson(_sourceDb, "p1");

        // Act: Nimblesite.Sync.Core delete
        var changes2 = FetchChangesFromSource(changes1.Max(c => c.Version));
        Assert.Single(changes2);
        Assert.Equal(Nimblesite.Sync.CoreOperation.Delete, changes2[0].Operation);

        ApplyChangesToTarget(changes2);

        // Assert: Person is gone from target
        Assert.Null(GetPerson(_targetDb, "p1"));
    }

    [Fact]
    public void Nimblesite.Sync.Core_MultipleRecords_AllSynced()
    {
        // Arrange: Insert multiple records
        InsertPerson(_sourceDb, "p1", "Alice", "alice@example.com");
        InsertPerson(_sourceDb, "p2", "Bob", "bob@example.com");
        InsertPerson(_sourceDb, "p3", "Charlie", "charlie@example.com");

        // Act: Nimblesite.Sync.Core
        var changes = FetchChangesFromSource();
        Assert.Equal(3, changes.Count);

        ApplyChangesToTarget(changes);

        // Assert: All records exist in target
        Assert.NotNull(GetPerson(_targetDb, "p1"));
        Assert.NotNull(GetPerson(_targetDb, "p2"));
        Assert.NotNull(GetPerson(_targetDb, "p3"));
    }

    [Fact]
    public void Nimblesite.Sync.Core_BatchedChanges_AllApplied()
    {
        // Arrange: Insert many records
        for (int i = 0; i < 50; i++)
        {
            InsertPerson(_sourceDb, $"p{i}", $"Person{i}", $"person{i}@example.com");
        }

        // Act: Fetch in batches of 10
        var allChanges = new List<Nimblesite.Sync.CoreLogEntry>();
        long fromVersion = 0;
        while (true)
        {
            var batch = FetchChangesFromSource(fromVersion, batchSize: 10);
            if (batch.Count == 0)
                break;
            allChanges.AddRange(batch);
            fromVersion = batch.Max(c => c.Version);
        }

        Assert.Equal(50, allChanges.Count);

        // Apply in batches
        foreach (var batch in allChanges.Chunk(10))
        {
            ApplyChangesToTarget([.. batch]);
        }

        // Assert: All 50 records synced
        for (int i = 0; i < 50; i++)
        {
            Assert.NotNull(GetPerson(_targetDb, $"p{i}"));
        }
    }

    [Fact]
    public void Nimblesite.Sync.Core_TriggerSuppression_PreventsDuplicateLogging()
    {
        // Arrange: Insert in source, sync to target
        InsertPerson(_sourceDb, "p1", "Alice", "alice@example.com");
        var changes = FetchChangesFromSource();

        // Act: Apply with suppression enabled
        Nimblesite.Sync.CoreSessionManager.EnableSuppression(_targetDb);
        ApplyChangesToTarget(changes, skipSuppression: true);
        Nimblesite.Sync.CoreSessionManager.DisableSuppression(_targetDb);

        // Assert: Target should NOT have logged this change (it came from sync)
        var targetChanges = FetchChanges(_targetDb, 0);
        Assert.Empty(targetChanges);

        // But the data should be there
        Assert.NotNull(GetPerson(_targetDb, "p1"));
    }

    [Fact]
    public void Nimblesite.Sync.Core_SkipsOwnOriginChanges()
    {
        // Arrange: Create change with target's own origin
        var fakeEntry = new Nimblesite.Sync.CoreLogEntry(
            1,
            "Person",
            "{\"Id\":\"p1\"}",
            Nimblesite.Sync.CoreOperation.Insert,
            "{\"Id\":\"p1\",\"Name\":\"Fake\",\"Email\":\"fake@example.com\"}",
            _targetOrigin, // Same as target's origin
            DateTime.UtcNow.ToString("O")
        );

        // Act: Try to apply - should skip because it's own origin
        var result = ChangeApplier.ApplyBatch(
            new Nimblesite.Sync.CoreBatch([fakeEntry], 0, 1, false),
            _targetOrigin,
            3,
            entry => ApplySingleChange(_targetDb, entry),
            NullLogger.Instance
        );

        // Assert: No error, but nothing applied
        Assert.IsType<BatchApplyResultOk>(result);
        var applyResult = ((BatchApplyResultOk)result).Value;
        Assert.Equal(0, applyResult.AppliedCount);
        Assert.Null(GetPerson(_targetDb, "p1"));
    }

    [Fact]
    public void Nimblesite.Sync.Core_BiDirectional_BothDbsGetChanges()
    {
        // Arrange: Insert in source
        InsertPerson(_sourceDb, "p1", "From Source", "source@example.com");

        // Insert in target (simulating offline change)
        InsertPerson(_targetDb, "p2", "From Target", "target@example.com");

        // Act: Nimblesite.Sync.Core source -> target
        var sourceChanges = FetchChangesFromSource();
        ApplyChangesToTarget(sourceChanges);

        // Nimblesite.Sync.Core target -> source
        var targetChanges = FetchChanges(_targetDb, 0);
        ApplyChanges(_sourceDb, targetChanges, _sourceOrigin);

        // Assert: Both DBs have both records
        Assert.NotNull(GetPerson(_sourceDb, "p1"));
        Assert.NotNull(GetPerson(_sourceDb, "p2"));
        Assert.NotNull(GetPerson(_targetDb, "p1"));
        Assert.NotNull(GetPerson(_targetDb, "p2"));
    }

    private static SqliteConnection CreateDatabase(string dbPath)
    {
        var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();
        return connection;
    }

    private static void SetupSchema(SqliteConnection connection, string originId)
    {
        Nimblesite.Sync.CoreSchema.CreateSchema(connection);
        Nimblesite.Sync.CoreSchema.SetOriginId(connection, originId);

        // Create Person table
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE Person (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                Email TEXT NOT NULL
            );
            """;
        cmd.ExecuteNonQuery();

        // Use TriggerGenerator to create sync triggers (spec Section 9)
        var triggerResult = TriggerGenerator.CreateTriggers(connection, "Person", TestLogger.L);
        if (triggerResult is BoolSyncError { Value: Nimblesite.Sync.CoreErrorDatabase dbError })
        {
            throw new InvalidOperationException($"Failed to create triggers: {dbError.Message}");
        }
    }

    private static void InsertPerson(SqliteConnection db, string id, string name, string email)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = "INSERT INTO Person (Id, Name, Email) VALUES (@id, @name, @email)";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@name", name);
        cmd.Parameters.AddWithValue("@email", email);
        cmd.ExecuteNonQuery();
    }

    private static void UpdatePerson(SqliteConnection db, string id, string name, string email)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = "UPDATE Person SET Name = @name, Email = @email WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@name", name);
        cmd.Parameters.AddWithValue("@email", email);
        cmd.ExecuteNonQuery();
    }

    private static void DeletePerson(SqliteConnection db, string id)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = "DELETE FROM Person WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    private static (string Id, string Name, string Email)? GetPerson(SqliteConnection db, string id)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, Email FROM Person WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            return (reader.GetString(0), reader.GetString(1), reader.GetString(2));
        }
        return null;
    }

    private List<Nimblesite.Sync.CoreLogEntry> FetchChangesFromSource(long fromVersion = 0, int batchSize = 1000)
    {
        var result = Nimblesite.Sync.CoreLogRepository.FetchChanges(_sourceDb, fromVersion, batchSize);
        Assert.IsType<Nimblesite.Sync.CoreLogListOk>(result);
        return [.. ((Nimblesite.Sync.CoreLogListOk)result).Value];
    }

    private static List<Nimblesite.Sync.CoreLogEntry> FetchChanges(
        SqliteConnection db,
        long fromVersion,
        int batchSize = 1000
    )
    {
        var result = Nimblesite.Sync.CoreLogRepository.FetchChanges(db, fromVersion, batchSize);
        Assert.IsType<Nimblesite.Sync.CoreLogListOk>(result);
        return [.. ((Nimblesite.Sync.CoreLogListOk)result).Value];
    }

    private void ApplyChangesToTarget(List<Nimblesite.Sync.CoreLogEntry> changes, bool skipSuppression = false) =>
        ApplyChanges(_targetDb, changes, _targetOrigin, skipSuppression);

    private static void ApplyChanges(
        SqliteConnection db,
        List<Nimblesite.Sync.CoreLogEntry> changes,
        string myOrigin,
        bool skipSuppression = false
    )
    {
        if (changes.Count == 0)
            return;

        if (!skipSuppression)
        {
            Nimblesite.Sync.CoreSessionManager.EnableSuppression(db);
        }

        try
        {
            var batch = new Nimblesite.Sync.CoreBatch(
                changes,
                changes.Min(c => c.Version) - 1,
                changes.Max(c => c.Version),
                false
            );

            var result = ChangeApplier.ApplyBatch(
                batch,
                myOrigin,
                3,
                entry => ApplySingleChange(db, entry),
                NullLogger.Instance
            );

            Assert.IsType<BatchApplyResultOk>(result);
        }
        finally
        {
            if (!skipSuppression)
            {
                Nimblesite.Sync.CoreSessionManager.DisableSuppression(db);
            }
        }
    }

    private static BoolSyncResult ApplySingleChange(SqliteConnection db, Nimblesite.Sync.CoreLogEntry entry)
    {
        try
        {
            using var cmd = db.CreateCommand();

            if (entry.TableName == "Person")
            {
                switch (entry.Operation)
                {
                    case Nimblesite.Sync.CoreOperation.Insert:
                    case Nimblesite.Sync.CoreOperation.Update:
                        var payload = System.Text.Json.JsonSerializer.Deserialize<
                            Dictionary<string, string>
                        >(entry.Payload!);
                        cmd.CommandText = """
                            INSERT INTO Person (Id, Name, Email) VALUES (@id, @name, @email)
                            ON CONFLICT(Id) DO UPDATE SET Name = @name, Email = @email
                            """;
                        cmd.Parameters.AddWithValue("@id", payload!["Id"]);
                        cmd.Parameters.AddWithValue("@name", payload["Name"]);
                        cmd.Parameters.AddWithValue("@email", payload["Email"]);
                        break;

                    case Nimblesite.Sync.CoreOperation.Delete:
                        var pk = System.Text.Json.JsonSerializer.Deserialize<
                            Dictionary<string, string>
                        >(entry.PkValue);
                        cmd.CommandText = "DELETE FROM Person WHERE Id = @id";
                        cmd.Parameters.AddWithValue("@id", pk!["Id"]);
                        break;
                }

                cmd.ExecuteNonQuery();
            }

            return new BoolSyncOk(true);
        }
        catch (SqliteException ex)
        {
            if (ChangeApplier.IsForeignKeyViolation(ex.Message))
            {
                return new BoolSyncOk(false);
            }
            return new BoolSyncError(new Nimblesite.Sync.CoreErrorDatabase(ex.Message));
        }
    }

    public void Dispose()
    {
        _sourceDb.Dispose();
        _targetDb.Dispose();
        if (File.Exists(_sourceDbPath))
        {
            try
            {
                File.Delete(_sourceDbPath);
            }
            catch
            { /* File may be locked */
            }
        }
        if (File.Exists(_targetDbPath))
        {
            try
            {
                File.Delete(_targetDbPath);
            }
            catch
            { /* File may be locked */
            }
        }
    }
}
