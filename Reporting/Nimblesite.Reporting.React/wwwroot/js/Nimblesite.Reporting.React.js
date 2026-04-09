/**
 * @compiler H5 26.3.64893+945123d2bd7640b7bd10ff332868c3e7b2f4ec79
 */
H5.assemblyVersion("Nimblesite.Reporting.React","0.9.0.0");
H5.assembly("Nimblesite.Reporting.React", function ($asm, globals) {
    "use strict";

    /** @namespace Nimblesite.Reporting.React.Api */

    /**
     * Client for the Reporting API.
     *
     * @static
     * @abstract
     * @public
     * @class Nimblesite.Reporting.React.Api.ReportApiClient
     */
    H5.define("Nimblesite.Reporting.React.Api.ReportApiClient", {
        statics: {
            fields: {
                _baseUrl: null
            },
            ctors: {
                init: function () {
                    this._baseUrl = "";
                }
            },
            methods: {
                /**
                 * Configures the API base URL.
                 *
                 * @static
                 * @public
                 * @this Nimblesite.Reporting.React.Api.ReportApiClient
                 * @memberof Nimblesite.Reporting.React.Api.ReportApiClient
                 * @param   {string}    baseUrl
                 * @return  {void}
                 */
                Configure: function (baseUrl) {
                    Nimblesite.Reporting.React.Api.ReportApiClient._baseUrl = baseUrl;
                },
                /**
                 * Fetches the list of available reports.
                 *
                 * @static
                 * @public
                 * @this Nimblesite.Reporting.React.Api.ReportApiClient
                 * @memberof Nimblesite.Reporting.React.Api.ReportApiClient
                 * @return  {System.Threading.Tasks.Task$1}
                 */
                GetReportsAsync: function () {
                    var $tcs = new System.Threading.Tasks.TaskCompletionSource();
                    (async () => {
                        {
                            var response = (await H5.toPromise(Nimblesite.Reporting.React.Api.ReportApiClient.FetchAsync((Nimblesite.Reporting.React.Api.ReportApiClient._baseUrl || "") + "/api/reports")));
                            return Nimblesite.Reporting.React.Api.ReportApiClient.ParseJson(System.Array.type(System.Object), response);
                        }})().then(function ($r) { $tcs.setResult($r); }, function ($e) { $tcs.setException(System.Exception.create($e)); });
                    return $tcs.task;
                },
                /**
                 * Fetches a report definition (metadata + layout).
                 *
                 * @static
                 * @public
                 * @this Nimblesite.Reporting.React.Api.ReportApiClient
                 * @memberof Nimblesite.Reporting.React.Api.ReportApiClient
                 * @param   {string}                           reportId
                 * @return  {System.Threading.Tasks.Task$1}
                 */
                GetReportAsync: function (reportId) {
                    var $tcs = new System.Threading.Tasks.TaskCompletionSource();
                    (async () => {
                        {
                            var response = (await H5.toPromise(Nimblesite.Reporting.React.Api.ReportApiClient.FetchAsync((Nimblesite.Reporting.React.Api.ReportApiClient._baseUrl || "") + "/api/reports/" + (reportId || ""))));
                            return Nimblesite.Reporting.React.Api.ReportApiClient.ParseJson(System.Object, response);
                        }})().then(function ($r) { $tcs.setResult($r); }, function ($e) { $tcs.setException(System.Exception.create($e)); });
                    return $tcs.task;
                },
                /**
                 * Executes a report with parameters and returns data.
                 *
                 * @static
                 * @public
                 * @this Nimblesite.Reporting.React.Api.ReportApiClient
                 * @memberof Nimblesite.Reporting.React.Api.ReportApiClient
                 * @param   {string}                           reportId      
                 * @param   {System.Object}                    parameters
                 * @return  {System.Threading.Tasks.Task$1}
                 */
                ExecuteReportAsync: function (reportId, parameters) {
                    var $tcs = new System.Threading.Tasks.TaskCompletionSource();
                    (async () => {
                        {
                            var body = { parameters: parameters, format: "json" };
                            var response = (await H5.toPromise(Nimblesite.Reporting.React.Api.ReportApiClient.PostAsync((Nimblesite.Reporting.React.Api.ReportApiClient._baseUrl || "") + "/api/reports/" + (reportId || "") + "/execute", body)));
                            return Nimblesite.Reporting.React.Api.ReportApiClient.ParseJson(System.Object, response);
                        }})().then(function ($r) { $tcs.setResult($r); }, function ($e) { $tcs.setException(System.Exception.create($e)); });
                    return $tcs.task;
                },
                FetchAsync: function (url) {
                    var $tcs = new System.Threading.Tasks.TaskCompletionSource();
                    (async () => {
                        {
                            var response = (await H5.toPromise(fetch(url, { method: "GET", headers: { Accept: "application/json" } })));

                            if (!response.Ok) {
                                throw new System.Exception("HTTP " + response.Status);
                            }

                            return (await H5.toPromise(response.Text()));
                        }})().then(function ($r) { $tcs.setResult($r); }, function ($e) { $tcs.setException(System.Exception.create($e)); });
                    return $tcs.task;
                },
                PostAsync: function (url, data) {
                    var $tcs = new System.Threading.Tasks.TaskCompletionSource();
                    (async () => {
                        {
                            var response = (await H5.toPromise(fetch(url, { method: "POST", headers: { Accept: "application/json", ContentType: "application/json" }, body: JSON.stringify(H5.unbox(data)) })));

                            if (!response.Ok) {
                                throw new System.Exception("HTTP " + response.Status);
                            }

                            return (await H5.toPromise(response.Text()));
                        }})().then(function ($r) { $tcs.setResult($r); }, function ($e) { $tcs.setException(System.Exception.create($e)); });
                    return $tcs.task;
                },
                ParseJson: function (T, json) {
                    return JSON.parse(json);
                }
            }
        }
    });

    /** @namespace Nimblesite.Reporting.React */

    /**
     * Report viewer application state.
     *
     * @public
     * @class Nimblesite.Reporting.React.AppState
     */
    H5.define("Nimblesite.Reporting.React.AppState", {
        fields: {
            /**
             * Current report definition (from API).
             *
             * @instance
             * @public
             * @memberof Nimblesite.Reporting.React.AppState
             * @function ReportDef
             * @type System.Object
             */
            ReportDef: null,
            /**
             * Current execution result (from API).
             *
             * @instance
             * @public
             * @memberof Nimblesite.Reporting.React.AppState
             * @function ExecutionResult
             * @type System.Object
             */
            ExecutionResult: null,
            /**
             * Whether a report is currently loading.
             *
             * @instance
             * @public
             * @memberof Nimblesite.Reporting.React.AppState
             * @function Loading
             * @type boolean
             */
            Loading: false,
            /**
             * Error message if loading failed.
             *
             * @instance
             * @public
             * @memberof Nimblesite.Reporting.React.AppState
             * @function Error
             * @type string
             */
            Error: null,
            /**
             * List of available reports.
             *
             * @instance
             * @public
             * @memberof Nimblesite.Reporting.React.AppState
             * @function ReportList
             * @type Array.<System.Object>
             */
            ReportList: null
        }
    });

    /** @namespace Nimblesite.Reporting.React.Components */

    /**
     * Renders a bar chart using SVG.
     *
     * @static
     * @abstract
     * @public
     * @class Nimblesite.Reporting.React.Components.BarChartComponent
     */
    H5.define("Nimblesite.Reporting.React.Components.BarChartComponent", {
        statics: {
            fields: {
                ChartWidth: 0,
                ChartHeight: 0,
                Padding: 0,
                BottomPadding: 0,
                BarColors: null
            },
            ctors: {
                init: function () {
                    this.ChartWidth = 500;
                    this.ChartHeight = 300;
                    this.Padding = 50;
                    this.BottomPadding = 80;
                    this.BarColors = System.Array.init(["#00BCD4", "#2E4450", "#FF6B6B", "#4CAF50", "#FF9800", "#9C27B0", "#3F51B5", "#009688"], System.String);
                }
            },
            methods: {
                /**
                 * Renders a bar chart from data source results.
                 *
                 * @static
                 * @public
                 * @this Nimblesite.Reporting.React.Components.BarChartComponent
                 * @memberof Nimblesite.Reporting.React.Components.BarChartComponent
                 * @param   {string}           title               
                 * @param   {System.Object}    xAxisDef            
                 * @param   {System.Object}    yAxisDef            
                 * @param   {System.Object}    dataSourceResult    
                 * @param   {string}           cssClass            
                 * @param   {System.Object}    cssStyle
                 * @return  {Object}
                 */
                Render: function (title, xAxisDef, yAxisDef, dataSourceResult, cssClass, cssStyle) {
                    var $t, $t1, $t2;
                    if (cssClass === void 0) { cssClass = null; }
                    if (cssStyle === void 0) { cssStyle = null; }
                    var xField = H5.unbox(xAxisDef)["field"];
                    var yField = H5.unbox(yAxisDef)["field"];
                    var yLabel = ($t = H5.unbox(yAxisDef)["label"], $t != null ? $t : yField);
                    var columnNames = H5.unbox(dataSourceResult)["columnNames"];
                    var rows = H5.unbox(dataSourceResult)["rows"] || System.Array.init(0, null, System.Object);

                    var xIndex = Nimblesite.Reporting.React.Components.BarChartComponent.FindColumnIndex(columnNames, xField);
                    var yIndex = Nimblesite.Reporting.React.Components.BarChartComponent.FindColumnIndex(columnNames, yField);

                    var chartClassName = cssClass != null ? "report-chart " + (cssClass || "") : "report-chart";

                    if (xIndex < 0 || yIndex < 0 || rows.length === 0) {
                        return Nimblesite.Reporting.React.Core.Elements.Div(chartClassName, void 0, cssStyle, void 0, System.Array.init([Nimblesite.Reporting.React.Core.Elements.H(3, "report-component-title", System.Array.init([Nimblesite.Reporting.React.Core.Elements.Text(title)], Object)), Nimblesite.Reporting.React.Core.Elements.P(void 0, System.Array.init([Nimblesite.Reporting.React.Core.Elements.Text("No data available")], Object))], Object));
                    }

                    var labels = System.Array.init(rows.length, null, System.String);
                    var values = System.Array.init(rows.length, 0, System.Double);
                    var maxValue = 0.0;

                    for (var i = 0; i < rows.length; i = (i + 1) | 0) {
                        var row = rows[i];
                        labels[System.Array.index(i, labels)] = ($t1 = (($t2 = row[xIndex]) != null ? H5.toString($t2) : null), $t1 != null ? $t1 : "");
                        var rawVal = row[yIndex];
                        values[System.Array.index(i, values)] = Number(H5.unbox(rawVal));
                        if (values[System.Array.index(i, values)] > maxValue) {
                            maxValue = values[System.Array.index(i, values)];
                        }
                    }

                    if (maxValue === 0) {
                        maxValue = 1;
                    }

                    var drawWidth = 400;
                    var drawHeight = 170;
                    var barWidth = drawWidth / (rows.length * 2.0);
                    var barElements = System.Array.init(((H5.Int.mul(rows.length, 3) + 4) | 0), null, Object);
                    var elementIndex = 0;

                    barElements[System.Array.index(H5.identity(elementIndex, ((elementIndex = (elementIndex + 1) | 0))), barElements)] = Nimblesite.Reporting.React.Core.Elements.Line(Nimblesite.Reporting.React.Components.BarChartComponent.Padding, Nimblesite.Reporting.React.Components.BarChartComponent.Padding, Nimblesite.Reporting.React.Components.BarChartComponent.Padding, 220, "#ccc", 1);

                    barElements[System.Array.index(H5.identity(elementIndex, ((elementIndex = (elementIndex + 1) | 0))), barElements)] = Nimblesite.Reporting.React.Core.Elements.Line(Nimblesite.Reporting.React.Components.BarChartComponent.Padding, 220, 450, 220, "#ccc", 1);

                    barElements[System.Array.index(H5.identity(elementIndex, ((elementIndex = (elementIndex + 1) | 0))), barElements)] = Nimblesite.Reporting.React.Core.Elements.SvgText(15, 150.0, yLabel, "#666", "middle", "11px", "rotate(-90, 15, " + System.Double.format((150.0)) + ")");

                    barElements[System.Array.index(H5.identity(elementIndex, ((elementIndex = (elementIndex + 1) | 0))), barElements)] = Nimblesite.Reporting.React.Core.Elements.SvgText(45, 54, System.Double.format(H5.Math.round(maxValue, 0, 6)), "#666", "end", "10px", void 0);

                    for (var i1 = 0; i1 < rows.length; i1 = (i1 + 1) | 0) {
                        var barHeight = (values[System.Array.index(i1, values)] / maxValue) * drawHeight;
                        var x = ((Nimblesite.Reporting.React.Components.BarChartComponent.Padding + H5.Int.mul(i1, (((H5.Int.div(drawWidth, rows.length)) | 0)))) | 0) + barWidth * 0.5;
                        var y = 220 - barHeight;
                        var color = Nimblesite.Reporting.React.Components.BarChartComponent.BarColors[System.Array.index(i1 % Nimblesite.Reporting.React.Components.BarChartComponent.BarColors.length, Nimblesite.Reporting.React.Components.BarChartComponent.BarColors)];

                        barElements[System.Array.index(H5.identity(elementIndex, ((elementIndex = (elementIndex + 1) | 0))), barElements)] = Nimblesite.Reporting.React.Core.Elements.Rect(x, y, barWidth, barHeight, color, void 0);

                        barElements[System.Array.index(H5.identity(elementIndex, ((elementIndex = (elementIndex + 1) | 0))), barElements)] = Nimblesite.Reporting.React.Core.Elements.SvgText(x + barWidth / 2.0, y - 5, System.Double.format(H5.Math.round(values[System.Array.index(i1, values)], 0, 6)), "#333", "middle", "10px", void 0);

                        barElements[System.Array.index(H5.identity(elementIndex, ((elementIndex = (elementIndex + 1) | 0))), barElements)] = Nimblesite.Reporting.React.Core.Elements.SvgText(x + barWidth / 2.0, 235, Nimblesite.Reporting.React.Components.BarChartComponent.TruncateLabel(labels[System.Array.index(i1, labels)], 12), "#666", "middle", "10px", void 0);
                    }

                    var finalElements = System.Array.init(elementIndex, null, Object);
                    System.Array.copy(barElements, 0, finalElements, 0, elementIndex);

                    return Nimblesite.Reporting.React.Core.Elements.Div(chartClassName, void 0, cssStyle, void 0, System.Array.init([Nimblesite.Reporting.React.Core.Elements.H(3, "report-component-title", System.Array.init([Nimblesite.Reporting.React.Core.Elements.Text(title)], Object)), Nimblesite.Reporting.React.Core.Elements.Svg("report-bar-chart", Nimblesite.Reporting.React.Components.BarChartComponent.ChartWidth, Nimblesite.Reporting.React.Components.BarChartComponent.ChartHeight, "0 0 " + Nimblesite.Reporting.React.Components.BarChartComponent.ChartWidth + " " + Nimblesite.Reporting.React.Components.BarChartComponent.ChartHeight, finalElements)], Object));
                },
                FindColumnIndex: function (columnNames, field) {
                    if (columnNames == null || field == null) {
                        return -1;
                    }
                    for (var i = 0; i < columnNames.length; i = (i + 1) | 0) {
                        if (H5.referenceEquals(columnNames[System.Array.index(i, columnNames)], field)) {
                            return i;
                        }
                    }
                    return -1;
                },
                TruncateLabel: function (label, maxLen) {
                    if (label == null) {
                        return "";
                    }
                    return label.length <= maxLen ? label : (label.substr(0, ((maxLen - 1) | 0)) || "") + "\u2026";
                }
            }
        }
    });

    /**
     * Renders a single KPI metric card.
     *
     * @static
     * @abstract
     * @public
     * @class Nimblesite.Reporting.React.Components.MetricComponent
     */
    H5.define("Nimblesite.Reporting.React.Components.MetricComponent", {
        statics: {
            methods: {
                /**
                 * Renders a metric component showing a single value.
                 *
                 * @static
                 * @public
                 * @this Nimblesite.Reporting.React.Components.MetricComponent
                 * @memberof Nimblesite.Reporting.React.Components.MetricComponent
                 * @param   {string}           title               
                 * @param   {string}           valueField          
                 * @param   {string}           format              
                 * @param   {System.Object}    dataSourceResult    
                 * @param   {string}           cssClass            
                 * @param   {System.Object}    cssStyle
                 * @return  {Object}
                 */
                Render: function (title, valueField, format, dataSourceResult, cssClass, cssStyle) {
                    if (cssClass === void 0) { cssClass = null; }
                    if (cssStyle === void 0) { cssStyle = null; }
                    var displayValue = Nimblesite.Reporting.React.Components.MetricComponent.ExtractMetricValue(dataSourceResult, valueField, format);
                    var className = cssClass != null ? "report-metric " + (cssClass || "") : "report-metric";

                    return Nimblesite.Reporting.React.Core.Elements.Div(className, void 0, cssStyle, void 0, System.Array.init([Nimblesite.Reporting.React.Core.Elements.Div("report-metric-value", void 0, void 0, void 0, System.Array.init([Nimblesite.Reporting.React.Core.Elements.Text(displayValue)], Object)), Nimblesite.Reporting.React.Core.Elements.Div("report-metric-title", void 0, void 0, void 0, System.Array.init([Nimblesite.Reporting.React.Core.Elements.Text(title)], Object))], Object));
                },
                ExtractMetricValue: function (dataSourceResult, valueField, format) {
                    var rows = H5.unbox(dataSourceResult)["rows"];
                    if (rows == null || rows.length === 0) {
                        return "\u2014";
                    }

                    var columns = H5.unbox(dataSourceResult)["columnNames"];
                    if (columns == null) {
                        return "\u2014";
                    }

                    var colIndex = -1;
                    for (var i = 0; i < columns.length; i = (i + 1) | 0) {
                        if (H5.referenceEquals(columns[System.Array.index(i, columns)], valueField)) {
                            colIndex = i;
                            break;
                        }
                    }

                    if (colIndex < 0) {
                        return "\u2014";
                    }

                    var firstRow = rows[0];
                    var rawValue = firstRow[colIndex];

                    if (rawValue == null) {
                        return "\u2014";
                    }

                    if (H5.referenceEquals(format, "currency")) {
                        return "$" + (Nimblesite.Reporting.React.Components.MetricComponent.FormatNumber(rawValue) || "");
                    }

                    if (H5.referenceEquals(format, "number")) {
                        return Nimblesite.Reporting.React.Components.MetricComponent.FormatNumber(rawValue);
                    }

                    return H5.toString(rawValue);
                },
                FormatNumber: function (value) {
                    return Number(value).toLocaleString();
                }
            }
        }
    });

    /**
     * Walks a report layout definition and renders all components.
     *
     * @static
     * @abstract
     * @public
     * @class Nimblesite.Reporting.React.Components.ReportRenderer
     */
    H5.define("Nimblesite.Reporting.React.Components.ReportRenderer", {
        statics: {
            methods: {
                /**
                 * Renders an entire report from its definition and execution results.
                 *
                 * @static
                 * @public
                 * @this Nimblesite.Reporting.React.Components.ReportRenderer
                 * @memberof Nimblesite.Reporting.React.Components.ReportRenderer
                 * @param   {System.Object}    reportDef          
                 * @param   {System.Object}    executionResult
                 * @return  {Object}
                 */
                Render: function (reportDef, executionResult) {
                    var $t;
                    var title = ($t = H5.unbox(reportDef)["title"], $t != null ? $t : "Report");
                    var layout = H5.unbox(reportDef)["layout"];
                    var customCss = H5.unbox(reportDef)["customCss"];
                    var dataSources = H5.unbox(executionResult)["dataSources"];

                    if (layout == null) {
                        return Nimblesite.Reporting.React.Core.Elements.Div("report-error", void 0, void 0, void 0, System.Array.init([Nimblesite.Reporting.React.Core.Elements.Text("No layout defined for this report.")], Object));
                    }

                    var rows = H5.unbox(layout)["rows"] || System.Array.init(0, null, System.Object);
                    var hasCustomCss = customCss != null && customCss.length > 0;
                    var renderedRows = System.Array.init(((((rows.length + 1) | 0) + (hasCustomCss ? 1 : 0)) | 0), null, Object);
                    var index = 0;

                    if (hasCustomCss) {
                        renderedRows[System.Array.index(H5.identity(index, ((index = (index + 1) | 0))), renderedRows)] = Nimblesite.Reporting.React.Components.ReportRenderer.InjectStyleTag(customCss);
                    }

                    renderedRows[System.Array.index(H5.identity(index, ((index = (index + 1) | 0))), renderedRows)] = Nimblesite.Reporting.React.Core.Elements.H(1, "report-title", System.Array.init([Nimblesite.Reporting.React.Core.Elements.Text(title)], Object));

                    for (var r = 0; r < rows.length; r = (r + 1) | 0) {
                        renderedRows[System.Array.index(H5.identity(index, ((index = (index + 1) | 0))), renderedRows)] = Nimblesite.Reporting.React.Components.ReportRenderer.RenderRow(rows[System.Array.index(r, rows)], dataSources);
                    }

                    return Nimblesite.Reporting.React.Core.Elements.Div("report-container", void 0, void 0, void 0, renderedRows);
                },
                InjectStyleTag: function (css) {
                    var props = { dangerouslySetInnerHTML: { __html: css } };
                    return React.createElement("style", props);
                },
                RenderRow: function (row, dataSources) {
                    var cells = H5.unbox(row)["cells"] || System.Array.init(0, null, System.Object);
                    var renderedCells = System.Array.init(cells.length, null, Object);

                    for (var c = 0; c < cells.length; c = (c + 1) | 0) {
                        renderedCells[System.Array.index(c, renderedCells)] = Nimblesite.Reporting.React.Components.ReportRenderer.RenderCell(cells[System.Array.index(c, cells)], dataSources);
                    }

                    return Nimblesite.Reporting.React.Core.Elements.Div("report-row", void 0, void 0, void 0, renderedCells);
                },
                RenderCell: function (cell, dataSources) {
                    var colSpan = H5.unbox(cell)["colSpan"];
                    var component = H5.unbox(cell)["component"];
                    var cellCssClass = H5.unbox(cell)["cssClass"];

                    var baseClass = "report-cell report-cell-" + colSpan;
                    var cellClassName = cellCssClass != null ? (baseClass || "") + " " + (cellCssClass || "") : baseClass;

                    if (component == null) {
                        return Nimblesite.Reporting.React.Core.Elements.Div(cellClassName, void 0, void 0, void 0);
                    }

                    var rendered = Nimblesite.Reporting.React.Components.ReportRenderer.RenderComponent(component, dataSources);

                    return Nimblesite.Reporting.React.Core.Elements.Div(cellClassName, void 0, void 0, void 0, System.Array.init([rendered], Object));
                },
                RenderComponent: function (component, dataSources) {
                    var $t, $t1, $t2, $t3, $t4, $t5;
                    var type = H5.unbox(component)["type"];
                    var dsId = H5.unbox(component)["dataSource"];
                    var title = ($t = H5.unbox(component)["title"], $t != null ? $t : "");
                    var cssClass = H5.unbox(component)["cssClass"];
                    var cssStyle = H5.unbox(component)["cssStyle"];

                    var dsResult = null;
                    if (dsId != null && dataSources != null) {
                        dsResult = dataSources[dsId];
                    }

                    if (H5.referenceEquals(type, "Metric") || H5.referenceEquals(type, "metric")) {
                        var valueField = ($t1 = H5.unbox(component)["value"], $t1 != null ? $t1 : "");
                        var format = ($t2 = H5.unbox(component)["format"], $t2 != null ? $t2 : "number");
                        return Nimblesite.Reporting.React.Components.MetricComponent.Render(title, valueField, format, dsResult, cssClass, cssStyle);
                    }

                    if (H5.referenceEquals(type, "Chart") || H5.referenceEquals(type, "chart")) {
                        var chartType = ($t3 = H5.unbox(component)["chartType"], $t3 != null ? $t3 : "bar");
                        var xAxis = H5.unbox(component)["xAxis"];
                        var yAxis = H5.unbox(component)["yAxis"];

                        if (H5.referenceEquals(chartType, "Bar") || H5.referenceEquals(chartType, "bar")) {
                            return Nimblesite.Reporting.React.Components.BarChartComponent.Render(title, xAxis, yAxis, dsResult, cssClass, cssStyle);
                        }

                        return Nimblesite.Reporting.React.Components.BarChartComponent.Render(title, xAxis, yAxis, dsResult, cssClass, cssStyle);
                    }

                    if (H5.referenceEquals(type, "Table") || H5.referenceEquals(type, "table")) {
                        var columns = H5.unbox(component)["columns"];
                        var pageSize = H5.unbox(component)["pageSize"];
                        return Nimblesite.Reporting.React.Components.TableComponent.Render(title, columns, dsResult, pageSize > 0 ? pageSize : 50, cssClass, cssStyle);
                    }

                    if (H5.referenceEquals(type, "Text") || H5.referenceEquals(type, "text")) {
                        var content = ($t4 = H5.unbox(component)["content"], $t4 != null ? $t4 : "");
                        var style = ($t5 = H5.unbox(component)["style"], $t5 != null ? $t5 : "body");
                        return Nimblesite.Reporting.React.Components.TextComponent.Render(content, style, cssClass, cssStyle);
                    }

                    return Nimblesite.Reporting.React.Core.Elements.Div("report-unknown-component", void 0, void 0, void 0, System.Array.init([Nimblesite.Reporting.React.Core.Elements.Text("Unknown component type: " + (type || ""))], Object));
                }
            }
        }
    });

    /**
     * Renders a data table from report data.
     *
     * @static
     * @abstract
     * @public
     * @class Nimblesite.Reporting.React.Components.TableComponent
     */
    H5.define("Nimblesite.Reporting.React.Components.TableComponent", {
        statics: {
            methods: {
                /**
                 * Renders a table with headers and rows from a data source result.
                 *
                 * @static
                 * @public
                 * @this Nimblesite.Reporting.React.Components.TableComponent
                 * @memberof Nimblesite.Reporting.React.Components.TableComponent
                 * @param   {string}           title               
                 * @param   {System.Object}    columnDefs          
                 * @param   {System.Object}    dataSourceResult    
                 * @param   {number}           pageSize            
                 * @param   {string}           cssClass            
                 * @param   {System.Object}    cssStyle
                 * @return  {Object}
                 */
                Render: function (title, columnDefs, dataSourceResult, pageSize, cssClass, cssStyle) {
                    var $t;
                    if (cssClass === void 0) { cssClass = null; }
                    if (cssStyle === void 0) { cssStyle = null; }
                    var columns = columnDefs || System.Array.init(0, null, System.Object);
                    var allColumnNames = H5.unbox(dataSourceResult)["columnNames"];
                    var rows = H5.unbox(dataSourceResult)["rows"] || System.Array.init(0, null, System.Object);

                    var headerCells = System.Array.init(columns.length, null, Object);
                    for (var i = 0; i < columns.length; i = (i + 1) | 0) {
                        var header = H5.unbox(columns[System.Array.index(i, columns)])["header"];
                        headerCells[System.Array.index(i, headerCells)] = Nimblesite.Reporting.React.Core.Elements.Th("report-table-th", System.Array.init([Nimblesite.Reporting.React.Core.Elements.Text(($t = header, $t != null ? $t : ""))], Object));
                    }

                    var headerRow = Nimblesite.Reporting.React.Core.Elements.Tr("report-table-header-row", headerCells);

                    var displayCount = pageSize > 0 && rows.length > pageSize ? pageSize : rows.length;
                    var dataRows = System.Array.init(displayCount, null, Object);

                    for (var r = 0; r < displayCount; r = (r + 1) | 0) {
                        var row = rows[r];
                        var cells = System.Array.init(columns.length, null, Object);

                        for (var c = 0; c < columns.length; c = (c + 1) | 0) {
                            var field = H5.unbox(columns[System.Array.index(c, columns)])["field"];
                            var colIndex = Nimblesite.Reporting.React.Components.TableComponent.FindColumnIndex(allColumnNames, field);
                            var cellValue = colIndex >= 0 ? row[colIndex] : null;
                            cells[System.Array.index(c, cells)] = Nimblesite.Reporting.React.Core.Elements.Td("report-table-td", System.Array.init([Nimblesite.Reporting.React.Core.Elements.Text(cellValue != null ? H5.toString(cellValue) : "")], Object));
                        }

                        dataRows[System.Array.index(r, dataRows)] = Nimblesite.Reporting.React.Core.Elements.Tr("report-table-row", cells);
                    }

                    var containerClassName = cssClass != null ? "report-table-container " + (cssClass || "") : "report-table-container";

                    return Nimblesite.Reporting.React.Core.Elements.Div(containerClassName, void 0, cssStyle, void 0, System.Array.init([Nimblesite.Reporting.React.Core.Elements.H(3, "report-component-title", System.Array.init([Nimblesite.Reporting.React.Core.Elements.Text(title)], Object)), Nimblesite.Reporting.React.Core.Elements.Table("report-table", System.Array.init([Nimblesite.Reporting.React.Core.Elements.THead(System.Array.init([headerRow], Object)), Nimblesite.Reporting.React.Core.Elements.TBody(dataRows)], Object)), rows.length > displayCount ? Nimblesite.Reporting.React.Core.Elements.P("report-table-overflow", System.Array.init([Nimblesite.Reporting.React.Core.Elements.Text("Showing " + displayCount + " of " + rows.length + " rows")], Object)) : Nimblesite.Reporting.React.Core.Elements.Fragment()], Object));
                },
                FindColumnIndex: function (columnNames, field) {
                    if (columnNames == null || field == null) {
                        return -1;
                    }
                    for (var i = 0; i < columnNames.length; i = (i + 1) | 0) {
                        if (H5.referenceEquals(columnNames[System.Array.index(i, columnNames)], field)) {
                            return i;
                        }
                    }
                    return -1;
                }
            }
        }
    });

    /**
     * Renders a text block (heading, body, or caption).
     *
     * @static
     * @abstract
     * @public
     * @class Nimblesite.Reporting.React.Components.TextComponent
     */
    H5.define("Nimblesite.Reporting.React.Components.TextComponent", {
        statics: {
            methods: {
                /**
                 * Renders a text component with the given style.
                 *
                 * @static
                 * @public
                 * @this Nimblesite.Reporting.React.Components.TextComponent
                 * @memberof Nimblesite.Reporting.React.Components.TextComponent
                 * @param   {string}           content     
                 * @param   {string}           style       
                 * @param   {string}           cssClass    
                 * @param   {System.Object}    cssStyle
                 * @return  {Object}
                 */
                Render: function (content, style, cssClass, cssStyle) {
                    var $t;
                    if (cssClass === void 0) { cssClass = null; }
                    if (cssStyle === void 0) { cssStyle = null; }
                    var baseClassName;
                    switch (style) {
                        case "heading": 
                            baseClassName = "report-text-heading";
                            break;
                        case "caption": 
                            baseClassName = "report-text-caption";
                            break;
                        default: 
                            baseClassName = "report-text-body";
                            break;
                    }
                    var className = cssClass != null ? (baseClassName || "") + " " + (cssClass || "") : baseClassName;

                    return Nimblesite.Reporting.React.Core.Elements.Div(className, void 0, cssStyle, void 0, System.Array.init([Nimblesite.Reporting.React.Core.Elements.Text(($t = content, $t != null ? $t : ""))], Object));
                }
            }
        }
    });

    /** @namespace System */

    /**
     * @memberof System
     * @callback System.Action
     * @return  {void}
     */

    /** @namespace Nimblesite.Reporting.React.Core */

    /**
     * HTML element factory methods for React.
     *
     * @static
     * @abstract
     * @public
     * @class Nimblesite.Reporting.React.Core.Elements
     */
    H5.define("Nimblesite.Reporting.React.Core.Elements", {
        statics: {
            methods: {
                /**
                 * Creates a div element.
                 *
                 * @static
                 * @public
                 * @this Nimblesite.Reporting.React.Core.Elements
                 * @memberof Nimblesite.Reporting.React.Core.Elements
                 * @param   {string}            className    
                 * @param   {string}            id           
                 * @param   {System.Object}     style        
                 * @param   {System.Action}     onClick      
                 * @param   {Array.<Object>}    children
                 * @return  {Object}
                 */
                Div: function (className, id, style, onClick, children) {
                    if (className === void 0) { className = null; }
                    if (id === void 0) { id = null; }
                    if (style === void 0) { style = null; }
                    if (onClick === void 0) { onClick = null; }
                    if (children === void 0) { children = []; }
                    return Nimblesite.Reporting.React.Core.Elements.CreateElement("div", className, id, style, onClick, children);
                },
                /**
                 * Creates a span element.
                 *
                 * @static
                 * @public
                 * @this Nimblesite.Reporting.React.Core.Elements
                 * @memberof Nimblesite.Reporting.React.Core.Elements
                 * @param   {string}            className    
                 * @param   {Array.<Object>}    children
                 * @return  {Object}
                 */
                Span: function (className, children) {
                    if (className === void 0) { className = null; }
                    if (children === void 0) { children = []; }
                    return Nimblesite.Reporting.React.Core.Elements.CreateElement("span", className, null, null, null, children);
                },
                /**
                 * Creates a paragraph element.
                 *
                 * @static
                 * @public
                 * @this Nimblesite.Reporting.React.Core.Elements
                 * @memberof Nimblesite.Reporting.React.Core.Elements
                 * @param   {string}            className    
                 * @param   {Array.<Object>}    children
                 * @return  {Object}
                 */
                P: function (className, children) {
                    if (className === void 0) { className = null; }
                    if (children === void 0) { children = []; }
                    return Nimblesite.Reporting.React.Core.Elements.CreateElement("p", className, null, null, null, children);
                },
                /**
                 * Creates a heading element (h1-h6).
                 *
                 * @static
                 * @public
                 * @this Nimblesite.Reporting.React.Core.Elements
                 * @memberof Nimblesite.Reporting.React.Core.Elements
                 * @param   {number}            level        
                 * @param   {string}            className    
                 * @param   {Array.<Object>}    children
                 * @return  {Object}
                 */
                H: function (level, className, children) {
                    if (className === void 0) { className = null; }
                    if (children === void 0) { children = []; }
                    return Nimblesite.Reporting.React.Core.Elements.CreateElement("h" + level, className, null, null, null, children);
                },
                /**
                 * Creates a button element.
                 *
                 * @static
                 * @public
                 * @this Nimblesite.Reporting.React.Core.Elements
                 * @memberof Nimblesite.Reporting.React.Core.Elements
                 * @param   {string}            className    
                 * @param   {System.Action}     onClick      
                 * @param   {boolean}           disabled     
                 * @param   {Array.<Object>}    children
                 * @return  {Object}
                 */
                Button: function (className, onClick, disabled, children) {
                    if (className === void 0) { className = null; }
                    if (onClick === void 0) { onClick = null; }
                    if (disabled === void 0) { disabled = false; }
                    if (children === void 0) { children = []; }
                    var clickHandler = null;
                    if (!H5.staticEquals(onClick, null)) {
                        clickHandler = function (e) {
                            e.stopPropagation();
                            onClick();
                        };
                    }
                    var props = { className: className, onClick: clickHandler, disabled: disabled, type: "button" };
                    return React.createElement("button", props, children);
                },
                /**
                 * Creates an input element.
                 *
                 * @static
                 * @public
                 * @this Nimblesite.Reporting.React.Core.Elements
                 * @memberof Nimblesite.Reporting.React.Core.Elements
                 * @param   {string}           className      
                 * @param   {string}           type           
                 * @param   {string}           value          
                 * @param   {string}           placeholder    
                 * @param   {System.Action}    onChange
                 * @return  {Object}
                 */
                Input: function (className, type, value, placeholder, onChange) {
                    if (className === void 0) { className = null; }
                    if (type === void 0) { type = "text"; }
                    if (value === void 0) { value = null; }
                    if (placeholder === void 0) { placeholder = null; }
                    if (onChange === void 0) { onChange = null; }
                    var changeHandler = null;
                    if (!H5.staticEquals(onChange, null)) {
                        changeHandler = function (e) {
                            onChange(H5.unbox(H5.unbox(e)["target"])["value"]);
                        };
                    }
                    var props = { className: className, type: type, value: value, placeholder: placeholder, onChange: changeHandler };
                    return React.createElement("input", props);
                },
                /**
                 * Creates a text node.
                 *
                 * @static
                 * @public
                 * @this Nimblesite.Reporting.React.Core.Elements
                 * @memberof Nimblesite.Reporting.React.Core.Elements
                 * @param   {string}    content
                 * @return  {Object}
                 */
                Text: function (content) {
                    return React.createElement("span", null, content);
                },
                /**
                 * Creates a table element.
                 *
                 * @static
                 * @public
                 * @this Nimblesite.Reporting.React.Core.Elements
                 * @memberof Nimblesite.Reporting.React.Core.Elements
                 * @param   {string}            className    
                 * @param   {Array.<Object>}    children
                 * @return  {Object}
                 */
                Table: function (className, children) {
                    if (className === void 0) { className = null; }
                    if (children === void 0) { children = []; }
                    return Nimblesite.Reporting.React.Core.Elements.CreateElement("table", className, null, null, null, children);
                },
                /**
                 * Creates a thead element.
                 *
                 * @static
                 * @public
                 * @this Nimblesite.Reporting.React.Core.Elements
                 * @memberof Nimblesite.Reporting.React.Core.Elements
                 * @param   {Array.<Object>}    children
                 * @return  {Object}
                 */
                THead: function (children) {
                    if (children === void 0) { children = []; }
                    return React.createElement("thead", null, children);
                },
                /**
                 * Creates a tbody element.
                 *
                 * @static
                 * @public
                 * @this Nimblesite.Reporting.React.Core.Elements
                 * @memberof Nimblesite.Reporting.React.Core.Elements
                 * @param   {Array.<Object>}    children
                 * @return  {Object}
                 */
                TBody: function (children) {
                    if (children === void 0) { children = []; }
                    return React.createElement("tbody", null, children);
                },
                /**
                 * Creates a tr element.
                 *
                 * @static
                 * @public
                 * @this Nimblesite.Reporting.React.Core.Elements
                 * @memberof Nimblesite.Reporting.React.Core.Elements
                 * @param   {string}            className    
                 * @param   {Array.<Object>}    children
                 * @return  {Object}
                 */
                Tr: function (className, children) {
                    if (className === void 0) { className = null; }
                    if (children === void 0) { children = []; }
                    var props = { className: className };
                    return React.createElement("tr", props, children);
                },
                /**
                 * Creates a th element.
                 *
                 * @static
                 * @public
                 * @this Nimblesite.Reporting.React.Core.Elements
                 * @memberof Nimblesite.Reporting.React.Core.Elements
                 * @param   {string}            className    
                 * @param   {Array.<Object>}    children
                 * @return  {Object}
                 */
                Th: function (className, children) {
                    if (className === void 0) { className = null; }
                    if (children === void 0) { children = []; }
                    return Nimblesite.Reporting.React.Core.Elements.CreateElement("th", className, null, null, null, children);
                },
                /**
                 * Creates a td element.
                 *
                 * @static
                 * @public
                 * @this Nimblesite.Reporting.React.Core.Elements
                 * @memberof Nimblesite.Reporting.React.Core.Elements
                 * @param   {string}            className    
                 * @param   {Array.<Object>}    children
                 * @return  {Object}
                 */
                Td: function (className, children) {
                    if (className === void 0) { className = null; }
                    if (children === void 0) { children = []; }
                    return Nimblesite.Reporting.React.Core.Elements.CreateElement("td", className, null, null, null, children);
                },
                /**
                 * Creates a canvas element for charts.
                 *
                 * @static
                 * @public
                 * @this Nimblesite.Reporting.React.Core.Elements
                 * @memberof Nimblesite.Reporting.React.Core.Elements
                 * @param   {string}    id           
                 * @param   {string}    className    
                 * @param   {number}    width        
                 * @param   {number}    height
                 * @return  {Object}
                 */
                Canvas: function (id, className, width, height) {
                    if (id === void 0) { id = null; }
                    if (className === void 0) { className = null; }
                    if (width === void 0) { width = 0; }
                    if (height === void 0) { height = 0; }
                    var props = { id: id, className: className, width: width > 0 ? H5.box(width, System.Int32) : null, height: height > 0 ? H5.box(height, System.Int32) : null };
                    return React.createElement("canvas", props);
                },
                /**
                 * Creates an SVG element.
                 *
                 * @static
                 * @public
                 * @this Nimblesite.Reporting.React.Core.Elements
                 * @memberof Nimblesite.Reporting.React.Core.Elements
                 * @param   {string}            className    
                 * @param   {number}            width        
                 * @param   {number}            height       
                 * @param   {string}            viewBox      
                 * @param   {Array.<Object>}    children
                 * @return  {Object}
                 */
                Svg: function (className, width, height, viewBox, children) {
                    if (className === void 0) { className = null; }
                    if (width === void 0) { width = 0; }
                    if (height === void 0) { height = 0; }
                    if (viewBox === void 0) { viewBox = null; }
                    if (children === void 0) { children = []; }
                    var props = { className: className, width: width > 0 ? H5.box(width, System.Int32) : null, height: height > 0 ? H5.box(height, System.Int32) : null, viewBox: viewBox };
                    return React.createElement("svg", props, children);
                },
                /**
                 * Creates an SVG rect element.
                 *
                 * @static
                 * @public
                 * @this Nimblesite.Reporting.React.Core.Elements
                 * @memberof Nimblesite.Reporting.React.Core.Elements
                 * @param   {number}    x            
                 * @param   {number}    y            
                 * @param   {number}    width        
                 * @param   {number}    height       
                 * @param   {string}    fill         
                 * @param   {string}    className
                 * @return  {Object}
                 */
                Rect: function (x, y, width, height, fill, className) {
                    if (fill === void 0) { fill = null; }
                    if (className === void 0) { className = null; }
                    var props = { x: x, y: y, width: width, height: height, fill: fill, className: className };
                    return React.createElement("rect", props);
                },
                /**
                 * Creates an SVG text element.
                 *
                 * @static
                 * @public
                 * @this Nimblesite.Reporting.React.Core.Elements
                 * @memberof Nimblesite.Reporting.React.Core.Elements
                 * @param   {number}    x             
                 * @param   {number}    y             
                 * @param   {string}    content       
                 * @param   {string}    fill          
                 * @param   {string}    textAnchor    
                 * @param   {string}    fontSize      
                 * @param   {string}    transform
                 * @return  {Object}
                 */
                SvgText: function (x, y, content, fill, textAnchor, fontSize, transform) {
                    if (fill === void 0) { fill = null; }
                    if (textAnchor === void 0) { textAnchor = null; }
                    if (fontSize === void 0) { fontSize = null; }
                    if (transform === void 0) { transform = null; }
                    var props = { x: x, y: y, fill: fill, textAnchor: textAnchor, fontSize: fontSize, transform: transform };
                    return React.createElement("text", props, content);
                },
                /**
                 * Creates an SVG line element.
                 *
                 * @static
                 * @public
                 * @this Nimblesite.Reporting.React.Core.Elements
                 * @memberof Nimblesite.Reporting.React.Core.Elements
                 * @param   {number}    x1             
                 * @param   {number}    y1             
                 * @param   {number}    x2             
                 * @param   {number}    y2             
                 * @param   {string}    stroke         
                 * @param   {number}    strokeWidth
                 * @return  {Object}
                 */
                Line: function (x1, y1, x2, y2, stroke, strokeWidth) {
                    if (stroke === void 0) { stroke = null; }
                    if (strokeWidth === void 0) { strokeWidth = 1; }
                    var props = { x1: x1, y1: y1, x2: x2, y2: y2, stroke: stroke, strokeWidth: strokeWidth };
                    return React.createElement("line", props);
                },
                /**
                 * Creates a React Fragment.
                 *
                 * @static
                 * @public
                 * @this Nimblesite.Reporting.React.Core.Elements
                 * @memberof Nimblesite.Reporting.React.Core.Elements
                 * @param   {Array.<Object>}    children
                 * @return  {Object}
                 */
                Fragment: function (children) {
                    if (children === void 0) { children = []; }
                    return React.createElement(H5.unbox(React["Fragment"]), null, children);
                },
                CreateElement: function (tag, className, id, style, onClick, children) {
                    var clickHandler = null;
                    if (!H5.staticEquals(onClick, null)) {
                        clickHandler = function (_) {
                            onClick();
                        };
                    }
                    var props = { className: className, id: id, style: style, onClick: clickHandler };
                    return React.createElement(tag, props, children);
                }
            }
        }
    });

    /**
     * React hooks for H5.
     *
     * @static
     * @abstract
     * @public
     * @class Nimblesite.Reporting.React.Core.Hooks
     */
    H5.define("Nimblesite.Reporting.React.Core.Hooks", {
        statics: {
            methods: {
                /**
                 * React useState hook.
                 *
                 * @static
                 * @public
                 * @this Nimblesite.Reporting.React.Core.Hooks
                 * @memberof Nimblesite.Reporting.React.Core.Hooks
                 * @param   {Function}                                         T               
                 * @param   {T}                                                initialValue
                 * @return  {Nimblesite.Reporting.React.Core.StateResult$1}
                 */
                UseState: function (T, initialValue) {
                    var $t;
                    var result = React.useState(initialValue);
                    return ($t = new (Nimblesite.Reporting.React.Core.StateResult$1(T))(), $t.State = result[0], $t.SetState = result[1], $t);
                },
                /**
                 * React useEffect hook.
                 *
                 * @static
                 * @public
                 * @this Nimblesite.Reporting.React.Core.Hooks
                 * @memberof Nimblesite.Reporting.React.Core.Hooks
                 * @param   {System.Action}            effect    
                 * @param   {Array.<System.Object>}    deps
                 * @return  {void}
                 */
                UseEffect: function (effect, deps) {
                    if (deps === void 0) { deps = null; }
                    if (deps != null) {
                        React.useEffect(effect, H5.unbox(deps));
                    } else {
                        React.useEffect(effect);
                    }
                }
            }
        }
    });

    /**
     * Core React interop types and functions for H5.
     *
     * @static
     * @abstract
     * @public
     * @class Nimblesite.Reporting.React.Core.ReactInterop
     */
    H5.define("Nimblesite.Reporting.React.Core.ReactInterop", {
        statics: {
            methods: {
                /**
                 * Creates a React element using React.createElement.
                 *
                 * @static
                 * @public
                 * @this Nimblesite.Reporting.React.Core.ReactInterop
                 * @memberof Nimblesite.Reporting.React.Core.ReactInterop
                 * @param   {string}                   type        
                 * @param   {System.Object}            props       
                 * @param   {Array.<System.Object>}    children
                 * @return  {Object}
                 */
                CreateElement: function (type, props, children) {
                    if (props === void 0) { props = null; }
                    if (children === void 0) { children = []; }
                    return React.createElement(type, H5.unbox(props), H5.unbox(children));
                },
                /**
                 * Creates the React root and renders the application.
                 *
                 * @static
                 * @public
                 * @this Nimblesite.Reporting.React.Core.ReactInterop
                 * @memberof Nimblesite.Reporting.React.Core.ReactInterop
                 * @param   {Object}    element        
                 * @param   {string}    containerId
                 * @return  {void}
                 */
                RenderApp: function (element, containerId) {
                    if (containerId === void 0) { containerId = "root"; }
                    var container = document.getElementById(containerId);
                    var root = ReactDOM.createRoot(container);
                    root.Render(element);
                }
            }
        }
    });

    /**
     * State result from useState hook.
     *
     * @public
     * @class Nimblesite.Reporting.React.Core.StateResult$1
     */
    H5.define("Nimblesite.Reporting.React.Core.StateResult$1", function (T) { return {
        fields: {
            /**
             * Current state value.
             *
             * @instance
             * @public
             * @memberof Nimblesite.Reporting.React.Core.StateResult$1
             * @function State
             * @type T
             */
            State: H5.getDefaultValue(T),
            /**
             * State setter function.
             *
             * @instance
             * @public
             * @memberof Nimblesite.Reporting.React.Core.StateResult$1
             * @function SetState
             * @type System.Action
             */
            SetState: null
        }
    }; });

    /**
     * Entry point for the report viewer React application.
     *
     * @static
     * @abstract
     * @public
     * @class Nimblesite.Reporting.React.Program
     */
    H5.define("Nimblesite.Reporting.React.Program", {
        /**
         * Main entry point - called when H5 script loads.
         *
         * @static
         * @public
         * @this Nimblesite.Reporting.React.Program
         * @memberof Nimblesite.Reporting.React.Program
         * @return  {void}
         */
        main: function Main () {
            var apiBaseUrl = Nimblesite.Reporting.React.Program.GetConfigValue("apiBaseUrl", "");
            var reportId = Nimblesite.Reporting.React.Program.GetConfigValue("reportId", "");

            Nimblesite.Reporting.React.Api.ReportApiClient.Configure(apiBaseUrl);

            Nimblesite.Reporting.React.Program.Log("Report Viewer starting...");
            Nimblesite.Reporting.React.Program.Log("API Base URL: " + (apiBaseUrl || ""));

            Nimblesite.Reporting.React.Program.HideLoadingScreen();

            Nimblesite.Reporting.React.Core.ReactInterop.RenderApp(Nimblesite.Reporting.React.Program.RenderApp(apiBaseUrl, reportId));

            Nimblesite.Reporting.React.Program.Log("Report Viewer initialized.");
        },
        statics: {
            methods: {
                RenderApp: function (apiBaseUrl, initialReportId) {
                    var $t;
                    var stateResult = Nimblesite.Reporting.React.Core.Hooks.UseState(Nimblesite.Reporting.React.AppState, ($t = new Nimblesite.Reporting.React.AppState(), $t.ReportDef = null, $t.ExecutionResult = null, $t.Loading = false, $t.Error = null, $t.ReportList = null, $t));

                    var state = stateResult.State;
                    var setState = stateResult.SetState;

                    Nimblesite.Reporting.React.Core.Hooks.UseEffect(function () {
                        if (System.String.isNullOrEmpty(apiBaseUrl)) {
                            return;
                        }

                        Nimblesite.Reporting.React.Program.LoadReportList(state, setState);

                        if (!System.String.isNullOrEmpty(initialReportId)) {
                            Nimblesite.Reporting.React.Program.LoadAndExecuteReport(initialReportId, state, setState);
                        }
                    }, System.Array.init([], System.Object));

                    if (!System.String.isNullOrEmpty(state.Error)) {
                        return Nimblesite.Reporting.React.Core.Elements.Div("report-viewer-error", void 0, void 0, void 0, System.Array.init([Nimblesite.Reporting.React.Core.Elements.H(2, void 0, System.Array.init([Nimblesite.Reporting.React.Core.Elements.Text("Error")], Object)), Nimblesite.Reporting.React.Core.Elements.P(void 0, System.Array.init([Nimblesite.Reporting.React.Core.Elements.Text(state.Error)], Object))], Object));
                    }

                    if (state.Loading) {
                        return Nimblesite.Reporting.React.Core.Elements.Div("report-viewer-loading", void 0, void 0, void 0, System.Array.init([Nimblesite.Reporting.React.Core.Elements.Text("Loading report...")], Object));
                    }

                    if (state.ReportDef != null && state.ExecutionResult != null) {
                        return Nimblesite.Reporting.React.Components.ReportRenderer.Render(state.ReportDef, state.ExecutionResult);
                    }

                    return Nimblesite.Reporting.React.Program.RenderReportList(state, setState);
                },
                RenderReportList: function (state, setState) {
                    var $t;
                    var reports = state.ReportList || System.Array.init(0, null, System.Object);

                    if (reports.length === 0) {
                        return Nimblesite.Reporting.React.Core.Elements.Div("report-viewer-empty", void 0, void 0, void 0, System.Array.init([Nimblesite.Reporting.React.Core.Elements.H(2, void 0, System.Array.init([Nimblesite.Reporting.React.Core.Elements.Text("Report Viewer")], Object)), Nimblesite.Reporting.React.Core.Elements.P(void 0, System.Array.init([Nimblesite.Reporting.React.Core.Elements.Text("No reports available. Configure the API base URL and add report definitions.")], Object))], Object));
                    }

                    var reportItems = System.Array.init(reports.length, null, Object);
                    for (var i = 0; i < reports.length; i = (i + 1) | 0) {
                        var report = reports[System.Array.index(i, reports)];
                        var id = H5.unbox(report)["id"];
                        var title = ($t = H5.unbox(report)["title"], $t != null ? $t : id);
                        var capturedId = { v : id };

                        reportItems[System.Array.index(i, reportItems)] = Nimblesite.Reporting.React.Core.Elements.Div("report-list-item", void 0, void 0, (function ($me, capturedId) {
                            return function () {
                                Nimblesite.Reporting.React.Program.LoadAndExecuteReport(capturedId.v, state, setState);
                            };
                        })(this, capturedId), System.Array.init([Nimblesite.Reporting.React.Core.Elements.H(3, void 0, System.Array.init([Nimblesite.Reporting.React.Core.Elements.Text(title)], Object))], Object));
                    }

                    return Nimblesite.Reporting.React.Core.Elements.Div("report-viewer-list", void 0, void 0, void 0, System.Array.init([Nimblesite.Reporting.React.Core.Elements.H(2, void 0, System.Array.init([Nimblesite.Reporting.React.Core.Elements.Text("Available Reports")], Object)), Nimblesite.Reporting.React.Core.Elements.Div("report-list", void 0, void 0, void 0, reportItems)], Object));
                },
                LoadReportList: function (currentState, setState) {
                    (async () => {
                        {
                            var $t;
                            try {
                                var reports = (await H5.toPromise(Nimblesite.Reporting.React.Api.ReportApiClient.GetReportsAsync()));
                                setState(($t = new Nimblesite.Reporting.React.AppState(), $t.ReportDef = currentState.ReportDef, $t.ExecutionResult = currentState.ExecutionResult, $t.Loading = false, $t.Error = null, $t.ReportList = reports, $t));
                            } catch (ex) {
                                ex = System.Exception.create(ex);
                                Nimblesite.Reporting.React.Program.Log("Failed to load report list: " + (ex.Message || ""));
                            }
                        }})()
                },
                LoadAndExecuteReport: function (reportId, currentState, setState) {
                    (async () => {
                        {
                            var $t;
                            setState(($t = new Nimblesite.Reporting.React.AppState(), $t.ReportDef = null, $t.ExecutionResult = null, $t.Loading = true, $t.Error = null, $t.ReportList = currentState.ReportList, $t));

                            try {
                                var reportDef = (await H5.toPromise(Nimblesite.Reporting.React.Api.ReportApiClient.GetReportAsync(reportId)));
                                var parameters = Object.create(H5.unbox(null));
                                var result = (await H5.toPromise(Nimblesite.Reporting.React.Api.ReportApiClient.ExecuteReportAsync(reportId, parameters)));

                                setState(($t = new Nimblesite.Reporting.React.AppState(), $t.ReportDef = reportDef, $t.ExecutionResult = result, $t.Loading = false, $t.Error = null, $t.ReportList = currentState.ReportList, $t));
                            } catch (ex) {
                                ex = System.Exception.create(ex);
                                setState(($t = new Nimblesite.Reporting.React.AppState(), $t.ReportDef = null, $t.ExecutionResult = null, $t.Loading = false, $t.Error = "Failed to load report: " + (ex.Message || ""), $t.ReportList = currentState.ReportList, $t));
                            }
                        }})()
                },
                GetConfigValue: function (key, defaultValue) {
                    var windowConfig = window["reportConfig"];
                    if (windowConfig != null) {
                        var value = H5.unbox(windowConfig)[key];
                        if (!System.String.isNullOrEmpty(value)) {
                            return value;
                        }
                    }
                    return defaultValue;
                },
                HideLoadingScreen: function () {
                    var loadingScreen = document.getElementById("loading-screen");
                    if (loadingScreen != null) {
                        loadingScreen.classList.add('hidden');
                    }
                },
                Log: function (message) {
                    console.log("[ReportViewer] " + (message || ""));
                }
            }
        }
    });
});
