using H5;
using Reporting.React.Core;
using static Reporting.React.Core.Elements;

namespace Reporting.React.Components
{
    /// <summary>
    /// Walks a report layout definition and renders all components.
    /// </summary>
    public static class ReportRenderer
    {
        /// <summary>
        /// Renders an entire report from its definition and execution results.
        /// </summary>
        public static ReactElement Render(object reportDef, object executionResult)
        {
            var title = Script.Get<string>(reportDef, "title") ?? "Report";
            var layout = Script.Get<object>(reportDef, "layout");
            var dataSources = Script.Get<object>(executionResult, "dataSources");

            if (layout == null)
            {
                return Div(
                    className: "report-error",
                    children: new[] { Text("No layout defined for this report.") }
                );
            }

            var rows = Script.Get<object[]>(layout, "rows") ?? new object[0];
            var renderedRows = new ReactElement[rows.Length + 1];

            // Report title
            renderedRows[0] = H(1, className: "report-title", children: new[] { Text(title) });

            for (var r = 0; r < rows.Length; r++)
            {
                renderedRows[r + 1] = RenderRow(rows[r], dataSources);
            }

            return Div(className: "report-container", children: renderedRows);
        }

        private static ReactElement RenderRow(object row, object dataSources)
        {
            var cells = Script.Get<object[]>(row, "cells") ?? new object[0];
            var renderedCells = new ReactElement[cells.Length];

            for (var c = 0; c < cells.Length; c++)
            {
                renderedCells[c] = RenderCell(cells[c], dataSources);
            }

            return Div(className: "report-row", children: renderedCells);
        }

        private static ReactElement RenderCell(object cell, object dataSources)
        {
            var colSpan = Script.Get<int>(cell, "colSpan");
            var component = Script.Get<object>(cell, "component");

            if (component == null)
            {
                return Div(className: "report-cell report-cell-" + colSpan);
            }

            var rendered = RenderComponent(component, dataSources);

            return Div(
                className: "report-cell report-cell-" + colSpan,
                children: new[] { rendered }
            );
        }

        private static ReactElement RenderComponent(object component, object dataSources)
        {
            var type = Script.Get<string>(component, "type");
            var dsId = Script.Get<string>(component, "dataSource");
            var title = Script.Get<string>(component, "title") ?? "";

            // Resolve the data source result if specified
            object dsResult = null;
            if (dsId != null && dataSources != null)
            {
                dsResult = Script.Write<object>("dataSources[dsId]");
            }

            if (type == "Metric" || type == "metric")
            {
                var valueField = Script.Get<string>(component, "value") ?? "";
                var format = Script.Get<string>(component, "format") ?? "number";
                return MetricComponent.Render(
                    title: title,
                    valueField: valueField,
                    format: format,
                    dataSourceResult: dsResult
                );
            }

            if (type == "Chart" || type == "chart")
            {
                var chartType = Script.Get<string>(component, "chartType") ?? "bar";
                var xAxis = Script.Get<object>(component, "xAxis");
                var yAxis = Script.Get<object>(component, "yAxis");

                if (chartType == "Bar" || chartType == "bar")
                {
                    return BarChartComponent.Render(
                        title: title,
                        xAxisDef: xAxis,
                        yAxisDef: yAxis,
                        dataSourceResult: dsResult
                    );
                }

                // Default: render as bar chart for MVP
                return BarChartComponent.Render(
                    title: title,
                    xAxisDef: xAxis,
                    yAxisDef: yAxis,
                    dataSourceResult: dsResult
                );
            }

            if (type == "Table" || type == "table")
            {
                var columns = Script.Get<object>(component, "columns");
                var pageSize = Script.Get<int>(component, "pageSize");
                return TableComponent.Render(
                    title: title,
                    columnDefs: columns,
                    dataSourceResult: dsResult,
                    pageSize: pageSize > 0 ? pageSize : 50
                );
            }

            if (type == "Text" || type == "text")
            {
                var content = Script.Get<string>(component, "content") ?? "";
                var style = Script.Get<string>(component, "style") ?? "body";
                return TextComponent.Render(content: content, style: style);
            }

            return Div(
                className: "report-unknown-component",
                children: new[] { Text("Unknown component type: " + type) }
            );
        }
    }
}
