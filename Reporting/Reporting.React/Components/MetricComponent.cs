using H5;
using Reporting.React.Core;
using static Reporting.React.Core.Elements;

namespace Reporting.React.Components
{
    /// <summary>
    /// Renders a single KPI metric card.
    /// </summary>
    public static class MetricComponent
    {
        /// <summary>
        /// Renders a metric component showing a single value.
        /// </summary>
        public static ReactElement Render(
            string title,
            string valueField,
            string format,
            object dataSourceResult
        )
        {
            var displayValue = ExtractMetricValue(dataSourceResult, valueField, format);

            return Div(
                className: "report-metric",
                children: new[]
                {
                    Div(className: "report-metric-value", children: new[] { Text(displayValue) }),
                    Div(className: "report-metric-title", children: new[] { Text(title) }),
                }
            );
        }

        private static string ExtractMetricValue(
            object dataSourceResult,
            string valueField,
            string format
        )
        {
            var rows = Script.Get<object[]>(dataSourceResult, "rows");
            if (rows == null || rows.Length == 0)
                return "—";

            var columns = Script.Get<string[]>(dataSourceResult, "columnNames");
            if (columns == null)
                return "—";

            var colIndex = -1;
            for (var i = 0; i < columns.Length; i++)
            {
                if (columns[i] == valueField)
                {
                    colIndex = i;
                    break;
                }
            }

            if (colIndex < 0)
                return "—";

            var firstRow = Script.Write<object[]>("rows[0]");
            var rawValue = Script.Write<object>("firstRow[colIndex]");

            if (rawValue == null)
                return "—";

            if (format == "currency")
                return "$" + FormatNumber(rawValue);

            if (format == "number")
                return FormatNumber(rawValue);

            return rawValue.ToString();
        }

        private static string FormatNumber(object value)
        {
            return Script.Call<string>("Number(value).toLocaleString");
        }
    }
}
