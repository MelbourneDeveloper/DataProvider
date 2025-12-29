namespace Migration;

/// <summary>
/// Platform-independent default value expression.
/// These get translated to the correct SQL for each database platform.
/// </summary>
public abstract record PortableDefault
{
    /// <summary>
    /// Prevents external inheritance - this makes the type hierarchy "closed".
    /// </summary>
    private protected PortableDefault() { }
}

// ═══════════════════════════════════════════════════════════════════
// LITERAL DEFAULTS - Exact values
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// A literal SQL expression to use as-is (no translation).
/// Use this for platform-specific expressions when needed.
/// </summary>
/// <param name="Expression">The raw SQL expression</param>
public sealed record LiteralDefault(string Expression) : PortableDefault;

/// <summary>
/// A string literal value, properly quoted.
/// </summary>
/// <param name="Value">The string value</param>
public sealed record StringDefault(string Value) : PortableDefault;

/// <summary>
/// An integer literal value.
/// </summary>
/// <param name="Value">The integer value</param>
public sealed record IntDefault(long Value) : PortableDefault;

/// <summary>
/// A decimal/floating point literal value.
/// </summary>
/// <param name="Value">The decimal value</param>
public sealed record DecimalDefault(decimal Value) : PortableDefault;

/// <summary>
/// A boolean literal value.
/// Maps to true/false on Postgres, 1/0 on SQLite.
/// </summary>
/// <param name="Value">The boolean value</param>
public sealed record BoolDefault(bool Value) : PortableDefault;

// ═══════════════════════════════════════════════════════════════════
// FUNCTION DEFAULTS - Database functions
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// Generate a new random UUID.
/// Maps to gen_random_uuid() on Postgres, a UUID-generating expression on SQLite.
/// </summary>
public sealed record NewUuidDefault : PortableDefault;

/// <summary>
/// Current timestamp (without timezone).
/// Maps to CURRENT_TIMESTAMP on both platforms.
/// </summary>
public sealed record CurrentTimestampDefault : PortableDefault;

/// <summary>
/// Current timestamp with timezone.
/// Maps to CURRENT_TIMESTAMP on Postgres (TIMESTAMPTZ), CURRENT_TIMESTAMP on SQLite (stored as text).
/// </summary>
public sealed record CurrentTimestampTzDefault : PortableDefault;

/// <summary>
/// Current date (no time component).
/// Maps to CURRENT_DATE on both platforms.
/// </summary>
public sealed record CurrentDateDefault : PortableDefault;

/// <summary>
/// Current time (no date component).
/// Maps to CURRENT_TIME on both platforms.
/// </summary>
public sealed record CurrentTimeDefault : PortableDefault;

/// <summary>
/// Null as default (for nullable columns).
/// Explicit NULL default.
/// </summary>
public sealed record NullDefault : PortableDefault;

// ═══════════════════════════════════════════════════════════════════
// SEQUENCE DEFAULTS - For auto-generated values
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// Next value from a named sequence.
/// Maps to nextval('sequence_name') on Postgres.
/// SQLite: Not supported, use AUTOINCREMENT instead.
/// </summary>
/// <param name="SequenceName">Name of the sequence</param>
public sealed record NextSequenceDefault(string SequenceName) : PortableDefault;

/// <summary>
/// Factory methods for portable defaults.
/// </summary>
public static class PortableDefaults
{
    /// <summary>Literal SQL expression (no translation).</summary>
    public static LiteralDefault Literal(string expression) => new(expression);

    /// <summary>String literal, properly quoted.</summary>
    public static StringDefault String(string value) => new(value);

    /// <summary>Integer literal.</summary>
    public static IntDefault Int(long value) => new(value);

    /// <summary>Decimal literal.</summary>
    public static DecimalDefault Decimal(decimal value) => new(value);

    /// <summary>Boolean literal (true/false on Postgres, 1/0 on SQLite).</summary>
    public static BoolDefault Bool(bool value) => new(value);

    /// <summary>Boolean true.</summary>
    public static BoolDefault True => new(true);

    /// <summary>Boolean false.</summary>
    public static BoolDefault False => new(false);

    /// <summary>Generate a new random UUID.</summary>
    public static NewUuidDefault NewUuid => new();

    /// <summary>Current timestamp (without timezone).</summary>
    public static CurrentTimestampDefault CurrentTimestamp => new();

    /// <summary>Current timestamp with timezone.</summary>
    public static CurrentTimestampTzDefault CurrentTimestampTz => new();

    /// <summary>Current date only.</summary>
    public static CurrentDateDefault CurrentDate => new();

    /// <summary>Current time only.</summary>
    public static CurrentTimeDefault CurrentTime => new();

    /// <summary>Explicit NULL default.</summary>
    public static NullDefault Null => new();

    /// <summary>Next value from a sequence.</summary>
    public static NextSequenceDefault NextSequence(string sequenceName) => new(sequenceName);
}
