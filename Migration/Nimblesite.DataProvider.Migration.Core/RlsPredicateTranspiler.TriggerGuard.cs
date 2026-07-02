using System.Text;
using GuardSqlError = Outcome.Result<
    string,
    Nimblesite.DataProvider.Migration.Core.MigrationError
>.Error<string, Nimblesite.DataProvider.Migration.Core.MigrationError>;
using GuardSqlOk = Outcome.Result<string, Nimblesite.DataProvider.Migration.Core.MigrationError>.Ok<
    string,
    Nimblesite.DataProvider.Migration.Core.MigrationError
>;
using GuardSqlResult = Outcome.Result<
    string,
    Nimblesite.DataProvider.Migration.Core.MigrationError
>;

namespace Nimblesite.DataProvider.Migration.Core;

// Implements [MIG-TRIGGER-GUARD-LQL] from
// docs/specs/declarative-triggers-spec.md (GitHub issue 82).

/// <summary>
/// Trigger guard predicate translation. Extends the RLS predicate transpiler
/// with <c>old.</c>/<c>new.</c> row references, <c>and</c>/<c>or</c>
/// composition, and <c>[not] exists(pipeline)</c> subqueries embedded in a
/// larger predicate.
/// </summary>
public static partial class RlsPredicateTranspiler
{
    private const string OldRowSentinelPrefix = "__TRG_OLD_";
    private const string NewRowSentinelPrefix = "__TRG_NEW_";
    private const string ExistsMarkerPrefix = "__TRG_EXISTS_";

    /// <summary>
    /// Translate a trigger guard LQL predicate to platform-specific SQL.
    /// Row column references use <c>old.</c>/<c>new.</c> prefixes
    /// (case-insensitive) and become platform-quoted <c>OLD.</c>/<c>NEW.</c>
    /// references. <c>exists(pipeline)</c> and <c>not exists(pipeline)</c>
    /// subqueries may appear anywhere in the predicate.
    /// </summary>
    /// <param name="lql">LQL guard predicate.</param>
    /// <param name="platform">Target platform.</param>
    /// <param name="triggerName">Trigger name -- used for error messages.</param>
    public static GuardSqlResult TranslateGuardPredicate(
        string lql,
        RlsPlatform platform,
        string triggerName
    )
    {
        if (string.IsNullOrWhiteSpace(lql))
        {
            return new GuardSqlError(MigrationError.RlsEmptyPredicate(triggerName));
        }

        var withRowSentinels = SubstituteRowReferences(lql.Trim());
        var (remainder, segments) = ExtractExistsSegments(withRowSentinels);

        var simple = TranslateSimplePredicate(remainder, platform, triggerName);
        if (simple is GuardSqlError simpleError)
        {
            return simpleError;
        }
        if (simple is not GuardSqlOk simpleOk)
        {
            return new GuardSqlError(
                MigrationError.RlsLqlParse(triggerName, "unknown guard predicate failure")
            );
        }

        var sql = simpleOk.Value;
        for (var index = 0; index < segments.Count; index++)
        {
            var segment = segments[index];
            var subquery = TranslateExistsSubquery(segment.InnerLql, platform, triggerName);
            if (subquery is GuardSqlError subqueryError)
            {
                return subqueryError;
            }
            if (subquery is not GuardSqlOk subqueryOk)
            {
                return new GuardSqlError(
                    MigrationError.RlsLqlParse(triggerName, "unknown guard subquery failure")
                );
            }
            var replacement = segment.Negated ? $"NOT {subqueryOk.Value}" : subqueryOk.Value;
            sql = sql.Replace(ExistsMarker(index), replacement, StringComparison.Ordinal);
        }

        return new GuardSqlOk(RestoreRowReferences(sql, platform));
    }

    private static string ExistsMarker(int index) => $"'{ExistsMarkerPrefix}{index}__'";

    /// <summary>
    /// Replaces <c>old.col</c>/<c>new.col</c> references with sentinel string
    /// literals that survive LQL transpilation. Skips string literals.
    /// </summary>
    private static string SubstituteRowReferences(string source)
    {
        var sb = new StringBuilder(source.Length + 32);
        var i = 0;
        while (i < source.Length)
        {
            if (source[i] == '\'')
            {
                i = AppendStringLiteral(source, i, sb);
                continue;
            }
            if (IsIdentifierStart(source, i))
            {
                var start = i;
                i = ReadIdentifierEnd(source, i);
                var word = source[start..i];
                var isRowRef =
                    (
                        word.Equals("old", StringComparison.OrdinalIgnoreCase)
                        || word.Equals("new", StringComparison.OrdinalIgnoreCase)
                    )
                    && i + 1 < source.Length
                    && source[i] == '.'
                    && (char.IsLetter(source[i + 1]) || source[i + 1] == '_');
                if (isRowRef)
                {
                    var columnStart = i + 1;
                    var columnEnd = ReadIdentifierEnd(source, columnStart);
                    var prefix = word.Equals("old", StringComparison.OrdinalIgnoreCase)
                        ? OldRowSentinelPrefix
                        : NewRowSentinelPrefix;
                    sb.Append('\'')
                        .Append(prefix)
                        .Append(source[columnStart..columnEnd])
                        .Append("__'");
                    i = columnEnd;
                    continue;
                }
                sb.Append(word);
                continue;
            }
            sb.Append(source[i]);
            i++;
        }
        return sb.ToString();
    }

