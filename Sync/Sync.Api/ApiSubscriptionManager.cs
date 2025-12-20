using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace Sync.Api;

/// <summary>
/// Manages real-time subscriptions for SSE.
/// Implements spec Section 10 (Real-Time Subscriptions).
/// Production hardened with bounded channels and cleanup.
/// </summary>
public sealed class ApiSubscriptionManager : IDisposable
{
    private readonly ConcurrentDictionary<string, Subscription> _subscriptions = new();
    private readonly ILogger<ApiSubscriptionManager> _logger;
#pragma warning disable IDE0052 // Timer keeps cleanup alive
    private readonly Timer _cleanupTimer;
#pragma warning restore IDE0052

    /// <summary>
    /// Creates a new subscription manager with auto-cleanup.
    /// </summary>
    public ApiSubscriptionManager(ILogger<ApiSubscriptionManager> logger)
    {
        _logger = logger;
        _cleanupTimer = new Timer(
            CleanupStaleSubscriptions,
            null,
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(5)
        );
    }

    /// <summary>
    /// Disposes resources including the cleanup timer.
    /// </summary>
    public void Dispose() => _cleanupTimer.Dispose();

    /// <summary>
    /// Subscribe to changes for a table (and optionally specific record).
    /// Uses BOUNDED channel to prevent memory exhaustion.
    /// </summary>
    public Channel<SyncLogEntry> Subscribe(string subscriptionId, string tableName, string? pkValue)
    {
        _logger.LogInformation(
            "SUBS: Creating subscription {Id} for {Table}/{Pk}",
            subscriptionId,
            tableName,
            pkValue
        );

        var channel = Channel.CreateBounded<SyncLogEntry>(
            new BoundedChannelOptions(1000) { FullMode = BoundedChannelFullMode.DropOldest }
        );
        var sub = new Subscription(subscriptionId, tableName, pkValue, channel, DateTime.UtcNow);
        _subscriptions[subscriptionId] = sub;

        return channel;
    }

    /// <summary>
    /// Unsubscribe from changes.
    /// </summary>
    public void Unsubscribe(string subscriptionId)
    {
        if (_subscriptions.TryRemove(subscriptionId, out var sub))
        {
            _logger.LogInformation("SUBS: Removed subscription {Id}", subscriptionId);
            sub.Channel.Writer.Complete();
        }
    }

    /// <summary>
    /// Notify all matching subscriptions of a change.
    /// </summary>
    public void NotifyChange(SyncLogEntryDto entry)
    {
        var syncEntry = new SyncLogEntry(
            entry.Version,
            entry.TableName,
            entry.PkValue,
            Enum.Parse<SyncOperation>(entry.Operation, true),
            entry.Payload,
            entry.Origin,
            entry.Timestamp
        );

        foreach (var sub in _subscriptions.Values)
        {
            if (sub.Matches(syncEntry))
            {
                _logger.LogDebug(
                    "SUBS: Notifying {Id} of change to {Table}/{Pk}",
                    sub.Id,
                    entry.TableName,
                    entry.PkValue
                );
                _ = sub.Channel.Writer.TryWrite(syncEntry);
            }
        }
    }

    private void CleanupStaleSubscriptions(object? state)
    {
        var staleThreshold = DateTime.UtcNow.AddHours(-1);
        var staleIds = _subscriptions
            .Where(kvp => kvp.Value.CreatedAt < staleThreshold)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var id in staleIds)
        {
            _logger.LogInformation("SUBS: Cleaning up stale subscription {Id}", id);
            Unsubscribe(id);
        }
    }

    private sealed record Subscription(
        string Id,
        string TableName,
        string? PkValue,
        Channel<SyncLogEntry> Channel,
        DateTime CreatedAt
    )
    {
        public bool Matches(SyncLogEntry entry)
        {
            if (!string.Equals(TableName, entry.TableName, StringComparison.OrdinalIgnoreCase))
                return false;

            if (PkValue is null)
                return true;

            return entry.PkValue.Contains(PkValue, StringComparison.Ordinal);
        }
    }
}
