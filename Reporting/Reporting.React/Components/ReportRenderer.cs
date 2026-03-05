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
            var customCss = Script.Get<string>(reportDef, "customCss");
            var dataSources = Script.Get<object>(executionResult, "dataSources");

            if (layout == null)
            {
                return Div(
                    className: "report-error",
                    children: new[] { Text("No layout defined for this report.") }
                );
            }

            var rows = Script.Get<object[]>(layout, "rows") ?? new object[0];
            var hasCustomCss = customCss != null && customCss.Length > 0;
            var renderedRows = new ReactElement[rows.Length + 1 + (hasCustomCss ? 1 : 0)];
            var index = 0;

            // Inject custom CSS as a <style> tag
            if (hasCustomCss)
            {
                renderedRows[index++] = InjectStyleTag(customCss);
            }

            // Report title
            renderedRows[index++] = H(
                1,
                className: "report-title",
                children: new[] { Text(title) }
            );

            for (var r = 0; r < rows.Length; r++)
            {
                renderedRows[index++] = RenderRow(rows[r], dataSources);
            }

            return Div(className: "report-container", children: renderedRows);
        }

        private static ReactElement InjectStyleTag(string css)
        {
            var props = new { dangerouslySetInnerHTML = new { __html = css } };
            return Script.Call<ReactElement>("React.createElement", "style", props);
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
            var cellCssClass = Script.Get<string>(cell, "cssClass");

            var baseClass = "report-cell report-cell-" + colSpan;
            var cellClassName = cellCssClass != null ? baseClass + " " + cellCssClass : baseClass;

            if (component == null)
            {
                return Div(className: cellClassName);
            }

            var rendered = RenderComponent(component, dataSources);

            return Div(className: cellClassName, children: new[] { rendered });
        }

        private static ReactElement RenderComponent(object component, object dataSources)
        {
            var type = Script.Get<string>(component, "type");
            var dsId = Script.Get<string>(component, "dataSource");
            var title = Script.Get<string>(component, "title") ?? "";
            var cssClass = Script.Get<string>(component, "cssClass");
            var cssStyle = Script.Get<object>(component, "cssStyle");

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
                    dataSourceResult: dsResult,
                    cssClass: cssClass,
                    cssStyle: cssStyle
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
                        dataSourceResult: dsResult,
                        cssClass: cssClass,
                        cssStyle: cssStyle
                    );
                }

                // Default: render as bar chart for MVP
                return BarChartComponent.Render(
                    title: title,
                    xAxisDef: xAxis,
                    yAxisDef: yAxis,
                    dataSourceResult: dsResult,
                    cssClass: cssClass,
                    cssStyle: cssStyle
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
                    pageSize: pageSize > 0 ? pageSize : 50,
                    cssClass: cssClass,
                    cssStyle: cssStyle
                );
            }

            if (type == "Text" || type == "text")
            {
                var content = Script.Get<string>(component, "content") ?? "";
                var style = Script.Get<string>(component, "style") ?? "body";
                return TextComponent.Render(
                    content: content,
                    style: style,
                    cssClass: cssClass,
                    cssStyle: cssStyle
                );
            }

            return Div(
                className: "report-unknown-component",
                children: new[] { Text("Unknown component type: " + type) }
            );
        }
    }
}
