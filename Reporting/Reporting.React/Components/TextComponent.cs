using Reporting.React.Core;
using static Reporting.React.Core.Elements;

namespace Reporting.React.Components
{
    /// <summary>
    /// Renders a text block (heading, body, or caption).
    /// </summary>
    public static class TextComponent
    {
        /// <summary>
        /// Renders a text component with the given style.
        /// </summary>
        public static ReactElement Render(string content, string style)
        {
            var className = style switch
            {
                "heading" => "report-text-heading",
                "caption" => "report-text-caption",
                _ => "report-text-body",
            };

            return Div(
                className: className,
                children: new[] { Text(content ?? "") }
            );
        }
    }
}
