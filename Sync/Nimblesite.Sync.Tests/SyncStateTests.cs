namespace Nimblesite.Sync.Tests;

/// <summary>
/// Tests for Nimblesite.Sync.CoreState, Nimblesite.Sync.CoreSession, and Nimblesite.Sync.CoreClient records.
/// </summary>
public sealed class Nimblesite.Sync.CoreStateTests
{
    [Fact]
    public void Nimblesite.Sync.CoreState_CreatesCorrectRecord()
    {
        var state = new Nimblesite.Sync.CoreState("origin-123", 100, 50);

        Assert.Equal("origin-123", state.OriginId);
        Assert.Equal(100, state.LastServerVersion);
        Assert.Equal(50, state.LastPushVersion);
    }

    [Fact]
    public void Nimblesite.Sync.CoreState_RecordEquality()
    {
        var state1 = new Nimblesite.Sync.CoreState("origin-123", 100, 50);
        var state2 = new Nimblesite.Sync.CoreState("origin-123", 100, 50);
        var state3 = new Nimblesite.Sync.CoreState("origin-456", 100, 50);

        Assert.Equal(state1, state2);
        Assert.NotEqual(state1, state3);
    }

    [Fact]
    public void Nimblesite.Sync.CoreState_With_CreatesModifiedCopy()
    {
        var state = new Nimblesite.Sync.CoreState("origin-123", 100, 50);
        var updated = state with { LastServerVersion = 200 };

        Assert.Equal("origin-123", updated.OriginId);
        Assert.Equal(200, updated.LastServerVersion);
        Assert.Equal(50, updated.LastPushVersion);
        Assert.NotEqual(state.LastServerVersion, updated.LastServerVersion);
    }

    [Fact]
    public void Nimblesite.Sync.CoreSession_CreatesCorrectRecord()
    {
        var session = new Nimblesite.Sync.CoreSession(true);
        Assert.True(session.Nimblesite.Sync.CoreActive);

        var sessionInactive = new Nimblesite.Sync.CoreSession(false);
        Assert.False(sessionInactive.Nimblesite.Sync.CoreActive);
    }

    [Fact]
    public void Nimblesite.Sync.CoreSession_RecordEquality()
    {
        var session1 = new Nimblesite.Sync.CoreSession(true);
        var session2 = new Nimblesite.Sync.CoreSession(true);
        var session3 = new Nimblesite.Sync.CoreSession(false);

        Assert.Equal(session1, session2);
        Assert.NotEqual(session1, session3);
    }

    [Fact]
    public void Nimblesite.Sync.CoreClient_CreatesCorrectRecord()
    {
        var client = new Nimblesite.Sync.CoreClient(
            "client-001",
            100,
            "2024-01-01T00:00:00Z",
            "2024-01-01T00:00:00Z"
        );

        Assert.Equal("client-001", client.OriginId);
        Assert.Equal(100, client.LastSyncVersion);
        Assert.Equal("2024-01-01T00:00:00Z", client.LastSyncTimestamp);
        Assert.Equal("2024-01-01T00:00:00Z", client.CreatedAt);
    }

    [Fact]
    public void Nimblesite.Sync.CoreClient_RecordEquality()
    {
        var client1 = new Nimblesite.Sync.CoreClient("c1", 100, "2024-01-01T00:00:00Z", "2024-01-01T00:00:00Z");
        var client2 = new Nimblesite.Sync.CoreClient("c1", 100, "2024-01-01T00:00:00Z", "2024-01-01T00:00:00Z");
        var client3 = new Nimblesite.Sync.CoreClient("c2", 100, "2024-01-01T00:00:00Z", "2024-01-01T00:00:00Z");

        Assert.Equal(client1, client2);
        Assert.NotEqual(client1, client3);
    }

    [Fact]
    public void Nimblesite.Sync.CoreClient_With_CreatesModifiedCopy()
    {
        var client = new Nimblesite.Sync.CoreClient("c1", 100, "2024-01-01T00:00:00Z", "2024-01-01T00:00:00Z");
        var updated = client with { LastSyncVersion = 200 };

        Assert.Equal("c1", updated.OriginId);
        Assert.Equal(200, updated.LastSyncVersion);
        Assert.Equal("2024-01-01T00:00:00Z", updated.CreatedAt);
    }

    [Fact]
    public void Nimblesite.Sync.CoreLogEntry_CreatesCorrectRecord()
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

