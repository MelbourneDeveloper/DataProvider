using System;
using Dashboard.React;
using static Dashboard.React.Elements;

namespace Dashboard.Components
{
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
    /// Metric card props class.
    /// </summary>
    public class MetricCardProps
    {
        /// <summary>Label text.</summary>
        public string Label { get; set; }

        /// <summary>Value text.</summary>
        public string Value { get; set; }

        /// <summary>Icon factory.</summary>
        public Func<ReactElement> Icon { get; set; }

        /// <summary>Icon color class.</summary>
        public string IconColor { get; set; }

        /// <summary>Trend value text.</summary>
        public string TrendValue { get; set; }

        /// <summary>Trend direction.</summary>
        public TrendDirection Trend { get; set; }
    }

    /// <summary>
    /// Metric card component for displaying KPIs.
    /// </summary>
    public static class MetricCard
    {
        /// <summary>
        /// Renders a metric card.
        /// </summary>
        public static ReactElement Render(MetricCardProps props)
        {
            ReactElement trendElement;
            if (props.TrendValue != null)
            {
                trendElement = Div(
                    className: "metric-trend " + TrendClass(props.Trend),
                    children: new[] { TrendIcon(props.Trend), Text(props.TrendValue) }
                );
            }
            else
            {
                trendElement = Text("");
            }

            return Div(
                className: "metric-card",
                children: new[]
                {
                    // Icon
                    Div(
                        className: "metric-icon " + (props.IconColor ?? "blue"),
                        children: new[] { props.Icon() }
                    ),
                    // Value
                    Div(className: "metric-value", children: new[] { Text(props.Value) }),
                    // Label
                    Div(className: "metric-label", children: new[] { Text(props.Label) }),
                    // Trend (if provided)
                    trendElement,
                }
            );
        }

        private static string TrendClass(TrendDirection trend)
        {
            if (trend == TrendDirection.Up)
            {
                return "up";
            }
            else if (trend == TrendDirection.Down)
            {
                return "down";
            }
            else
            {
                return "neutral";
            }
        }

        private static ReactElement TrendIcon(TrendDirection trend)
        {
            if (trend == TrendDirection.Up)
            {
                return Icons.TrendUp();
            }
            else if (trend == TrendDirection.Down)
            {
                return Icons.TrendDown();
            }
            else
            {
                return Text("");
            }
        }
    }
}
