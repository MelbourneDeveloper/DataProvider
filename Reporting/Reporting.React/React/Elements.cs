using System;
using H5;

namespace Reporting.React.Core
{
    /// <summary>
    /// HTML element factory methods for React.
    /// </summary>
    public static class Elements
    {
        /// <summary>
        /// Creates a div element.
        /// </summary>
        public static ReactElement Div(
            string className = null,
            string id = null,
            object style = null,
            Action onClick = null,
            params ReactElement[] children
        ) => CreateElement("div", className, id, style, onClick, children);

        /// <summary>
        /// Creates a span element.
        /// </summary>
        public static ReactElement Span(
            string className = null,
            params ReactElement[] children
        ) => CreateElement("span", className, null, null, null, children);

        /// <summary>
        /// Creates a paragraph element.
        /// </summary>
        public static ReactElement P(
            string className = null,
            params ReactElement[] children
        ) => CreateElement("p", className, null, null, null, children);

        /// <summary>
        /// Creates a heading element (h1-h6).
        /// </summary>
        public static ReactElement H(
            int level,
            string className = null,
            params ReactElement[] children
        ) => CreateElement("h" + level, className, null, null, null, children);

        /// <summary>
        /// Creates a button element.
        /// </summary>
        public static ReactElement Button(
            string className = null,
            Action onClick = null,
            bool disabled = false,
            params ReactElement[] children
        )
        {
            Action<object> clickHandler = null;
            if (onClick != null)
            {
                clickHandler = e =>
                {
                    Script.Write("e.stopPropagation()");
                    onClick();
                };
            }
            var props = new { className = className, onClick = clickHandler, disabled = disabled, type = "button" };
            return Script.Call<ReactElement>("React.createElement", "button", props, children);
        }

        /// <summary>
        /// Creates an input element.
        /// </summary>
        public static ReactElement Input(
            string className = null,
            string type = "text",
            string value = null,
            string placeholder = null,
            Action<string> onChange = null
        )
        {
            Action<object> changeHandler = null;
            if (onChange != null)
            {
                changeHandler = e =>
                    onChange(Script.Get<string>(Script.Get<object>(e, "target"), "value"));
            }
            var props = new { className = className, type = type, value = value, placeholder = placeholder, onChange = changeHandler };
            return Script.Call<ReactElement>("React.createElement", "input", props);
        }

        /// <summary>
        /// Creates a text node.
        /// </summary>
        public static ReactElement Text(string content) =>
            Script.Call<ReactElement>("React.createElement", "span", null, content);

        /// <summary>
        /// Creates a table element.
        /// </summary>
        public static ReactElement Table(string className = null, params ReactElement[] children) =>
            CreateElement("table", className, null, null, null, children);

        /// <summary>
        /// Creates a thead element.
        /// </summary>
        public static ReactElement THead(params ReactElement[] children) =>
            Script.Call<ReactElement>("React.createElement", "thead", null, children);

        /// <summary>
        /// Creates a tbody element.
        /// </summary>
        public static ReactElement TBody(params ReactElement[] children) =>
            Script.Call<ReactElement>("React.createElement", "tbody", null, children);

        /// <summary>
        /// Creates a tr element.
        /// </summary>
        public static ReactElement Tr(string className = null, params ReactElement[] children)
        {
            var props = new { className = className };
            return Script.Call<ReactElement>("React.createElement", "tr", props, children);
        }

        /// <summary>
        /// Creates a th element.
        /// </summary>
        public static ReactElement Th(string className = null, params ReactElement[] children) =>
            CreateElement("th", className, null, null, null, children);

        /// <summary>
        /// Creates a td element.
        /// </summary>
        public static ReactElement Td(string className = null, params ReactElement[] children) =>
            CreateElement("td", className, null, null, null, children);

        /// <summary>
        /// Creates a canvas element for charts.
        /// </summary>
        public static ReactElement Canvas(
            string id = null,
            string className = null,
            int width = 0,
            int height = 0
        )
        {
            var props = new
            {
                id = id,
                className = className,
                width = width > 0 ? (object)width : null,
                height = height > 0 ? (object)height : null,
            };
            return Script.Call<ReactElement>("React.createElement", "canvas", props);
        }

        /// <summary>
        /// Creates an SVG element.
        /// </summary>
        public static ReactElement Svg(
            string className = null,
            int width = 0,
            int height = 0,
            string viewBox = null,
            params ReactElement[] children
        )
        {
            var props = new
            {
                className = className,
                width = width > 0 ? (object)width : null,
                height = height > 0 ? (object)height : null,
                viewBox = viewBox,
            };
            return Script.Call<ReactElement>("React.createElement", "svg", props, children);
        }

        /// <summary>
        /// Creates an SVG rect element.
        /// </summary>
        public static ReactElement Rect(
            double x, double y, double width, double height,
            string fill = null, string className = null
        )
        {
            var props = new { x = x, y = y, width = width, height = height, fill = fill, className = className };
            return Script.Call<ReactElement>("React.createElement", "rect", props);
        }

        /// <summary>
        /// Creates an SVG text element.
        /// </summary>
        public static ReactElement SvgText(
            double x, double y, string content,
            string fill = null, string textAnchor = null,
            string fontSize = null, string transform = null
        )
        {
            var props = new { x = x, y = y, fill = fill, textAnchor = textAnchor, fontSize = fontSize, transform = transform };
            return Script.Call<ReactElement>("React.createElement", "text", props, content);
        }

        /// <summary>
        /// Creates an SVG line element.
        /// </summary>
        public static ReactElement Line(
            double x1, double y1, double x2, double y2,
            string stroke = null, int strokeWidth = 1
        )
        {
            var props = new { x1 = x1, y1 = y1, x2 = x2, y2 = y2, stroke = stroke, strokeWidth = strokeWidth };
            return Script.Call<ReactElement>("React.createElement", "line", props);
        }

        /// <summary>
        /// Creates a React Fragment.
        /// </summary>
        public static ReactElement Fragment(params ReactElement[] children) =>
            Script.Call<ReactElement>(
                "React.createElement",
                Script.Get<object>("React", "Fragment"),
                null,
                children
            );

        private static ReactElement CreateElement(
            string tag, string className, string id,
            object style, Action onClick, ReactElement[] children
        )
        {
            Action<object> clickHandler = null;
            if (onClick != null) { clickHandler = _ => onClick(); }
            var props = new { className = className, id = id, style = style, onClick = clickHandler };
            return Script.Call<ReactElement>("React.createElement", tag, props, children);
        }
    }
}