    /// <summary>
    /// Extracts every top-level <c>[not] exists(...)</c> subquery, replacing
    /// each with a marker literal so the remainder can be translated as a
    /// simple predicate.
    /// </summary>
    private static (
        string Remainder,
        IReadOnlyList<GuardExistsSegment> Segments
    ) ExtractExistsSegments(string predicate)
    {
        var segments = new List<GuardExistsSegment>();
        var sb = new StringBuilder(predicate.Length);
        var i = 0;
        while (i < predicate.Length)
        {
            if (predicate[i] == '\'')
            {
                i = AppendStringLiteral(predicate, i, sb);
                continue;
            }
            if (IsIdentifierStart(predicate, i))
            {
                var start = i;
                i = ReadIdentifierEnd(predicate, i);
                var word = predicate[start..i];
                if (word.Equals("not", StringComparison.OrdinalIgnoreCase))
                {
                    var afterNot = SkipWhitespace(predicate, i);
                    var keywordEnd = ReadIdentifierEnd(predicate, afterNot);
                    var next = predicate[afterNot..keywordEnd];
                    if (
                        next.Equals("exists", StringComparison.OrdinalIgnoreCase)
                        && TryReadSubquery(predicate, keywordEnd, out var negatedInner, out var end)
                    )
                    {
                        segments.Add(new GuardExistsSegment(negatedInner, Negated: true));
                        sb.Append(ExistsMarker(segments.Count - 1));
                        i = end;
                        continue;
                    }
                }
                if (
                    word.Equals("exists", StringComparison.OrdinalIgnoreCase)
                    && TryReadSubquery(predicate, i, out var inner, out var afterSubquery)
                )
                {
                    segments.Add(new GuardExistsSegment(inner, Negated: false));
                    sb.Append(ExistsMarker(segments.Count - 1));
                    i = afterSubquery;
                    continue;
                }
                sb.Append(word);
                continue;
            }
            sb.Append(predicate[i]);
            i++;
        }
        return (sb.ToString(), segments);
    }

    /// <summary>
    /// Reads a balanced-paren subquery starting at the first <c>(</c> after
    /// <paramref name="from"/>. Returns false when no subquery follows.
    /// </summary>
    private static bool TryReadSubquery(string source, int from, out string inner, out int end)
    {
        inner = string.Empty;
        end = from;
        var i = SkipWhitespace(source, from);
        if (i >= source.Length || source[i] != '(')
        {
            return false;
        }
        var openIndex = i;
        var depth = 1;
        i++;
        while (i < source.Length && depth > 0)
        {
            switch (source[i])
            {
                case '\'':
                    i = SkipStringLiteral(source, i);
                    continue;
                case '(':
                    depth++;
                    break;
                case ')':
                    depth--;
                    if (depth == 0)
                    {
                        inner = source[(openIndex + 1)..i];
                        end = i + 1;
                        return true;
                    }
                    break;
            }
            i++;
        }
        return false;
    }

    /// <summary>
    /// Replaces row-reference sentinel literals with platform-quoted
    /// <c>OLD.</c>/<c>NEW.</c> column references.
    /// </summary>
    private static string RestoreRowReferences(string sql, RlsPlatform platform)
    {
        var (open, close) = platform == RlsPlatform.Postgres ? ('"', '"') : ('[', ']');
        var restored = ReplaceRowSentinels(sql, OldRowSentinelPrefix, "OLD", open, close);
        return ReplaceRowSentinels(restored, NewRowSentinelPrefix, "NEW", open, close);
    }

    private static string ReplaceRowSentinels(
        string sql,
        string sentinelPrefix,
        string rowAlias,
        char open,
        char close
    )
    {
        var marker = $"'{sentinelPrefix}";
        var sb = new StringBuilder(sql.Length);
        var i = 0;
        while (i < sql.Length)
        {
            var idx = sql.IndexOf(marker, i, StringComparison.Ordinal);
            if (idx < 0)
            {
                sb.Append(sql, i, sql.Length - i);
                break;
            }
            sb.Append(sql, i, idx - i);
            var columnStart = idx + marker.Length;
            var terminator = sql.IndexOf("__'", columnStart, StringComparison.Ordinal);
            if (terminator < 0)
            {
                sb.Append(sql, idx, sql.Length - idx);
                break;
            }
            sb.Append(rowAlias)
                .Append('.')
                .Append(open)
                .Append(sql[columnStart..terminator])
                .Append(close);
            i = terminator + 3;
        }
        return sb.ToString();
    }

    private static bool IsIdentifierStart(string source, int i) =>
        (char.IsLetter(source[i]) || source[i] == '_')
        && (
            i == 0
            || (
                !char.IsLetterOrDigit(source[i - 1]) && source[i - 1] != '_' && source[i - 1] != '.'
            )
        );

    private static int ReadIdentifierEnd(string source, int start)
    {
        var i = start;
        while (i < source.Length && (char.IsLetterOrDigit(source[i]) || source[i] == '_'))
        {
            i++;
        }
        return i;
    }

    private static int SkipWhitespace(string source, int start)
    {
        var i = start;
        while (i < source.Length && char.IsWhiteSpace(source[i]))
        {
            i++;
        }
        return i;
    }

    private static int SkipStringLiteral(string source, int start)
    {
        // start points at the opening quote; returns index after the closing
        // quote (handles '' escapes).
        var i = start + 1;
        while (i < source.Length)
        {
            if (source[i] == '\'')
            {
                if (i + 1 < source.Length && source[i + 1] == '\'')
                {
                    i += 2;
                    continue;
                }
                return i + 1;
            }
            i++;
        }
        return i;
    }

    private static int AppendStringLiteral(string source, int start, StringBuilder sb)
    {
        var end = SkipStringLiteral(source, start);
        sb.Append(source, start, end - start);
        return end;
    }
}

/// <summary>
/// One extracted <c>[not] exists(...)</c> subquery segment of a guard
/// predicate.
/// </summary>
internal sealed record GuardExistsSegment(string InnerLql, bool Negated);
