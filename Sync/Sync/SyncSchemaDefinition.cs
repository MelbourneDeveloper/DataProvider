using Migration;
using P = Migration.PortableTypes;

namespace Sync;

/// <summary>
/// Database-agnostic sync schema definition using the Migration framework.
/// Implements spec Appendix A schema for all supported databases.
/// </summary>
public static class SyncSchemaDefinition
{
    /// <summary>
    /// Gets the complete sync schema definition.
    /// This is database-independent and can be applied to SQLite, PostgreSQL, or SQL Server.
    /// </summary>
    public static SchemaDefinition Definition { get; } = BuildSchema();

    /// <summary>
    /// Gets the initial sync state values to insert after schema creation.
    /// </summary>
    public static IReadOnlyList<(string Key, string Value)> InitialStateValues { get; } =
        [("origin_id", ""), ("last_server_version", "0"), ("last_push_version", "0")];

    private static SchemaDefinition BuildSchema() =>
        Schema
            .Define("_sync")
            .Table(
                "_sync_state",
                t =>
                    t.Column("key", P.Text, c => c.PrimaryKey())
                        .Column("value", P.Text, c => c.NotNull())
                        .Comment("Sync state (persistent). Stores origin ID and version tracking.")
            )
            .Table(
                "_sync_session",
                t =>
                    t.Column("sync_active", P.Int, c => c.NotNull().Default("0"))
                        .Comment(
                            "Sync session (ephemeral flag). Single row with sync_active for trigger suppression."
                        )
            )
            .Table(
                "_sync_log",
                t =>
                    t.Column("version", P.BigInt, c => c.PrimaryKey().Identity())
                        .Column("table_name", P.Text, c => c.NotNull())
                        .Column("pk_value", P.Text, c => c.NotNull())
                        .Column(
                            "operation",
                            P.Text,
                            c => c.NotNull().Check("operation IN ('insert', 'update', 'delete')")
                        )
                        .Column("payload", P.Text)
                        .Column("origin", P.Text, c => c.NotNull())
                        .Column("timestamp", P.Text, c => c.NotNull())
                        .Index("idx_sync_log_version", "version")
                        .Index("idx_sync_log_table", ["table_name", "version"])
                        .Comment("Unified change log with JSON payloads.")
            )
            .Table(
                "_sync_clients",
                t =>
                    t.Column("origin_id", P.Text, c => c.PrimaryKey())
                        .Column("last_sync_version", P.BigInt, c => c.NotNull().Default("0"))
                        .Column("last_sync_timestamp", P.Text, c => c.NotNull())
                        .Column("created_at", P.Text, c => c.NotNull())
                        .Index("idx_sync_clients_version", "last_sync_version")
                        .Comment(
                            "Server-side tracking of client sync state for tombstone retention. Maps to spec Section 13.3 / Appendix B."
                        )
            )
            .Table(
                "_sync_subscriptions",
                t =>
                    t.Column("subscription_id", P.Text, c => c.PrimaryKey())
                        .Column("origin_id", P.Text, c => c.NotNull())
                        .Column(
                            "subscription_type",
                            P.Text,
                            c =>
                                c.NotNull()
                                    .Check("subscription_type IN ('record', 'table', 'query')")
                        )
                        .Column("table_name", P.Text, c => c.NotNull())
                        .Column("filter", P.Text)
                        .Column("created_at", P.Text, c => c.NotNull())
                        .Column("expires_at", P.Text)
                        .Index("idx_subscriptions_table", "table_name")
                        .Index("idx_subscriptions_origin", "origin_id")
                        .Comment(
                            "Server-side tracking of real-time subscriptions. Maps to spec Section 10.6."
                        )
            )
            .Table(
                "_sync_mapping_state",
                t =>
                    t.Column("mapping_id", P.Text, c => c.PrimaryKey())
                        .Column("last_synced_version", P.BigInt, c => c.NotNull().Default("0"))
                        .Column("last_sync_timestamp", P.Text, c => c.NotNull())
                        .Column("records_synced", P.BigInt, c => c.NotNull().Default("0"))
                        .Comment(
                            "Per-mapping sync state for version-based tracking. Maps to spec Section 7.5.2."
                        )
            )
            .Table(
                "_sync_record_hashes",
                t =>
                    t.Column("mapping_id", P.Text, c => c.NotNull())
                        .Column("source_pk", P.Text, c => c.NotNull())
                        .Column("payload_hash", P.Text, c => c.NotNull())
                        .Column("synced_at", P.Text, c => c.NotNull())
                        .CompositePrimaryKey("mapping_id", "source_pk")
                        .Index("idx_record_hashes_mapping", "mapping_id")
                        .Comment(
                            "Per-record hash tracking for hash-based sync strategy. Maps to spec Section 7.5.2."
                        )
            )
            .Build();
}
