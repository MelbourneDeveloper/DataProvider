namespace Dashboard.Components;

using Dashboard.React;
using static Dashboard.React.Elements;

/// <summary>
/// Metric card component for displaying KPIs.
/// </summary>
public static class MetricCard
{
    /// <summary>
    /// Metric card props.
    /// </summary>
    public record Props(
        string Label,
        string Value,
        Func<ReactElement> Icon,
        string IconColor = "blue",
        string? TrendValue = null,
        TrendDirection Trend = TrendDirection.Neutral
    );

    /// <summary>
    /// Trend direction enum.
    /// </summary>
    public enum TrendDirection
    {
        /// <summary>Upward trend.</summary>
        Up,

        /// <summary>Downward trend.</summary>
        Down,

        /// <summary>No change.</summary>
        Neutral,
    }

    /// <summary>
    /// Renders a metric card.
    /// </summary>
    public static ReactElement Render(Props props) =>
        Div(
            className: "metric-card",
            children: new[]
            {
                // Icon
                Div(className: $"metric-icon {props.IconColor}", children: new[] { props.Icon() }),
                // Value
                Div(className: "metric-value", children: new[] { Text(props.Value) }),
                // Label
                Div(className: "metric-label", children: new[] { Text(props.Label) }),
                // Trend (if provided)
                props.TrendValue != null
                    ? Div(
                        className: $"metric-trend {TrendClass(props.Trend)}",
                        children: new[] { TrendIcon(props.Trend), Text(props.TrendValue) }
                    )
                    : Text(""),
            }
        );

    private static string TrendClass(TrendDirection trend) =>
        trend switch
        {
            TrendDirection.Up => "up",
            TrendDirection.Down => "down",
            _ => "neutral",
        };

    private static ReactElement TrendIcon(TrendDirection trend) =>
        trend switch
        {
            TrendDirection.Up => Icons.TrendUp(),
            TrendDirection.Down => Icons.TrendDown(),
            _ => Text(""),
        };
}
