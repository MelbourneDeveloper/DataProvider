using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Sync.Api;

/// <summary>
/// Manages real-time subscriptions for Server-Sent Events.
/// Implements spec Section 10 (Real-Time Subscriptions).
/// </summary>
public sealed class SubscriptionHub
{
    private readonly ConcurrentDictionary<string, Subscription> _subscriptions = new();

    /// <summary>
    /// Subscribe to changes for a table or specific record.
    /// </summary>
    /// <param name="subscriptionId">Unique subscription ID.</param>
    /// <param name="tableName">Table to subscribe to.</param>
    /// <param name="pkValue">Optional PK value for record-level subscription.</param>
    /// <returns>Channel to read changes from.</returns>
    public Channel<SyncLogEntry> Subscribe(string subscriptionId, string tableName, string? pkValue)
    {
        var channel = Channel.CreateUnbounded<SyncLogEntry>();
        var subscription = new Subscription(tableName, pkValue, channel);
        _subscriptions[subscriptionId] = subscription;
        return channel;
    }

    /// <summary>
    /// Unsubscribe and close the channel.
    /// </summary>
    /// <param name="subscriptionId">Subscription to remove.</param>
    public void Unsubscribe(string subscriptionId)
    {
        if (_subscriptions.TryRemove(subscriptionId, out var subscription))
        {
            subscription.Channel.Writer.Complete();
        }
    }

    /// <summary>
    /// Notify all matching subscribers of a change.
    /// </summary>
    /// <param name="entry">Change entry to broadcast.</param>
    public async Task NotifyChange(SyncLogEntry entry)
    {
        foreach (var kvp in _subscriptions)
        {
            var sub = kvp.Value;

            // Table-level subscription matches any change in the table
            if (sub.PkValue is null && sub.TableName.Equals(entry.TableName, StringComparison.OrdinalIgnoreCase))
            {
                await sub.Channel.Writer.WriteAsync(entry);
            }
            // Record-level subscription matches specific PK
            else if (
                sub.PkValue is not null
                && sub.TableName.Equals(entry.TableName, StringComparison.OrdinalIgnoreCase)
                && entry.PkValue.Contains(sub.PkValue, StringComparison.OrdinalIgnoreCase)
            )
            {
                await sub.Channel.Writer.WriteAsync(entry);
            }
        }
    }

    /// <summary>
    /// Get count of active subscriptions.
    /// </summary>
    public int SubscriptionCount => _subscriptions.Count;

    private sealed record Subscription(string TableName, string? PkValue, Channel<SyncLogEntry> Channel);
}
