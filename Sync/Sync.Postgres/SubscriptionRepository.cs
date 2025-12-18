using Npgsql;

namespace Sync.Postgres;

/// <summary>
/// Static methods for subscription operations in PostgreSQL.
/// Implements spec Section 10 (Real-Time Subscriptions).
/// </summary>
public static class SubscriptionRepository
{
    /// <summary>
    /// Creates a new subscription.
    /// </summary>
    /// <param name="connection">Postgres connection.</param>
    /// <param name="subscription">Subscription to create.</param>
    /// <returns>Success or database error.</returns>
    public static BoolSyncResult CreateSubscription(
        NpgsqlConnection connection,
        SyncSubscription subscription
    )
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                INSERT INTO _sync_subscriptions 
                    (subscription_id, origin_id, subscription_type, table_name, filter, created_at, expires_at)
                VALUES (@id, @originId, @type, @tableName, @filter, @createdAt, @expiresAt)
                """;
            cmd.Parameters.AddWithValue("@id", subscription.SubscriptionId);
            cmd.Parameters.AddWithValue("@originId", subscription.OriginId);
            cmd.Parameters.AddWithValue("@type", subscription.Type.ToString().ToLowerInvariant());
            cmd.Parameters.AddWithValue("@tableName", subscription.TableName);
            cmd.Parameters.AddWithValue("@filter", (object?)subscription.Filter ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@createdAt", subscription.CreatedAt);
            cmd.Parameters.AddWithValue("@expiresAt", (object?)subscription.ExpiresAt ?? DBNull.Value);
            cmd.ExecuteNonQuery();

            return new BoolSyncOk(true);
        }
        catch (NpgsqlException ex)
        {
            return new BoolSyncError(
                new SyncErrorDatabase($"Failed to create subscription: {ex.Message}")
            );
        }
    }

    /// <summary>
    /// Gets a subscription by ID.
    /// </summary>
    /// <param name="connection">Postgres connection.</param>
    /// <param name="subscriptionId">Subscription ID.</param>
    /// <returns>Subscription or null if not found.</returns>
    public static SubscriptionResult GetSubscription(
        NpgsqlConnection connection,
        string subscriptionId
    )
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                SELECT subscription_id, origin_id, subscription_type, table_name, filter, created_at, expires_at
                FROM _sync_subscriptions
                WHERE subscription_id = @id
                """;
            cmd.Parameters.AddWithValue("@id", subscriptionId);

            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
            {
                return new SubscriptionOk(null);
            }

            return new SubscriptionOk(
                new SyncSubscription(
                    SubscriptionId: reader.GetString(0),
                    OriginId: reader.GetString(1),
                    Type: ParseSubscriptionType(reader.GetString(2)),
                    TableName: reader.GetString(3),
                    Filter: reader.IsDBNull(4) ? null : reader.GetString(4),
                    CreatedAt: reader.GetString(5),
                    ExpiresAt: reader.IsDBNull(6) ? null : reader.GetString(6)
                )
            );
        }
        catch (NpgsqlException ex)
        {
            return new SubscriptionError(
                new SyncErrorDatabase($"Failed to get subscription: {ex.Message}")
            );
        }
    }

    /// <summary>
    /// Gets all subscriptions for a table.
    /// </summary>
    /// <param name="connection">Postgres connection.</param>
    /// <param name="tableName">Table name.</param>
    /// <returns>List of subscriptions.</returns>
    public static SubscriptionListResult GetSubscriptionsForTable(
        NpgsqlConnection connection,
        string tableName
    )
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                SELECT subscription_id, origin_id, subscription_type, table_name, filter, created_at, expires_at
                FROM _sync_subscriptions
                WHERE table_name = @tableName
                """;
            cmd.Parameters.AddWithValue("@tableName", tableName);

            var subscriptions = new List<SyncSubscription>();
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                subscriptions.Add(
                    new SyncSubscription(
                        SubscriptionId: reader.GetString(0),
                        OriginId: reader.GetString(1),
                        Type: ParseSubscriptionType(reader.GetString(2)),
                        TableName: reader.GetString(3),
                        Filter: reader.IsDBNull(4) ? null : reader.GetString(4),
                        CreatedAt: reader.GetString(5),
                        ExpiresAt: reader.IsDBNull(6) ? null : reader.GetString(6)
                    )
                );
            }

            return new SubscriptionListOk(subscriptions);
        }
        catch (NpgsqlException ex)
        {
            return new SubscriptionListError(
                new SyncErrorDatabase($"Failed to get subscriptions: {ex.Message}")
            );
        }
    }

    /// <summary>
    /// Deletes a subscription.
    /// </summary>
    /// <param name="connection">Postgres connection.</param>
    /// <param name="subscriptionId">Subscription to delete.</param>
    /// <returns>Success or database error.</returns>
    public static BoolSyncResult DeleteSubscription(
        NpgsqlConnection connection,
        string subscriptionId
    )
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM _sync_subscriptions WHERE subscription_id = @id";
            cmd.Parameters.AddWithValue("@id", subscriptionId);
            cmd.ExecuteNonQuery();

            return new BoolSyncOk(true);
        }
        catch (NpgsqlException ex)
        {
            return new BoolSyncError(
                new SyncErrorDatabase($"Failed to delete subscription: {ex.Message}")
            );
        }
    }

    /// <summary>
    /// Deletes expired subscriptions.
    /// </summary>
    /// <param name="connection">Postgres connection.</param>
    /// <param name="currentTime">Current UTC timestamp.</param>
    /// <returns>Number of deleted subscriptions.</returns>
    public static IntSyncResult DeleteExpiredSubscriptions(
        NpgsqlConnection connection,
        string currentTime
    )
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                DELETE FROM _sync_subscriptions 
                WHERE expires_at IS NOT NULL AND expires_at < @currentTime
                """;
            cmd.Parameters.AddWithValue("@currentTime", currentTime);
            var deleted = cmd.ExecuteNonQuery();

            return new IntSyncOk(deleted);
        }
        catch (NpgsqlException ex)
        {
            return new IntSyncError(
                new SyncErrorDatabase($"Failed to delete expired subscriptions: {ex.Message}")
            );
        }
    }

    private static SubscriptionType ParseSubscriptionType(string type) =>
        type.ToLowerInvariant() switch
        {
            "record" => SubscriptionType.Record,
            "table" => SubscriptionType.Table,
            "query" => SubscriptionType.Query,
            _ => SubscriptionType.Table,
        };
}
