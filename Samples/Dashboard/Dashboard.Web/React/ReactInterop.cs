using System;
using H5;
using static H5.Core.dom;

namespace Dashboard.React
{
    /// <summary>
    /// Core React interop types and functions for H5.
    /// </summary>
    public static class ReactInterop
    {
        /// <summary>
        /// Creates a React element using React.createElement.
        /// </summary>
        public static ReactElement CreateElement(
            string type,
            object props = null,
            params object[] children
        ) => Script.Call<ReactElement>("React.createElement", type, props, children);

        /// <summary>
        /// Creates a React element from a component function.
        /// </summary>
        public static ReactElement CreateElement(
            Func<object, ReactElement> component,
            object props = null,
            params object[] children
        ) => Script.Call<ReactElement>("React.createElement", component, props, children);

        /// <summary>
        /// Creates the React root and renders the application.
        /// </summary>
        public static void RenderApp(ReactElement element, string containerId = "root")
        {
            var container = document.getElementById(containerId);
            var root = Script.Call<Root>("ReactDOM.createRoot", container);
            root.Render(element);
        }
    }

    /// <summary>
    /// React element type - represents a virtual DOM node.
    /// </summary>
    [External]
    [Name("Object")]
    public class ReactElement { }

    /// <summary>
    /// React root for concurrent rendering.
    /// </summary>
    [External]
    public class Root
    {
        /// <summary>
        /// Renders a React element into the root.
        /// </summary>
        public extern void Render(ReactElement element);
    }

    /// <summary>
    /// React ref object for accessing DOM elements.
    /// </summary>
    [External]
    [Name("Object")]
    public class RefObject<T>
    {
        /// <summary>
        /// Current value of the ref.
        /// </summary>
        public extern T Current { get; set; }
    }
}