        Assert.Equal(1, entry.Version);
        Assert.Equal("Person", entry.TableName);
        Assert.Equal("{\"Id\":\"p1\"}", entry.PkValue);
        Assert.Equal(Nimblesite.Sync.CoreOperation.Insert, entry.Operation);
        Assert.Equal("{\"Id\":\"p1\",\"Name\":\"Alice\"}", entry.Payload);
        Assert.Equal("origin-1", entry.Origin);
        Assert.Equal("2024-01-01T00:00:00Z", entry.Timestamp);
    }

    [Fact]
    public void Nimblesite.Sync.CoreLogEntry_DeleteHasNullPayload()
    {
        var entry = new Nimblesite.Sync.CoreLogEntry(
            1,
            "Person",
            "{\"Id\":\"p1\"}",
            Nimblesite.Sync.CoreOperation.Delete,
            null,
            "origin-1",
            "2024-01-01T00:00:00Z"
        );

        Assert.Null(entry.Payload);
        Assert.Equal(Nimblesite.Sync.CoreOperation.Delete, entry.Operation);
    }

    [Fact]
    public void Nimblesite.Sync.CoreOperation_HasCorrectValues()
    {
        Assert.Equal(Nimblesite.Sync.CoreOperation.Insert, (Nimblesite.Sync.CoreOperation)0);
        Assert.Equal(Nimblesite.Sync.CoreOperation.Update, (Nimblesite.Sync.CoreOperation)1);
        Assert.Equal(Nimblesite.Sync.CoreOperation.Delete, (Nimblesite.Sync.CoreOperation)2);
    }

    [Fact]
    public void SubscriptionType_HasCorrectValues()
    {
        Assert.Equal(SubscriptionType.Record, (SubscriptionType)0);
        Assert.Equal(SubscriptionType.Table, (SubscriptionType)1);
        Assert.Equal(SubscriptionType.Query, (SubscriptionType)2);
    }

    [Fact]
    public void Nimblesite.Sync.CoreSubscription_CreatesCorrectRecord()
    {
        var sub = new Nimblesite.Sync.CoreSubscription(
            "sub-1",
            "origin-1",
            SubscriptionType.Table,
            "Person",
            null,
            "2024-01-01T00:00:00Z",
            "2024-12-31T00:00:00Z"
        );

        Assert.Equal("sub-1", sub.SubscriptionId);
        Assert.Equal("origin-1", sub.OriginId);
        Assert.Equal(SubscriptionType.Table, sub.Type);
        Assert.Equal("Person", sub.TableName);
        Assert.Null(sub.Filter);
        Assert.Equal("2024-01-01T00:00:00Z", sub.CreatedAt);
        Assert.Equal("2024-12-31T00:00:00Z", sub.ExpiresAt);
    }

    [Fact]
    public void ChangeNotification_CreatesCorrectRecord()
    {
        var entry = new Nimblesite.Sync.CoreLogEntry(
            1,
            "Person",
            "{\"Id\":\"p1\"}",
            Nimblesite.Sync.CoreOperation.Insert,
            "{}",
            "origin",
            "2024-01-01T00:00:00Z"
        );

        var notification = new ChangeNotification("sub-1", entry);

        Assert.Equal("sub-1", notification.SubscriptionId);
        Assert.Equal(entry, notification.Change);
    }

    [Fact]
    public void PullResult_CreatesCorrectRecord()
    {
        var result = new PullResult(10, 0, 100);

        Assert.Equal(10, result.ChangesApplied);
        Assert.Equal(0, result.FromVersion);
        Assert.Equal(100, result.ToVersion);
    }

    [Fact]
    public void PushResult_CreatesCorrectRecord()
    {
        var result = new PushResult(5, 0, 50);

        Assert.Equal(5, result.ChangesPushed);
        Assert.Equal(0, result.FromVersion);
        Assert.Equal(50, result.ToVersion);
    }

    [Fact]
    public void Nimblesite.Sync.CoreResult_CreatesCorrectRecord()
    {
        var pull = new PullResult(10, 0, 100);
        var push = new PushResult(5, 0, 50);
        var result = new Nimblesite.Sync.CoreResult(pull, push);

        Assert.Equal(pull, result.Pull);
        Assert.Equal(push, result.Push);
    }

    [Fact]
    public void BatchConfig_CreatesCorrectRecord()
    {
        var config = new BatchConfig(100, 3);

        Assert.Equal(100, config.BatchSize);
        Assert.Equal(3, config.MaxRetryPasses);
    }

    [Fact]
    public void BatchConfig_DefaultMaxRetry()
    {
        var config = new BatchConfig(50);

        Assert.Equal(50, config.BatchSize);
        Assert.Equal(3, config.MaxRetryPasses); // Default
    }

    [Fact]
    public void BatchApplyResult_CreatesCorrectRecord()
    {
        var result = new BatchApplyResult(10, 2, 100L);

        Assert.Equal(10, result.AppliedCount);
        Assert.Equal(2, result.DeferredCount);
        Assert.Equal(100L, result.ToVersion);
    }

    [Fact]
    public void Nimblesite.Sync.CoreBatch_CreatesCorrectRecord()
    {
        var changes = new List<Nimblesite.Sync.CoreLogEntry>
        {
            new(1, "T", "PK", Nimblesite.Sync.CoreOperation.Insert, "{}", "O", "TS"),
        };
        var batch = new Nimblesite.Sync.CoreBatch(changes, 0, 1, false);

        Assert.Single(batch.Changes);
        Assert.Equal(0, batch.FromVersion);
        Assert.Equal(1, batch.ToVersion);
        Assert.False(batch.HasMore);
    }

    [Fact]
    public void Nimblesite.Sync.CoreBatch_HasMore_True()
    {
        var changes = new List<Nimblesite.Sync.CoreLogEntry>
        {
            new(1, "T", "PK", Nimblesite.Sync.CoreOperation.Insert, "{}", "O", "TS"),
        };
        var batch = new Nimblesite.Sync.CoreBatch(changes, 0, 1, true);

        Assert.True(batch.HasMore);
    }

    [Fact]
    public void Nimblesite.Sync.CoreBatch_WithHash()
    {
        var changes = new List<Nimblesite.Sync.CoreLogEntry>
        {
            new(1, "T", "PK", Nimblesite.Sync.CoreOperation.Insert, "{}", "O", "TS"),
        };
        var batch = new Nimblesite.Sync.CoreBatch(changes, 0, 1, false, "abc123hash");

        Assert.Equal("abc123hash", batch.Hash);
    }

    [Fact]
    public void ConflictStrategy_HasCorrectValues()
    {
        Assert.Equal(ConflictStrategy.LastWriteWins, (ConflictStrategy)0);
        Assert.Equal(ConflictStrategy.ServerWins, (ConflictStrategy)1);
        Assert.Equal(ConflictStrategy.ClientWins, (ConflictStrategy)2);
    }
}
