using System.Collections.Concurrent;
using System.Threading.Channels;
using Sync;

namespace Sync.Api;

/// <summary>
/// Manages real-time subscriptions for sync changes.
/// Implements spec Section 10 (Real-Time Subscriptions) over SSE.
/// </summary>
public sealed class SubscriptionService
{
    private readonly ConcurrentDictionary<string, SubscriptionInfo> _subscriptions = new();

    /// <summary>
    /// Subscribe to changes for a table and/or record.
    /// </summary>
    /// <param name="subscriptionId">Unique subscription ID.</param>
    /// <param name="tableName">Optional table name filter.</param>
    /// <param name="recordId">Optional record ID filter (PK value).</param>
    /// <returns>Channel to read changes from.</returns>
    public Channel<SyncLogEntry> Subscribe(string subscriptionId, string? tableName, string? recordId)
    {
        var channel = Channel.CreateUnbounded<SyncLogEntry>();
        var info = new SubscriptionInfo(channel, tableName, recordId);
        _subscriptions.TryAdd(subscriptionId, info);
        return channel;
    }

    /// <summary>
    /// Unsubscribe and clean up resources.
    /// </summary>
    /// <param name="subscriptionId">Subscription to remove.</param>
    public void Unsubscribe(string subscriptionId)
    {
        if (_subscriptions.TryRemove(subscriptionId, out var info))
        {
            info.Channel.Writer.Complete();
        }
    }

    /// <summary>
    /// Notify all matching subscribers of a change.
    /// </summary>
    /// <param name="entry">The sync log entry to broadcast.</param>
    public async Task NotifyChange(SyncLogEntry entry)
    {
        foreach (var kvp in _subscriptions)
        {
            var info = kvp.Value;
            
            // Check table filter
            if (info.TableName != null && info.TableName != entry.TableName)
            {
                continue;
            }

            // Check record filter
            if (info.RecordId != null && !entry.PkValue.Contains(info.RecordId))
            {
                continue;
            }

            // Send to matching subscriber
            await info.Channel.Writer.WriteAsync(entry);
        }
    }

    /// <summary>
    /// Get count of active subscriptions (for monitoring).
    /// </summary>
    public int ActiveSubscriptionCount => _subscriptions.Count;

    private sealed record SubscriptionInfo(
        Channel<SyncLogEntry> Channel,
        string? TableName,
        string? RecordId
    );
}
