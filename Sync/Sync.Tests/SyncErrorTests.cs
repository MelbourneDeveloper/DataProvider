namespace Sync.Tests;

/// <summary>
/// Tests for SyncError types.
/// </summary>
public sealed class SyncErrorTests
{
    [Fact]
    public void SyncErrorForeignKeyViolation_CreatesCorrectRecord()
    {
        var error = new SyncErrorForeignKeyViolation(
            "Person",
            "{\"Id\":\"p1\"}",
            "FK_Person_Department"
        );

        Assert.Equal("Person", error.TableName);
        Assert.Equal("{\"Id\":\"p1\"}", error.PkValue);
        Assert.Equal("FK_Person_Department", error.Details);
    }

    [Fact]
    public void SyncErrorDeferredChangeFailed_CreatesCorrectRecord()
    {
        var entry = new SyncLogEntry(
            1,
            "Person",
            "{\"Id\":\"p1\"}",
            SyncOperation.Insert,
            "{\"Id\":\"p1\",\"Name\":\"Alice\"}",
            "origin-1",
            "2024-01-01T00:00:00Z"
        );

        var error = new SyncErrorDeferredChangeFailed(entry, "Max retries exceeded");

        Assert.Equal(entry, error.Entry);
        Assert.Equal("Max retries exceeded", error.Reason);
    }

    [Fact]
    public void SyncErrorFullResyncRequired_CreatesCorrectRecord()
    {
        var error = new SyncErrorFullResyncRequired(100, 500);

        Assert.Equal(100, error.ClientVersion);
        Assert.Equal(500, error.OldestAvailableVersion);
    }

    [Fact]
    public void SyncErrorHashMismatch_CreatesCorrectRecord()
    {
        var error = new SyncErrorHashMismatch("abc123", "def456");

        Assert.Equal("abc123", error.ExpectedHash);
        Assert.Equal("def456", error.ActualHash);
    }

    [Fact]
    public void SyncErrorDatabase_CreatesCorrectRecord()
    {
        var error = new SyncErrorDatabase("Connection failed");

        Assert.Equal("Connection failed", error.Message);
    }

    [Fact]
    public void SyncErrorUnresolvedConflict_CreatesCorrectRecord()
    {
        var localChange = new SyncLogEntry(
            1,
            "Person",
            "{\"Id\":\"p1\"}",
            SyncOperation.Update,
            "{\"Id\":\"p1\",\"Name\":\"LocalName\"}",
            "local-origin",
            "2024-01-01T00:00:00Z"
        );

        var remoteChange = new SyncLogEntry(
            2,
            "Person",
            "{\"Id\":\"p1\"}",
            SyncOperation.Update,
            "{\"Id\":\"p1\",\"Name\":\"RemoteName\"}",
            "remote-origin",
            "2024-01-01T00:00:01Z"
        );

        var error = new SyncErrorUnresolvedConflict(localChange, remoteChange);

        Assert.Equal(localChange, error.LocalChange);
        Assert.Equal(remoteChange, error.RemoteChange);
    }

    [Fact]
    public void AllSyncErrors_DeriveFromSyncError()
    {
        var errors = new SyncError[]
        {
            new SyncErrorForeignKeyViolation("T", "PK", "Details"),
            new SyncErrorDeferredChangeFailed(
                new SyncLogEntry(1, "T", "PK", SyncOperation.Insert, "{}", "O", "TS"),
                "Reason"
            ),
            new SyncErrorFullResyncRequired(1, 100),
            new SyncErrorHashMismatch("E", "A"),
            new SyncErrorDatabase("Msg"),
            new SyncErrorUnresolvedConflict(
                new SyncLogEntry(1, "T", "PK", SyncOperation.Update, "{}", "O1", "TS1"),
                new SyncLogEntry(2, "T", "PK", SyncOperation.Update, "{}", "O2", "TS2")
            ),
        };

        foreach (var error in errors)
        {
            Assert.IsAssignableFrom<SyncError>(error);
        }
    }

    [Fact]
    public void SyncErrors_CanBePatternMatched()
    {
        SyncError error = new SyncErrorDatabase("test");

        var result = error switch
        {
            SyncErrorForeignKeyViolation fk => $"FK: {fk.TableName}",
            SyncErrorDeferredChangeFailed dc => $"Deferred: {dc.Reason}",
            SyncErrorFullResyncRequired fr => $"Resync: {fr.ClientVersion}",
            SyncErrorHashMismatch hm => $"Hash: {hm.ExpectedHash}",
            SyncErrorDatabase db => $"DB: {db.Message}",
            SyncErrorUnresolvedConflict uc => $"Conflict: {uc.LocalChange.TableName}",
            _ => "Unknown",
        };

        Assert.Equal("DB: test", result);
    }
}
