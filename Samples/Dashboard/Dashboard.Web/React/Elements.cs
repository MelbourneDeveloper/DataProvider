using System;
using H5;

namespace Dashboard.React
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
            string id = null,
            object style = null,
            params ReactElement[] children
        ) => CreateElement("span", className, id, style, null, children);

        /// <summary>
        /// Creates a paragraph element.
        /// </summary>
        public static ReactElement P(
            string className = null,
            object style = null,
            params ReactElement[] children
        ) => CreateElement("p", className, null, style, null, children);

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
            string type = "button",
            params ReactElement[] children
        )
        {
            Action<object> clickHandler = null;
            if (onClick != null)
            {
                clickHandler = _ => onClick();
            }
            var props = new
            {
                className = className,
                onClick = clickHandler,
                disabled = disabled,
                type = type,
            };
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
            Action<string> onChange = null,
            bool disabled = false
        )
        {
            Action<object> changeHandler = null;
            if (onChange != null)
            {
                changeHandler = e =>
                    onChange(Script.Get<string>(Script.Get<object>(e, "target"), "value"));
            }
            var props = new
            {
                className = className,
                type = type,
                value = value,
                placeholder = placeholder,
                onChange = changeHandler,
                disabled = disabled,
            };
            return Script.Call<ReactElement>("React.createElement", "input", props);
        }

        /// <summary>
        /// Creates a text node.
        /// </summary>
        public static ReactElement Text(string content) =>
            Script.Call<ReactElement>("React.createElement", "span", null, content);

        /// <summary>
        /// Creates an image element.
        /// </summary>
        public static ReactElement Img(
            string src,
            string alt = null,
            string className = null,
            object style = null
        )
        {
            var props = new
            {
                src = src,
                alt = alt,
                className = className,
                style = style,
            };
            return Script.Call<ReactElement>("React.createElement", "img", props);
        }

        /// <summary>
        /// Creates a link element.
        /// </summary>
        public static ReactElement A(
            string href,
            string className = null,
            string target = null,
            Action onClick = null,
            params ReactElement[] children
        )
        {
            Action<object> clickHandler = null;
            if (onClick != null)
            {
                clickHandler = e =>
                {
                    Script.Write("e.preventDefault()");
                    onClick();
                };
            }
            var props = new
            {
                href = href,
                className = className,
                target = target,
                onClick = clickHandler,
            };
            return Script.Call<ReactElement>("React.createElement", "a", props, children);
        }

        /// <summary>
        /// Creates a nav element.
        /// </summary>
        public static ReactElement Nav(string className = null, params ReactElement[] children) =>
            CreateElement("nav", className, null, null, null, children);

        /// <summary>
        /// Creates a header element.
        /// </summary>
        public static ReactElement Header(
            string className = null,
            params ReactElement[] children
        ) => CreateElement("header", className, null, null, null, children);

        /// <summary>
        /// Creates a main element.
        /// </summary>
        public static ReactElement Main(string className = null, params ReactElement[] children) =>
            CreateElement("main", className, null, null, null, children);

        /// <summary>
        /// Creates an aside element.
        /// </summary>
        public static ReactElement Aside(string className = null, params ReactElement[] children) =>
            CreateElement("aside", className, null, null, null, children);

        /// <summary>
        /// Creates a section element.
        /// </summary>
        public static ReactElement Section(
            string className = null,
            params ReactElement[] children
        ) => CreateElement("section", className, null, null, null, children);

        /// <summary>
        /// Creates an article element.
        /// </summary>
        public static ReactElement Article(
            string className = null,
            params ReactElement[] children
        ) => CreateElement("article", className, null, null, null, children);

        /// <summary>
        /// Creates a footer element.
        /// </summary>
        public static ReactElement Footer(
            string className = null,
            params ReactElement[] children
        ) => CreateElement("footer", className, null, null, null, children);

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
        public static ReactElement Tr(
            string className = null,
            Action onClick = null,
            params ReactElement[] children
        )
        {
            Action<object> clickHandler = null;
            if (onClick != null)
            {
                clickHandler = _ => onClick();
            }
            var props = new { className = className, onClick = clickHandler };
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
        /// Creates an unordered list element.
        /// </summary>
        public static ReactElement Ul(string className = null, params ReactElement[] children) =>
            CreateElement("ul", className, null, null, null, children);

        /// <summary>
        /// Creates a list item element.
        /// </summary>
        public static ReactElement Li(
            string className = null,
            Action onClick = null,
            params ReactElement[] children
        ) => CreateElement("li", className, null, null, onClick, children);

        /// <summary>
        /// Creates a form element.
        /// </summary>
        public static ReactElement Form(
            string className = null,
            Action onSubmit = null,
            params ReactElement[] children
        )
        {
            Action<object> submitHandler = null;
            if (onSubmit != null)
            {
                submitHandler = e =>
                {
                    Script.Write("e.preventDefault()");
                    onSubmit();
                };
            }
            var props = new { className = className, onSubmit = submitHandler };
            return Script.Call<ReactElement>("React.createElement", "form", props, children);
        }

        /// <summary>
        /// Creates a label element.
        /// </summary>
        public static ReactElement Label(
            string htmlFor = null,
            string className = null,
            params ReactElement[] children
        )
        {
            var props = new { htmlFor = htmlFor, className = className };
            return Script.Call<ReactElement>("React.createElement", "label", props, children);
        }

        /// <summary>
        /// Creates a select element.
        /// </summary>
        public static ReactElement Select(
            string className = null,
            string value = null,
            Action<string> onChange = null,
            params ReactElement[] children
        )
        {
            Action<object> changeHandler = null;
            if (onChange != null)
            {
                changeHandler = e =>
                    onChange(Script.Get<string>(Script.Get<object>(e, "target"), "value"));
            }
            var props = new
            {
                className = className,
                value = value,
                onChange = changeHandler,
            };
            return Script.Call<ReactElement>("React.createElement", "select", props, children);
        }

        /// <summary>
        /// Creates an option element.
        /// </summary>
        public static ReactElement Option(string value, string label)
        {
            var props = new { value = value };
            return Script.Call<ReactElement>("React.createElement", "option", props, label);
        }

        /// <summary>
        /// Creates a textarea element.
        /// </summary>
        public static ReactElement TextArea(
            string className = null,
            string value = null,
            string placeholder = null,
            int rows = 0,
            Action<string> onChange = null
        )
        {
            Action<object> changeHandler = null;
            if (onChange != null)
            {
                changeHandler = e =>
                    onChange(Script.Get<string>(Script.Get<object>(e, "target"), "value"));
            }
            var props = new
            {
                className = className,
                value = value,
                placeholder = placeholder,
                rows = rows > 0 ? (object)rows : null,
                onChange = changeHandler,
            };
            return Script.Call<ReactElement>("React.createElement", "textarea", props);
        }

        /// <summary>
        /// Creates an SVG element.
        /// </summary>
        public static ReactElement Svg(
            string className = null,
            int width = 0,
            int height = 0,
            string viewBox = null,
            string fill = null,
            params ReactElement[] children
        )
        {
            var props = new
            {
                className = className,
                width = width > 0 ? (object)width : null,
                height = height > 0 ? (object)height : null,
                viewBox = viewBox,
                fill = fill,
            };
            return Script.Call<ReactElement>("React.createElement", "svg", props, children);
        }

        /// <summary>
        /// Creates a path element for SVG.
        /// </summary>
        public static ReactElement Path(
            string d,
            string fill = null,
            string stroke = null,
            int strokeWidth = 0
        )
        {
            var props = new
            {
                d = d,
                fill = fill,
                stroke = stroke,
                strokeWidth = strokeWidth > 0 ? (object)strokeWidth : null,
            };
            return Script.Call<ReactElement>("React.createElement", "path", props);
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
            string tag,
            string className,
            string id,
            object style,
            Action onClick,
            ReactElement[] children
        )
        {
            Action<object> clickHandler = null;
            if (onClick != null)
            {
                clickHandler = _ => onClick();
            }
            var props = new
            {
                className = className,
                id = id,
                style = style,
                onClick = clickHandler,
            };
            return Script.Call<ReactElement>("React.createElement", tag, props, children);
        }
    }
}
