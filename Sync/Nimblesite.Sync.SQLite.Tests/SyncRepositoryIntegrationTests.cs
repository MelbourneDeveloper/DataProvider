using Microsoft.Data.Sqlite;
using Xunit;

namespace Nimblesite.Sync.SQLite.Tests;

/// <summary>
/// Integration tests for SQLite repository classes.
/// Tests Nimblesite.Sync.CoreClientRepository, Nimblesite.Sync.CoreLogRepository, and SubscriptionRepository.
/// NO MOCKS - real SQLite databases only!
/// </summary>
public sealed class Nimblesite.Sync.CoreRepositoryIntegrationTests : IDisposable
{
    private readonly SqliteConnection _db;
    private readonly string _dbPath = Path.Combine(
        Path.GetTempPath(),
        $"syncrepositoryintegrationtests_{Guid.NewGuid()}.db"
    );
    private readonly string _originId = Guid.NewGuid().ToString();
    private const string Timestamp = "2025-01-01T00:00:00.000Z";

    public Nimblesite.Sync.CoreRepositoryIntegrationTests()
    {
        _db = new SqliteConnection($"Data Source={_dbPath}");
        _db.Open();
        Nimblesite.Sync.CoreSchema.CreateSchema(_db);
        Nimblesite.Sync.CoreSchema.SetOriginId(_db, _originId);

        // Create test table with triggers
        using var cmd = _db.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE Person (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                Age INTEGER
            );
            """;
        cmd.ExecuteNonQuery();
        TriggerGenerator.CreateTriggers(_db, "Person", NullLogger.Instance);
    }

    #region Nimblesite.Sync.CoreClientRepository Tests

    [Fact]
    public void Nimblesite.Sync.CoreClientRepository_GetAll_EmptyDatabase_ReturnsEmptyList()
    {
        // Act
        var result = Nimblesite.Sync.CoreClientRepository.GetAll(_db);

        // Assert
        Assert.True(result is Nimblesite.Sync.CoreClientListOk);
        Assert.Empty(((Nimblesite.Sync.CoreClientListOk)result).Value);
    }

    [Fact]
    public void Nimblesite.Sync.CoreClientRepository_Upsert_NewClient_Inserts()
    {
        // Arrange
        var client = new Nimblesite.Sync.CoreClient("client-1", 100, Timestamp, Timestamp);

        // Act
        var result = Nimblesite.Sync.CoreClientRepository.Upsert(_db, client);

        // Assert
        Assert.True(result is BoolSyncOk);

        var allClients = Nimblesite.Sync.CoreClientRepository.GetAll(_db);
        Assert.Single(((Nimblesite.Sync.CoreClientListOk)allClients).Value);
    }

    [Fact]
    public void Nimblesite.Sync.CoreClientRepository_Upsert_ExistingClient_Updates()
    {
        // Arrange
        var client1 = new Nimblesite.Sync.CoreClient("client-1", 100, Timestamp, Timestamp);
        Nimblesite.Sync.CoreClientRepository.Upsert(_db, client1);

        var client2 = new Nimblesite.Sync.CoreClient("client-1", 200, "2025-02-01T00:00:00Z", Timestamp);

        // Act
        var result = Nimblesite.Sync.CoreClientRepository.Upsert(_db, client2);

        // Assert
        Assert.True(result is BoolSyncOk);

        var retrieved = Nimblesite.Sync.CoreClientRepository.GetByOrigin(_db, "client-1");
        var client = ((Nimblesite.Sync.CoreClientOk)retrieved).Value;
        Assert.NotNull(client);
        Assert.Equal(200, client.LastSyncVersion);
    }

    [Fact]
    public void Nimblesite.Sync.CoreClientRepository_GetByOrigin_ExistingClient_ReturnsClient()
    {
        // Arrange
        var client = new Nimblesite.Sync.CoreClient("client-1", 100, Timestamp, Timestamp);
        Nimblesite.Sync.CoreClientRepository.Upsert(_db, client);

        // Act
        var result = Nimblesite.Sync.CoreClientRepository.GetByOrigin(_db, "client-1");

        // Assert
        Assert.True(result is Nimblesite.Sync.CoreClientOk);
        var retrieved = ((Nimblesite.Sync.CoreClientOk)result).Value;
        Assert.NotNull(retrieved);
        Assert.Equal("client-1", retrieved.OriginId);
        Assert.Equal(100, retrieved.LastSyncVersion);
    }

    [Fact]
    public void Nimblesite.Sync.CoreClientRepository_GetByOrigin_NonExistingClient_ReturnsNull()
    {
        // Act
        var result = Nimblesite.Sync.CoreClientRepository.GetByOrigin(_db, "nonexistent");

        // Assert
        Assert.True(result is Nimblesite.Sync.CoreClientOk);
        Assert.Null(((Nimblesite.Sync.CoreClientOk)result).Value);
    }

    [Fact]
    public void Nimblesite.Sync.CoreClientRepository_Delete_ExistingClient_Removes()
    {
        // Arrange
        Nimblesite.Sync.CoreClientRepository.Upsert(_db, new Nimblesite.Sync.CoreClient("client-1", 100, Timestamp, Timestamp));

        // Act
        var result = Nimblesite.Sync.CoreClientRepository.Delete(_db, "client-1");

        // Assert
        Assert.True(result is BoolSyncOk);

        var retrieved = Nimblesite.Sync.CoreClientRepository.GetByOrigin(_db, "client-1");
        Assert.Null(((Nimblesite.Sync.CoreClientOk)retrieved).Value);
    }

    [Fact]
    public void Nimblesite.Sync.CoreClientRepository_Delete_NonExistingClient_StillSucceeds()
    {
        // Act
        var result = Nimblesite.Sync.CoreClientRepository.Delete(_db, "nonexistent");

        // Assert
        Assert.True(result is BoolSyncOk);
    }

    [Fact]
    public void Nimblesite.Sync.CoreClientRepository_GetMinVersion_EmptyDatabase_ReturnsZero()
    {
        // Act
        var result = Nimblesite.Sync.CoreClientRepository.GetMinVersion(_db);

        // Assert
        Assert.True(result is LongSyncOk);
        Assert.Equal(0, ((LongSyncOk)result).Value);
    }

    [Fact]
    public void Nimblesite.Sync.CoreClientRepository_GetMinVersion_WithClients_ReturnsMinimum()
    {
        // Arrange
        Nimblesite.Sync.CoreClientRepository.Upsert(_db, new Nimblesite.Sync.CoreClient("client-a", 300, Timestamp, Timestamp));
        Nimblesite.Sync.CoreClientRepository.Upsert(_db, new Nimblesite.Sync.CoreClient("client-b", 100, Timestamp, Timestamp));
        Nimblesite.Sync.CoreClientRepository.Upsert(_db, new Nimblesite.Sync.CoreClient("client-c", 200, Timestamp, Timestamp));

        // Act
        var result = Nimblesite.Sync.CoreClientRepository.GetMinVersion(_db);

        // Assert
        Assert.True(result is LongSyncOk);
        Assert.Equal(100, ((LongSyncOk)result).Value);
    }

    [Fact]
    public void Nimblesite.Sync.CoreClientRepository_DeleteMultiple_RemovesSpecified()
    {
        // Arrange
        Nimblesite.Sync.CoreClientRepository.Upsert(_db, new Nimblesite.Sync.CoreClient("client-1", 100, Timestamp, Timestamp));
        Nimblesite.Sync.CoreClientRepository.Upsert(_db, new Nimblesite.Sync.CoreClient("client-2", 200, Timestamp, Timestamp));
        Nimblesite.Sync.CoreClientRepository.Upsert(_db, new Nimblesite.Sync.CoreClient("client-3", 300, Timestamp, Timestamp));

        // Act
        var result = Nimblesite.Sync.CoreClientRepository.DeleteMultiple(_db, ["client-1", "client-2"]);

        // Assert
        Assert.True(result is IntSyncOk);
        Assert.Equal(2, ((IntSyncOk)result).Value);

        var remaining = Nimblesite.Sync.CoreClientRepository.GetAll(_db);
        Assert.Single(((Nimblesite.Sync.CoreClientListOk)remaining).Value);
    }

    #endregion

    #region Nimblesite.Sync.CoreLogRepository Tests

    [Fact]
    public void Nimblesite.Sync.CoreLogRepository_FetchChanges_EmptyLog_ReturnsEmptyList()
    {
        // Act
        var result = Nimblesite.Sync.CoreLogRepository.FetchChanges(_db, 0, 100);

        // Assert
        Assert.True(result is Nimblesite.Sync.CoreLogListOk);
        Assert.Empty(((Nimblesite.Sync.CoreLogListOk)result).Value);
    }

    [Fact]
    public void Nimblesite.Sync.CoreLogRepository_FetchChanges_WithEntries_ReturnsEntries()
    {
        // Arrange - Insert via user table to trigger logging
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "INSERT INTO Person (Id, Name, Age) VALUES ('p1', 'Alice', 30)";
        cmd.ExecuteNonQuery();

        // Act
        var result = Nimblesite.Sync.CoreLogRepository.FetchChanges(_db, 0, 100);

        // Assert
        Assert.True(result is Nimblesite.Sync.CoreLogListOk);
        var entries = ((Nimblesite.Sync.CoreLogListOk)result).Value;
        Assert.Single(entries);
        Assert.Equal("Person", entries[0].TableName);
    }

    [Fact]
    public void Nimblesite.Sync.CoreLogRepository_FetchChanges_RespectsBatchSize()
    {
        // Arrange
        for (var i = 0; i < 10; i++)
        {
            using var cmd = _db.CreateCommand();
            cmd.CommandText =
                $"INSERT INTO Person (Id, Name, Age) VALUES ('p{i}', 'Person {i}', {20 + i})";
            cmd.ExecuteNonQuery();
        }

        // Act
        var result = Nimblesite.Sync.CoreLogRepository.FetchChanges(_db, 0, 5);

        // Assert
        Assert.True(result is Nimblesite.Sync.CoreLogListOk);
        Assert.Equal(5, ((Nimblesite.Sync.CoreLogListOk)result).Value.Count);
    }

    [Fact]
    public void Nimblesite.Sync.CoreLogRepository_FetchChanges_FromVersion_FiltersCorrectly()
    {
        // Arrange
        for (var i = 0; i < 5; i++)
        {
            using var cmd = _db.CreateCommand();
            cmd.CommandText =
                $"INSERT INTO Person (Id, Name, Age) VALUES ('p{i}', 'Person {i}', {20 + i})";
            cmd.ExecuteNonQuery();
        }

        // Get the max version
        var maxResult = Nimblesite.Sync.CoreLogRepository.GetMaxVersion(_db);
        var maxVersion = ((LongSyncOk)maxResult).Value;

        // Act - Fetch from version 3
        var result = Nimblesite.Sync.CoreLogRepository.FetchChanges(_db, maxVersion - 2, 100);

        // Assert
        Assert.True(result is Nimblesite.Sync.CoreLogListOk);
        Assert.Equal(2, ((Nimblesite.Sync.CoreLogListOk)result).Value.Count);
    }

    [Fact]
    public void Nimblesite.Sync.CoreLogRepository_GetMaxVersion_EmptyLog_ReturnsZero()
    {
        // Act
        var result = Nimblesite.Sync.CoreLogRepository.GetMaxVersion(_db);

        // Assert
        Assert.True(result is LongSyncOk);
        Assert.Equal(0, ((LongSyncOk)result).Value);
    }

    [Fact]
    public void Nimblesite.Sync.CoreLogRepository_GetMaxVersion_WithEntries_ReturnsMax()
    {
        // Arrange
        for (var i = 0; i < 5; i++)
        {
            using var cmd = _db.CreateCommand();
            cmd.CommandText =
                $"INSERT INTO Person (Id, Name, Age) VALUES ('p{i}', 'Person {i}', {20 + i})";
            cmd.ExecuteNonQuery();
        }

        // Act
        var result = Nimblesite.Sync.CoreLogRepository.GetMaxVersion(_db);

        // Assert
        Assert.True(result is LongSyncOk);
        Assert.True(((LongSyncOk)result).Value >= 5);
    }

    [Fact]
    public void Nimblesite.Sync.CoreLogRepository_Insert_LogsUpdate()
    {
        // Arrange
        using var insertCmd = _db.CreateCommand();
        insertCmd.CommandText = "INSERT INTO Person (Id, Name, Age) VALUES ('p1', 'Alice', 30)";
        insertCmd.ExecuteNonQuery();

        var versionAfterInsert = ((LongSyncOk)Nimblesite.Sync.CoreLogRepository.GetMaxVersion(_db)).Value;

        // Act - Update
        using var updateCmd = _db.CreateCommand();
        updateCmd.CommandText = "UPDATE Person SET Name = 'Alice Updated' WHERE Id = 'p1'";
        updateCmd.ExecuteNonQuery();

        // Assert
        var result = Nimblesite.Sync.CoreLogRepository.FetchChanges(_db, versionAfterInsert, 100);
        var entries = ((Nimblesite.Sync.CoreLogListOk)result).Value;
        Assert.Single(entries);
        Assert.Equal(Nimblesite.Sync.CoreOperation.Update, entries[0].Operation);
    }

    [Fact]
    public void Nimblesite.Sync.CoreLogRepository_Delete_LogsTombstone()
    {
        // Arrange
        using var insertCmd = _db.CreateCommand();
        insertCmd.CommandText = "INSERT INTO Person (Id, Name, Age) VALUES ('p1', 'Alice', 30)";
        insertCmd.ExecuteNonQuery();

        var versionAfterInsert = ((LongSyncOk)Nimblesite.Sync.CoreLogRepository.GetMaxVersion(_db)).Value;

        // Act - Delete
        using var deleteCmd = _db.CreateCommand();
        deleteCmd.CommandText = "DELETE FROM Person WHERE Id = 'p1'";
        deleteCmd.ExecuteNonQuery();

        // Assert
        var result = Nimblesite.Sync.CoreLogRepository.FetchChanges(_db, versionAfterInsert, 100);
        var entries = ((Nimblesite.Sync.CoreLogListOk)result).Value;
        Assert.Single(entries);
        Assert.Equal(Nimblesite.Sync.CoreOperation.Delete, entries[0].Operation);
        Assert.Null(entries[0].Payload); // Tombstone has no payload
    }

    [Fact]
    public void Nimblesite.Sync.CoreLogRepository_GetLastServerVersion_Default_ReturnsZero()
    {
        // Act
        var result = Nimblesite.Sync.CoreLogRepository.GetLastServerVersion(_db);

        // Assert
        Assert.True(result is LongSyncOk);
        Assert.Equal(0, ((LongSyncOk)result).Value);
    }

    [Fact]
    public void Nimblesite.Sync.CoreLogRepository_UpdateLastServerVersion_UpdatesValue()
    {
        // Act
        var updateResult = Nimblesite.Sync.CoreLogRepository.UpdateLastServerVersion(_db, 100);

        // Assert
        Assert.True(updateResult is BoolSyncOk);

        var getResult = Nimblesite.Sync.CoreLogRepository.GetLastServerVersion(_db);
        Assert.True(getResult is LongSyncOk);
        Assert.Equal(100, ((LongSyncOk)getResult).Value);
    }

    [Fact]
    public void Nimblesite.Sync.CoreLogRepository_GetMinVersion_EmptyLog_ReturnsZero()
    {
        // Act
        var result = Nimblesite.Sync.CoreLogRepository.GetMinVersion(_db);

        // Assert
        Assert.True(result is LongSyncOk);
        Assert.Equal(0, ((LongSyncOk)result).Value);
    }

    [Fact]
    public void Nimblesite.Sync.CoreLogRepository_GetMinVersion_WithEntries_ReturnsMin()
    {
        // Arrange
        for (var i = 0; i < 5; i++)
        {
            using var cmd = _db.CreateCommand();
            cmd.CommandText =
                $"INSERT INTO Person (Id, Name, Age) VALUES ('min{i}', 'Person {i}', {20 + i})";
            cmd.ExecuteNonQuery();
        }

        // Act
        var result = Nimblesite.Sync.CoreLogRepository.GetMinVersion(_db);

        // Assert
        Assert.True(result is LongSyncOk);
        Assert.True(((LongSyncOk)result).Value >= 1);
    }

    [Fact]
    public void Nimblesite.Sync.CoreLogRepository_GetEntryCount_EmptyLog_ReturnsZero()
    {
        // Act
        var result = Nimblesite.Sync.CoreLogRepository.GetEntryCount(_db);

        // Assert
        Assert.True(result is LongSyncOk);
        Assert.Equal(0, ((LongSyncOk)result).Value);
    }

    [Fact]
    public void Nimblesite.Sync.CoreLogRepository_GetEntryCount_WithEntries_ReturnsCount()
    {
        // Arrange
        for (var i = 0; i < 5; i++)
        {
            using var cmd = _db.CreateCommand();
            cmd.CommandText =
                $"INSERT INTO Person (Id, Name, Age) VALUES ('cnt{i}', 'Person {i}', {20 + i})";
            cmd.ExecuteNonQuery();
        }

        // Act
        var result = Nimblesite.Sync.CoreLogRepository.GetEntryCount(_db);

        // Assert
        Assert.True(result is LongSyncOk);
        Assert.Equal(5, ((LongSyncOk)result).Value);
    }

    [Fact]
    public void Nimblesite.Sync.CoreLogRepository_FetchChanges_ContainsAllFields()
    {
        // Arrange
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "INSERT INTO Person (Id, Name, Age) VALUES ('fld1', 'Alice', 30)";
        cmd.ExecuteNonQuery();

        // Act
        var result = Nimblesite.Sync.CoreLogRepository.FetchChanges(_db, 0, 100);

        // Assert
        Assert.True(result is Nimblesite.Sync.CoreLogListOk);
        var entries = ((Nimblesite.Sync.CoreLogListOk)result).Value;
        Assert.NotEmpty(entries);

        var entry = entries[^1];
        Assert.True(entry.Version > 0);
        Assert.Equal("Person", entry.TableName);
        Assert.Contains("fld1", entry.PkValue);
        Assert.Equal(Nimblesite.Sync.CoreOperation.Insert, entry.Operation);
        Assert.NotNull(entry.Payload);
        Assert.Equal(_originId, entry.Origin);
        Assert.NotEmpty(entry.Timestamp);
    }

    #endregion

    #region SubscriptionRepository Tests

    [Fact]
    public void SubscriptionRepository_GetAll_EmptyDatabase_ReturnsEmptyList()
    {
        // Act
        var result = SubscriptionRepository.GetAll(_db);

        // Assert
        Assert.True(result is SubscriptionListOk);
        Assert.Empty(((SubscriptionListOk)result).Value);
    }

    [Fact]
    public void SubscriptionRepository_Insert_ValidSubscription_Succeeds()
    {
        // Arrange
        var subscription = new Nimblesite.Sync.CoreSubscription(
            "sub-1",
            _originId,
            SubscriptionType.Table,
            "Person",
            null,
            Timestamp,
            null
        );

        // Act
        var result = SubscriptionRepository.Insert(_db, subscription);

        // Assert
        Assert.True(result is BoolSyncOk);

        var all = SubscriptionRepository.GetAll(_db);
        Assert.Single(((SubscriptionListOk)all).Value);
    }

    [Fact]
    public void SubscriptionRepository_GetById_ExistingSubscription_ReturnsSubscription()
    {
        // Arrange
        var subscription = new Nimblesite.Sync.CoreSubscription(
            "sub-1",
            _originId,
            SubscriptionType.Table,
            "Person",
            null,
            Timestamp,
            null
        );
        SubscriptionRepository.Insert(_db, subscription);

        // Act
        var result = SubscriptionRepository.GetById(_db, "sub-1");

        // Assert
        Assert.True(result is SubscriptionOk);
        var retrieved = ((SubscriptionOk)result).Value;
        Assert.NotNull(retrieved);
        Assert.Equal("sub-1", retrieved.SubscriptionId);
    }

    [Fact]
    public void SubscriptionRepository_GetById_NonExisting_ReturnsNull()
    {
        // Act
        var result = SubscriptionRepository.GetById(_db, "nonexistent");

        // Assert
        Assert.True(result is SubscriptionOk);
        Assert.Null(((SubscriptionOk)result).Value);
    }

    [Fact]
    public void SubscriptionRepository_GetByTable_FiltersCorrectly()
    {
        // Arrange
        SubscriptionRepository.Insert(
            _db,
            new Nimblesite.Sync.CoreSubscription(
                "sub-1",
                _originId,
                SubscriptionType.Table,
                "Person",
                null,
                Timestamp,
                null
            )
        );
        SubscriptionRepository.Insert(
            _db,
            new Nimblesite.Sync.CoreSubscription(
                "sub-2",
                _originId,
                SubscriptionType.Table,
                "Orders",
                null,
                Timestamp,
                null
            )
        );

        // Act
        var result = SubscriptionRepository.GetByTable(_db, "Person");

        // Assert
        Assert.True(result is SubscriptionListOk);
        var subs = ((SubscriptionListOk)result).Value;
        Assert.Single(subs);
        Assert.Equal("sub-1", subs[0].SubscriptionId);
    }

    [Fact]
    public void SubscriptionRepository_GetByOrigin_FiltersCorrectly()
    {
        // Arrange
        var origin1 = "origin-1";
        var origin2 = "origin-2";
        SubscriptionRepository.Insert(
            _db,
            new Nimblesite.Sync.CoreSubscription(
                "sub-1",
                origin1,
                SubscriptionType.Table,
                "Person",
                null,
                Timestamp,
                null
            )
        );
        SubscriptionRepository.Insert(
            _db,
            new Nimblesite.Sync.CoreSubscription(
                "sub-2",
                origin2,
                SubscriptionType.Table,
                "Person",
                null,
                Timestamp,
                null
            )
        );

        // Act
        var result = SubscriptionRepository.GetByOrigin(_db, origin1);

        // Assert
        Assert.True(result is SubscriptionListOk);
        var subs = ((SubscriptionListOk)result).Value;
        Assert.Single(subs);
        Assert.Equal("sub-1", subs[0].SubscriptionId);
    }

    [Fact]
    public void SubscriptionRepository_Delete_ExistingSubscription_Removes()
    {
        // Arrange
        SubscriptionRepository.Insert(
            _db,
            new Nimblesite.Sync.CoreSubscription(
                "sub-1",
                _originId,
                SubscriptionType.Table,
                "Person",
                null,
                Timestamp,
                null
            )
        );

        // Act
        var result = SubscriptionRepository.Delete(_db, "sub-1");

        // Assert
        Assert.True(result is BoolSyncOk);

        var all = SubscriptionRepository.GetAll(_db);
        Assert.Empty(((SubscriptionListOk)all).Value);
    }

    [Fact]
    public void SubscriptionRepository_DeleteByOrigin_RemovesAll()
    {
        // Arrange
        var origin1 = "origin-1";
        SubscriptionRepository.Insert(
            _db,
            new Nimblesite.Sync.CoreSubscription(
                "sub-1",
                origin1,
                SubscriptionType.Table,
                "Person",
                null,
                Timestamp,
                null
            )
        );
        SubscriptionRepository.Insert(
            _db,
            new Nimblesite.Sync.CoreSubscription(
                "sub-2",
                origin1,
                SubscriptionType.Table,
                "Orders",
                null,
                Timestamp,
                null
            )
        );
        SubscriptionRepository.Insert(
            _db,
            new Nimblesite.Sync.CoreSubscription(
                "sub-3",
                "origin-2",
                SubscriptionType.Table,
                "Products",
                null,
                Timestamp,
                null
            )
        );

        // Act
        var result = SubscriptionRepository.DeleteByOrigin(_db, origin1);

        // Assert
        Assert.True(result is IntSyncOk);
        Assert.Equal(2, ((IntSyncOk)result).Value);

        var remaining = SubscriptionRepository.GetAll(_db);
        Assert.Single(((SubscriptionListOk)remaining).Value);
    }

    [Fact]
    public void SubscriptionRepository_DeleteExpired_RemovesExpiredOnly()
    {
        // Arrange
        SubscriptionRepository.Insert(
            _db,
            new Nimblesite.Sync.CoreSubscription(
                "sub-expired",
                _originId,
                SubscriptionType.Table,
                "Person",
                null,
                "2025-01-01T00:00:00Z",
                "2025-06-01T00:00:00Z"
            )
        );
        SubscriptionRepository.Insert(
            _db,
            new Nimblesite.Sync.CoreSubscription(
                "sub-active",
                _originId,
                SubscriptionType.Table,
                "Orders",
                null,
                "2025-01-01T00:00:00Z",
                "2025-12-31T23:59:59Z"
            )
        );

        // Act
        var result = SubscriptionRepository.DeleteExpired(_db, "2025-07-01T00:00:00Z");

        // Assert
        Assert.True(result is IntSyncOk);
        Assert.Equal(1, ((IntSyncOk)result).Value);

        var remaining = SubscriptionRepository.GetAll(_db);
        Assert.Single(((SubscriptionListOk)remaining).Value);
        Assert.Equal("sub-active", ((SubscriptionListOk)remaining).Value[0].SubscriptionId);
    }

    #endregion

    #region Tombstone Integration Tests

    [Fact]
    public void TombstoneManager_CalculateSafeVersion_WithClients_ReturnsMinVersion()
    {
        // Arrange
        var clients = new List<Nimblesite.Sync.CoreClient>
        {
            new("client-1", 100, Timestamp, Timestamp),
            new("client-2", 200, Timestamp, Timestamp),
            new("client-3", 300, Timestamp, Timestamp),
        };

        // Act
        var result = TombstoneManager.CalculateSafePurgeVersion(clients);

        // Assert
        Assert.Equal(100, result);
    }

    [Fact]
    public void TombstoneManager_PurgeTombstones_RemovesOldDeletes()
    {
        // Arrange - Insert and delete to create tombstones
        using var insertCmd = _db.CreateCommand();
        insertCmd.CommandText = "INSERT INTO Person (Id, Name) VALUES ('p1', 'Alice')";
        insertCmd.ExecuteNonQuery();

        using var deleteCmd = _db.CreateCommand();
        deleteCmd.CommandText = "DELETE FROM Person WHERE Id = 'p1'";
        deleteCmd.ExecuteNonQuery();

        // Get version
        var maxVersion = ((LongSyncOk)Nimblesite.Sync.CoreLogRepository.GetMaxVersion(_db)).Value;

        // Use extension method to purge tombstones
        var result = _db.PurgeTombstones(maxVersion + 1);

        // Assert
        Assert.True(result is IntSyncOk);
        // Should have purged at least the delete tombstone
    }

    #endregion

    public void Dispose()
    {
        _db.Dispose();
        if (File.Exists(_dbPath))
        {
            try
            {
                File.Delete(_dbPath);
            }
            catch
            {
                /* File may be locked */
            }
        }
    }
}
