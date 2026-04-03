namespace Nimblesite.Sync.Tests;

/// <summary>
/// Tests for Nimblesite.Sync.CoreError types.
/// </summary>
public sealed class Nimblesite.Sync.CoreErrorTests
{
    [Fact]
    public void Nimblesite.Sync.CoreErrorForeignKeyViolation_CreatesCorrectRecord()
    {
        var error = new Nimblesite.Sync.CoreErrorForeignKeyViolation(
            "Person",
            "{\"Id\":\"p1\"}",
            "FK_Person_Department"
        );

        Assert.Equal("Person", error.TableName);
        Assert.Equal("{\"Id\":\"p1\"}", error.PkValue);
        Assert.Equal("FK_Person_Department", error.Details);
    }

    [Fact]
    public void Nimblesite.Sync.CoreErrorDeferredChangeFailed_CreatesCorrectRecord()
    {
        var entry = new Nimblesite.Sync.CoreLogEntry(
            1,
            "Person",
            "{\"Id\":\"p1\"}",
            Nimblesite.Sync.CoreOperation.Insert,
            "{\"Id\":\"p1\",\"Name\":\"Alice\"}",
            "origin-1",
            "2024-01-01T00:00:00Z"
        );

        var error = new Nimblesite.Sync.CoreErrorDeferredChangeFailed(entry, "Max retries exceeded");

        Assert.Equal(entry, error.Entry);
        Assert.Equal("Max retries exceeded", error.Reason);
    }

    [Fact]
    public void Nimblesite.Sync.CoreErrorFullResyncRequired_CreatesCorrectRecord()
    {
        var error = new Nimblesite.Sync.CoreErrorFullResyncRequired(100, 500);

        Assert.Equal(100, error.ClientVersion);
        Assert.Equal(500, error.OldestAvailableVersion);
    }

    [Fact]
    public void Nimblesite.Sync.CoreErrorHashMismatch_CreatesCorrectRecord()
    {
        var error = new Nimblesite.Sync.CoreErrorHashMismatch("abc123", "def456");

        Assert.Equal("abc123", error.ExpectedHash);
        Assert.Equal("def456", error.ActualHash);
    }

    [Fact]
    public void Nimblesite.Sync.CoreErrorDatabase_CreatesCorrectRecord()
    {
        var error = new Nimblesite.Sync.CoreErrorDatabase("Connection failed");

        Assert.Equal("Connection failed", error.Message);
    }

    [Fact]
    public void Nimblesite.Sync.CoreErrorUnresolvedConflict_CreatesCorrectRecord()
    {
        var localChange = new Nimblesite.Sync.CoreLogEntry(
            1,
            "Person",
            "{\"Id\":\"p1\"}",
            Nimblesite.Sync.CoreOperation.Update,
            "{\"Id\":\"p1\",\"Name\":\"LocalName\"}",
            "local-origin",
            "2024-01-01T00:00:00Z"
        );

        var remoteChange = new Nimblesite.Sync.CoreLogEntry(
            2,
            "Person",
            "{\"Id\":\"p1\"}",
            Nimblesite.Sync.CoreOperation.Update,
            "{\"Id\":\"p1\",\"Name\":\"RemoteName\"}",
            "remote-origin",
            "2024-01-01T00:00:01Z"
        );

        var error = new Nimblesite.Sync.CoreErrorUnresolvedConflict(localChange, remoteChange);

        Assert.Equal(localChange, error.LocalChange);
        Assert.Equal(remoteChange, error.RemoteChange);
    }

    [Fact]
    public void AllSyncErrors_DeriveFromSyncError()
    {
        var errors = new Nimblesite.Sync.CoreError[]
        {
            new Nimblesite.Sync.CoreErrorForeignKeyViolation("T", "PK", "Details"),
            new Nimblesite.Sync.CoreErrorDeferredChangeFailed(
                new Nimblesite.Sync.CoreLogEntry(1, "T", "PK", Nimblesite.Sync.CoreOperation.Insert, "{}", "O", "TS"),
                "Reason"
            ),
            new Nimblesite.Sync.CoreErrorFullResyncRequired(1, 100),
            new Nimblesite.Sync.CoreErrorHashMismatch("E", "A"),
            new Nimblesite.Sync.CoreErrorDatabase("Msg"),
            new Nimblesite.Sync.CoreErrorUnresolvedConflict(
                new Nimblesite.Sync.CoreLogEntry(1, "T", "PK", Nimblesite.Sync.CoreOperation.Update, "{}", "O1", "TS1"),
                new Nimblesite.Sync.CoreLogEntry(2, "T", "PK", Nimblesite.Sync.CoreOperation.Update, "{}", "O2", "TS2")
            ),
        };

        foreach (var error in errors)
        {
            Assert.IsAssignableFrom<Nimblesite.Sync.CoreError>(error);
        }
    }

    [Fact]
    public void Nimblesite.Sync.CoreErrors_CanBePatternMatched()
    {
        Nimblesite.Sync.CoreError error = new Nimblesite.Sync.CoreErrorDatabase("test");

        var result = error switch
        {
            Nimblesite.Sync.CoreErrorForeignKeyViolation fk => $"FK: {fk.TableName}",
            Nimblesite.Sync.CoreErrorDeferredChangeFailed dc => $"Deferred: {dc.Reason}",
            Nimblesite.Sync.CoreErrorFullResyncRequired fr => $"Resync: {fr.ClientVersion}",
            Nimblesite.Sync.CoreErrorHashMismatch hm => $"Hash: {hm.ExpectedHash}",
            Nimblesite.Sync.CoreErrorDatabase db => $"DB: {db.Message}",
            Nimblesite.Sync.CoreErrorUnresolvedConflict uc => $"Conflict: {uc.LocalChange.TableName}",
            _ => "Unknown",
        };

        Assert.Equal("DB: test", result);
    }
}
