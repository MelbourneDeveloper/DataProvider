namespace Migration;

/// <summary>
/// Database-agnostic type definition. Base sealed type for discriminated union.
/// Pattern match on derived types to extract type-specific metadata.
/// </summary>
public abstract record PortableType;

// ═══════════════════════════════════════════════════════════════════
// INTEGER TYPES - No parameters needed (bit size is implicit in type)
// ═══════════════════════════════════════════════════════════════════

/// <summary>8-bit integer: 0 to 255 (unsigned) or -128 to 127 (signed).</summary>
public sealed record TinyIntType : PortableType;

/// <summary>16-bit signed integer: -32,768 to 32,767.</summary>
public sealed record SmallIntType : PortableType;

/// <summary>32-bit signed integer: -2,147,483,648 to 2,147,483,647.</summary>
public sealed record IntType : PortableType;

/// <summary>64-bit signed integer: -9.2E18 to 9.2E18.</summary>
public sealed record BigIntType : PortableType;

// ═══════════════════════════════════════════════════════════════════
// EXACT NUMERIC TYPES - Require precision and/or scale
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// Exact decimal number with specified precision and scale.
/// Precision = total number of digits (1-38).
/// Scale = digits after decimal point (0 to Precision).
/// Example: DECIMAL(10,2) stores values like 12345678.99
/// </summary>
/// <param name="Precision">Total digits (1-38)</param>
/// <param name="Scale">Decimal places (0 to Precision)</param>
public sealed record DecimalType(int Precision, int Scale) : PortableType;

/// <summary>
/// Currency type with fixed precision for financial calculations.
/// Equivalent to DECIMAL(19,4) - supports values up to ~922 trillion.
/// </summary>
public sealed record MoneyType : PortableType;

/// <summary>
/// Small currency type with reduced precision.
/// Equivalent to DECIMAL(10,4) - supports values up to ~214,748.
/// </summary>
public sealed record SmallMoneyType : PortableType;

// ═══════════════════════════════════════════════════════════════════
// FLOATING POINT TYPES - No parameters (IEEE standard sizes)
// ═══════════════════════════════════════════════════════════════════

/// <summary>32-bit IEEE 754 floating point. ~7 significant digits.</summary>
public sealed record FloatType : PortableType;

/// <summary>64-bit IEEE 754 floating point. ~15 significant digits.</summary>
public sealed record DoubleType : PortableType;

// ═══════════════════════════════════════════════════════════════════
// STRING TYPES - Each variant has exactly the parameters it needs
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// Fixed-length ASCII/single-byte character string. Always padded to exact length.
/// </summary>
/// <param name="Length">Exact character count (1-8000)</param>
public sealed record CharType(int Length) : PortableType;

/// <summary>
/// Variable-length ASCII/single-byte character string up to max length.
/// </summary>
/// <param name="MaxLength">Maximum characters (1-8000)</param>
public sealed record VarCharType(int MaxLength) : PortableType;

/// <summary>
/// Fixed-length Unicode character string. Always padded to exact length.
/// Uses 2 bytes per character (UCS-2/UTF-16).
/// </summary>
/// <param name="Length">Exact character count (1-4000)</param>
public sealed record NCharType(int Length) : PortableType;

/// <summary>
/// Variable-length Unicode character string up to max length.
/// Uses 2 bytes per character (UCS-2/UTF-16).
/// </summary>
/// <param name="MaxLength">Maximum characters (1-4000, or int.MaxValue for MAX)</param>
public sealed record NVarCharType(int MaxLength) : PortableType;

/// <summary>
/// Unlimited length text storage. No length parameter needed.
/// Maps to TEXT (Postgres/SQLite), NVARCHAR(MAX) (SQL Server).
/// </summary>
public sealed record TextType : PortableType;

// ═══════════════════════════════════════════════════════════════════
// BINARY TYPES - Length-based variants
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// Fixed-length binary data. Always padded to exact length.
/// </summary>
/// <param name="Length">Exact byte count (1-8000)</param>
public sealed record BinaryType(int Length) : PortableType;

/// <summary>
/// Variable-length binary data up to max length.
/// </summary>
/// <param name="MaxLength">Maximum bytes (1-8000, or int.MaxValue for MAX)</param>
public sealed record VarBinaryType(int MaxLength) : PortableType;

/// <summary>
/// Unlimited binary storage. No length parameter needed.
/// Maps to BLOB (SQLite), BYTEA (Postgres), VARBINARY(MAX) (SQL Server).
/// </summary>
public sealed record BlobType : PortableType;

// ═══════════════════════════════════════════════════════════════════
// DATE/TIME TYPES - Some need precision, some don't
// ═══════════════════════════════════════════════════════════════════

/// <summary>Date only (no time component). No parameters needed.</summary>
public sealed record DateType : PortableType;

/// <summary>
/// Time only (no date component) with fractional seconds precision.
/// </summary>
/// <param name="Precision">Fractional seconds digits (0-7, default 7)</param>
public sealed record TimeType(int Precision = 7) : PortableType;

/// <summary>
/// Date and time without timezone.
/// </summary>
/// <param name="Precision">Fractional seconds digits (0-7, default 3)</param>
public sealed record DateTimeType(int Precision = 3) : PortableType;

/// <summary>
/// Date and time with timezone offset. No precision parameter.
/// Always stores full precision with timezone info.
/// </summary>
public sealed record DateTimeOffsetType : PortableType;

