namespace Sync;

/// <summary>
/// Direction of sync mapping. Spec Section 7.6.
/// </summary>
public enum MappingDirection
{
    /// <summary>Push changes from source to target.</summary>
    Push,

    /// <summary>Pull changes from target to source.</summary>
    Pull,

    /// <summary>Bidirectional sync (requires separate push/pull mappings).</summary>
    Both,
}

/// <summary>
/// Behavior for tables not explicitly mapped. Spec Section 7.7.
/// </summary>
public enum UnmappedTableBehavior
{
    /// <summary>Unmapped tables are NOT synced (explicit opt-in).</summary>
    Strict,

    /// <summary>Unmapped tables sync with identity mapping (same schema).</summary>
    Passthrough,
}

/// <summary>
/// Sync tracking strategy. Spec Section 7.5.1.
/// </summary>
public enum SyncTrackingStrategy
{
    /// <summary>Track last synced _sync_log version per mapping.</summary>
    Version,

    /// <summary>Store hash of synced payload, resync on change.</summary>
    Hash,

    /// <summary>Track last sync timestamp per record.</summary>
    Timestamp,

    /// <summary>Application manages tracking externally.</summary>
    External,
}

/// <summary>
/// Column value transform type. Spec Section 7.3.
/// </summary>
public enum TransformType
{
    /// <summary>No transformation, direct copy.</summary>
    None,

    /// <summary>Constant value (not from source).</summary>
    Constant,

    /// <summary>LQL expression transform.</summary>
    Lql,
}

/// <summary>
/// Primary key column mapping between source and target.
/// </summary>
/// <param name="SourceColumn">Column name in source table.</param>
/// <param name="TargetColumn">Column name in target table.</param>
public sealed record PkMapping(string SourceColumn, string TargetColumn);

/// <summary>
/// Column mapping between source and target with optional transform.
/// Spec Section 7.3.
/// </summary>
/// <param name="Source">Source column name. NULL for computed/constant columns.</param>
/// <param name="Target">Target column name.</param>
/// <param name="Transform">Transform type to apply.</param>
/// <param name="Value">Constant value when Transform=Constant.</param>
/// <param name="Lql">LQL expression when Transform=Lql.</param>
public sealed record ColumnMapping(
    string? Source,
    string Target,
    TransformType Transform = TransformType.None,
    string? Value = null,
    string? Lql = null
);

/// <summary>
/// Filter expression to select which records to sync.
/// </summary>
/// <param name="Lql">LQL boolean expression, e.g. "IsActive = true AND DeletedAt IS NULL".</param>
public sealed record SyncFilter(string Lql);

/// <summary>
/// Sync tracking configuration for a mapping.
/// </summary>
/// <param name="Enabled">Whether sync tracking is enabled.</param>
/// <param name="Strategy">Tracking strategy to use.</param>
/// <param name="TrackingColumn">Column name for timestamp strategy.</param>
public sealed record SyncTrackingConfig(
    bool Enabled = true,
    SyncTrackingStrategy Strategy = SyncTrackingStrategy.Version,
    string? TrackingColumn = null
);

/// <summary>
/// Target table configuration for multi-target mappings.
/// </summary>
/// <param name="Table">Target table name.</param>
/// <param name="ColumnMappings">Column mappings for this target.</param>
public sealed record TargetConfig(string Table, IReadOnlyList<ColumnMapping> ColumnMappings);

/// <summary>
/// Complete table mapping configuration. Spec Section 7.3.
/// </summary>
/// <param name="Id">Unique mapping identifier.</param>
/// <param name="SourceTable">Source table name.</param>
/// <param name="TargetTable">Target table name (single target). NULL for multi-target.</param>
/// <param name="Direction">Sync direction.</param>
/// <param name="Enabled">Whether mapping is active.</param>
/// <param name="PkMapping">Primary key column mapping.</param>
/// <param name="ColumnMappings">Column mappings for single target.</param>
/// <param name="ExcludedColumns">Columns to exclude from sync.</param>
/// <param name="Filter">Optional filter expression.</param>
/// <param name="SyncTracking">Sync tracking configuration.</param>
/// <param name="IsMultiTarget">True if mapping to multiple tables.</param>
/// <param name="Targets">Target configurations for multi-target mapping.</param>
public sealed record TableMapping(
    string Id,
    string SourceTable,
    string? TargetTable,
    MappingDirection Direction,
    bool Enabled,
    PkMapping? PkMapping,
    IReadOnlyList<ColumnMapping> ColumnMappings,
    IReadOnlyList<string> ExcludedColumns,
    SyncFilter? Filter,
    SyncTrackingConfig SyncTracking,
    bool IsMultiTarget = false,
    IReadOnlyList<TargetConfig>? Targets = null
)
{
    /// <summary>
    /// Creates a simple identity mapping (same table/column names).
    /// </summary>
    /// <param name="tableName">Table name.</param>
    /// <param name="direction">Sync direction.</param>
    /// <returns>Identity mapping.</returns>
    public static TableMapping Identity(string tableName, MappingDirection direction) =>
        new(
            Id: $"identity-{tableName}",
            SourceTable: tableName,
            TargetTable: tableName,
            Direction: direction,
            Enabled: true,
            PkMapping: null,
            ColumnMappings: [],
            ExcludedColumns: [],
            Filter: null,
            SyncTracking: new SyncTrackingConfig()
        );
}

/// <summary>
/// Root sync mapping configuration. Spec Section 7.3.
/// </summary>
/// <param name="Version">Configuration format version.</param>
/// <param name="UnmappedTableBehavior">Behavior for unmapped tables.</param>
/// <param name="Mappings">List of table mappings.</param>
public sealed record SyncMappingConfig(
    string Version,
    UnmappedTableBehavior UnmappedTableBehavior,
    IReadOnlyList<TableMapping> Mappings
)
{
    /// <summary>
    /// Empty configuration with strict unmapped table behavior.
    /// </summary>
    public static SyncMappingConfig Empty => new("1.0", UnmappedTableBehavior.Strict, []);

    /// <summary>
    /// Configuration that passes through all tables unchanged.
    /// </summary>
    public static SyncMappingConfig Passthrough =>
        new("1.0", UnmappedTableBehavior.Passthrough, []);
}
