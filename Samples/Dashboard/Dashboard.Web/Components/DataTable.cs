namespace Dashboard.Components;

using Dashboard.React;
using static Dashboard.React.Elements;

/// <summary>
/// Data table component.
/// </summary>
public static class DataTable
{
    /// <summary>
    /// Column definition.
    /// </summary>
    public record Column(string Key, string Header, string? ClassName = null);

    /// <summary>
    /// Renders a data table.
    /// </summary>
    public static ReactElement Render<T>(
        Column[] columns,
        T[] data,
        Func<T, string> getKey,
        Func<T, string, ReactElement> renderCell,
        Action<T>? onRowClick = null
    )
    {
        return Div(
            className: "table-container",
            children: new[]
            {
                Table(
                    className: "table",
                    children: new[]
                    {
                        THead(
                            children: new[]
                            {
                                Tr(
                                    children: columns
                                        .Select(col =>
                                            Th(
                                                className: col.ClassName,
                                                children: new[] { Text(col.Header) }
                                            )
                                        )
                                        .ToArray()
                                ),
                            }
                        ),
                        TBody(
                            children: data.Select(row =>
                                    Tr(
                                        className: onRowClick != null ? "cursor-pointer" : null,
                                        onClick: onRowClick != null ? () => onRowClick(row) : null,
                                        children: columns
                                            .Select(col =>
                                                Td(
                                                    className: col.ClassName,
                                                    children: new[] { renderCell(row, col.Key) }
                                                )
                                            )
                                            .ToArray()
                                    )
                                )
                                .ToArray()
                        ),
                    }
                ),
            }
        );
    }

    /// <summary>
    /// Renders an empty state for the table.
    /// </summary>
    public static ReactElement RenderEmpty(string message = "No data available") =>
        Div(
            className: "empty-state",
            children: new[]
            {
                Div(className: "empty-state-icon", children: new[] { Icons.Clipboard() }),
                H(4, className: "empty-state-title", children: new[] { Text("No Results") }),
                P(className: "empty-state-description", children: new[] { Text(message) }),
            }
        );

    /// <summary>
    /// Renders a loading skeleton.
    /// </summary>
    public static ReactElement RenderLoading(int rows = 5, int columns = 4) =>
        Div(
            className: "table-container",
            children: new[]
            {
                Table(
                    className: "table",
                    children: new[]
                    {
                        THead(
                            children: new[]
                            {
                                Tr(
                                    children: Enumerable
                                        .Range(0, columns)
                                        .Select(_ =>
                                            Th(
                                                children: new[]
                                                {
                                                    Div(
                                                        className: "skeleton",
                                                        style: new
                                                        {
                                                            width = "80px",
                                                            height = "16px",
                                                        }
                                                    ),
                                                }
                                            )
                                        )
                                        .ToArray()
                                ),
                            }
                        ),
                        TBody(
                            children: Enumerable
                                .Range(0, rows)
                                .Select(_ =>
                                    Tr(
                                        children: Enumerable
                                            .Range(0, columns)
                                            .Select(_ =>
                                                Td(
                                                    children: new[]
                                                    {
                                                        Div(
                                                            className: "skeleton",
                                                            style: new
                                                            {
                                                                width = "100%",
                                                                height = "16px",
                                                            }
                                                        ),
                                                    }
                                                )
                                            )
                                            .ToArray()
                                    )
                                )
                                .ToArray()
                        ),
                    }
                ),
            }
        );
}