/// <summary>
/// Row version / timestamp for optimistic concurrency.
/// Auto-generated binary value that changes on each update.
/// No parameters - platform determines size (8 bytes typically).
/// </summary>
public sealed record RowVersionType : PortableType;

// ═══════════════════════════════════════════════════════════════════
// OTHER TYPES - Specialized types with specific parameters
// ═══════════════════════════════════════════════════════════════════

/// <summary>128-bit globally unique identifier. No parameters needed.</summary>
public sealed record UuidType : PortableType;

/// <summary>Boolean true/false value. No parameters needed.</summary>
public sealed record BooleanType : PortableType;

/// <summary>
/// JSON document storage. No parameters needed.
/// Maps to JSONB (Postgres), TEXT (SQLite), NVARCHAR(MAX) (SQL Server).
/// </summary>
public sealed record JsonType : PortableType;

/// <summary>
/// XML document storage. No parameters needed.
/// Maps to XML (SQL Server/Postgres), TEXT (SQLite).
/// </summary>
public sealed record XmlType : PortableType;

/// <summary>
/// Database enumeration type with named values.
/// Postgres: Creates actual ENUM type.
/// SQL Server/SQLite: Maps to constrained string.
/// </summary>
/// <param name="Name">Enum type name for DDL</param>
/// <param name="Values">Allowed enum values in order</param>
public sealed record EnumType(string Name, IReadOnlyList<string> Values) : PortableType;

/// <summary>
/// Spatial geometry type for GIS data.
/// </summary>
/// <param name="Srid">Spatial Reference ID (e.g., 4326 for WGS84)</param>
public sealed record GeometryType(int? Srid) : PortableType;

/// <summary>
/// Spatial geography type for Earth-surface GIS data.
/// </summary>
/// <param name="Srid">Spatial Reference ID (default 4326 for WGS84)</param>
public sealed record GeographyType(int Srid = 4326) : PortableType;

/// <summary>
/// Factory methods for portable types.
/// </summary>
public static class PortableTypes
{
    /// <summary>8-bit integer.</summary>
    public static TinyIntType TinyInt => new();

    /// <summary>16-bit integer.</summary>
    public static SmallIntType SmallInt => new();

    /// <summary>32-bit integer.</summary>
    public static IntType Int32 => new();

    /// <summary>64-bit integer.</summary>
    public static BigIntType BigInt => new();

    /// <summary>Decimal with precision and scale.</summary>
    public static DecimalType DecimalNumber(int precision, int scale) => new(precision, scale);

    /// <summary>Currency type.</summary>
    public static MoneyType Money => new();

    /// <summary>32-bit float.</summary>
    public static FloatType Float32 => new();

    /// <summary>64-bit double.</summary>
    public static DoubleType Float64 => new();

    /// <summary>Fixed-length string.</summary>
    public static CharType FixedChar(int length) => new(length);

    /// <summary>Variable-length string.</summary>
    public static VarCharType VarChar(int maxLength) => new(maxLength);

    /// <summary>Fixed-length Unicode string.</summary>
    public static NCharType NChar(int length) => new(length);

    /// <summary>Variable-length Unicode string.</summary>
    public static NVarCharType NVarChar(int maxLength) => new(maxLength);

    /// <summary>Variable-length Unicode string with MAX length.</summary>
    public static NVarCharType NVarCharMax => new(int.MaxValue);

    /// <summary>Unlimited text.</summary>
    public static TextType Text => new();

    /// <summary>Fixed-length binary.</summary>
    public static BinaryType Binary(int length) => new(length);

    /// <summary>Variable-length binary.</summary>
    public static VarBinaryType VarBinary(int maxLength) => new(maxLength);

    /// <summary>Variable-length binary with MAX length.</summary>
    public static VarBinaryType VarBinaryMax => new(int.MaxValue);

    /// <summary>Unlimited binary.</summary>
    public static BlobType Blob => new();

    /// <summary>Date only.</summary>
    public static DateType Date => new();

    /// <summary>Time with precision.</summary>
    public static TimeType Time(int precision = 7) => new(precision);

    /// <summary>DateTime with precision.</summary>
    public static DateTimeType DateTime(int precision = 3) => new(precision);

    /// <summary>DateTime with timezone.</summary>
    public static DateTimeOffsetType DateTimeOffset => new();

    /// <summary>Row version for concurrency.</summary>
    public static RowVersionType RowVersion => new();

    /// <summary>UUID/GUID.</summary>
    public static UuidType Uuid => new();

    /// <summary>Boolean.</summary>
    public static BooleanType Boolean => new();

    /// <summary>JSON document.</summary>
    public static JsonType Json => new();

    /// <summary>XML document.</summary>
    public static XmlType Xml => new();

    /// <summary>Enum type.</summary>
    public static EnumType Enum(string name, params string[] values) => new(name, values);

    // ═══════════════════════════════════════════════════════════════════
    // BACKWARD-COMPATIBLE ALIASES (for test convenience)
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>32-bit integer (alias for Int32).</summary>
    public static IntType Int => new();

    /// <summary>32-bit float (alias for Float32).</summary>
    public static FloatType Float => new();

    /// <summary>64-bit double (alias for Float64).</summary>
    public static DoubleType Double => new();

    /// <summary>Fixed-length string (alias for FixedChar).</summary>
    public static CharType Char(int length) => new(length);

    /// <summary>Decimal with precision and scale (alias for DecimalNumber).</summary>
    public static DecimalType Decimal(int precision, int scale) => new(precision, scale);
}
