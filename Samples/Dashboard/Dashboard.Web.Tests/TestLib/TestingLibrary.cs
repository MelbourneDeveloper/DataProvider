namespace Dashboard.Tests.TestLib
{
    using System;
    using System.Threading.Tasks;
    using H5;

    /// <summary>
    /// C# wrapper for React Testing Library.
    /// Provides render, query, and interaction methods.
    /// </summary>
    public static class TestingLibrary
    {
        /// <summary>
        /// Renders a React component and returns a RenderResult for querying.
        /// </summary>
        public static RenderResult Render(React.ReactElement element)
        {
            var result = Script.Call<object>("TestingLibrary.render", element);
            return new RenderResult(result);
        }

        /// <summary>
        /// Renders the full App component with optional mock fetch.
        /// </summary>
        public static RenderResult RenderApp(Func<string, Task<object>> mockFetch = null)
        {
            if (mockFetch != null)
            {
                Script.Set("window", "mockFetch", mockFetch);
            }

            var appElement = Dashboard.App.Render();
            return Render(appElement);
        }

        /// <summary>
        /// Waits for an element matching the text to appear.
        /// </summary>
        public static Task<object> WaitFor(Func<bool> condition, int timeout = 5000) =>
            Script.Call<Task<object>>(
                "TestingLibrary.waitFor",
                (Func<object>)(
                    () =>
                    {
                        if (!condition())
                        {
                            throw new Exception("Condition not met");
                        }
                        return null;
                    }
                ),
                new { timeout }
            );

        /// <summary>
        /// Waits for text to appear in the document.
        /// </summary>
        public static Task<object> WaitForText(string text, int timeout = 5000) =>
            Script.Call<Task<object>>(
                "TestingLibrary.waitFor",
                (Func<object>)(
                    () =>
                    {
                        var found = Script.Call<bool>("document.body.textContent.includes", text);
                        if (!found)
                        {
                            throw new Exception("Text not found: " + text);
                        }
                        return null;
                    }
                ),
                new { timeout }
            );

        /// <summary>
        /// Simulates typing text into an input element.
        /// </summary>
        public static Task UserType(object element, string text) =>
            Script.Call<Task>("TestingLibrary.userEvent.type", element, text);

        /// <summary>
        /// Simulates clicking an element.
        /// </summary>
        public static Task UserClick(object element) =>
            Script.Call<Task>("TestingLibrary.userEvent.click", element);

        /// <summary>
        /// Fires a click event on an element.
        /// </summary>
        public static void FireClick(object element) =>
            Script.Call<object>("TestingLibrary.fireEvent.click", element);

        /// <summary>
        /// Fires a change event on an element.
        /// </summary>
        public static void FireChange(object element, object eventInit) =>
            Script.Call<object>("TestingLibrary.fireEvent.change", element, eventInit);

        /// <summary>
        /// Clears all rendered components (call in cleanup).
        /// </summary>
        public static void Cleanup() => Script.Call<object>("TestingLibrary.cleanup");

        /// <summary>
        /// Advances fake timers by specified milliseconds.
        /// </summary>
        public static void AdvanceTimers(int ms) =>
            Script.Call<object>("jest.advanceTimersByTime", ms);

        /// <summary>
        /// Runs all pending timers immediately.
        /// </summary>
        public static void RunAllTimers() => Script.Call<object>("jest.runAllTimers");
    }

    /// <summary>
    /// Result from rendering a React component.
    /// Provides query methods to find elements.
    /// </summary>
    public class RenderResult
    {
        private readonly object _result;

        /// <summary>
        /// Creates a new render result wrapper.
        /// </summary>
        public RenderResult(object result)
        {
            _result = result;
        }

        /// <summary>
        /// Gets the container DOM element.
        /// </summary>
        public object Container => Script.Get<object>(_result, "container");

        /// <summary>
        /// Gets the base element (usually document.body).
        /// </summary>
        public object BaseElement => Script.Get<object>(_result, "baseElement");

        /// <summary>
        /// Unmounts the rendered component.
        /// </summary>
        public void Unmount()
        {
            _ = Script.Get<object>(_result, "unmount");
            Script.Write("unmount()");
        }

        /// <summary>
        /// Re-renders the component with new props.
        /// </summary>
        public void Rerender(React.ReactElement element)
        {
            _ = Script.Get<object>(_result, "rerender");
            Script.Write("rerender(element)");
        }

        // Query by text

        /// <summary>
        /// Gets an element by its text content. Throws if not found.
        /// </summary>
        public object GetByText(string text) =>
            Script.Call<object>("TestingLibrary.screen.getByText", text);

        /// <summary>
        /// Gets all elements matching the text.
        /// </summary>
        public object[] GetAllByText(string text) =>
            Script.Call<object[]>("TestingLibrary.screen.getAllByText", text);

        /// <summary>
        /// Queries for an element by text. Returns null if not found.
        /// </summary>
        public object QueryByText(string text) =>
            Script.Call<object>("TestingLibrary.screen.queryByText", text);

        /// <summary>
        /// Queries for all elements by text.
        /// </summary>
        public object[] QueryAllByText(string text) =>
            Script.Call<object[]>("TestingLibrary.screen.queryAllByText", text);

        /// <summary>
        /// Finds an element by text (waits for it to appear).
        /// </summary>
        public Task<object> FindByText(string text, int timeout = 5000) =>
            Script.Call<Task<object>>("TestingLibrary.screen.findByText", text, new { timeout });

        // Query by role

        /// <summary>
        /// Gets an element by its ARIA role. Throws if not found.
        /// </summary>
        public object GetByRole(string role, object options = null) =>
            Script.Call<object>("TestingLibrary.screen.getByRole", role, options);

        /// <summary>
        /// Gets all elements matching the role.
        /// </summary>
        public object[] GetAllByRole(string role, object options = null) =>
            Script.Call<object[]>("TestingLibrary.screen.getAllByRole", role, options);

        /// <summary>
        /// Queries for an element by role. Returns null if not found.
        /// </summary>
        public object QueryByRole(string role, object options = null) =>
            Script.Call<object>("TestingLibrary.screen.queryByRole", role, options);

        /// <summary>
        /// Finds an element by role (waits for it to appear).
        /// </summary>
        public Task<object> FindByRole(string role, object options = null, int timeout = 5000) =>
            Script.Call<Task<object>>(
                "TestingLibrary.screen.findByRole",
                role,
                options ?? new { timeout }
            );

        // Query by placeholder

        /// <summary>
        /// Gets an element by its placeholder text.
        /// </summary>
        public object GetByPlaceholderText(string text) =>
            Script.Call<object>("TestingLibrary.screen.getByPlaceholderText", text);

        /// <summary>
        /// Queries for an element by placeholder text.
        /// </summary>
        public object QueryByPlaceholderText(string text) =>
            Script.Call<object>("TestingLibrary.screen.queryByPlaceholderText", text);

        /// <summary>
        /// Finds an element by placeholder text (waits).
        /// </summary>
        public Task<object> FindByPlaceholderText(string text, int timeout = 5000) =>
            Script.Call<Task<object>>(
                "TestingLibrary.screen.findByPlaceholderText",
                text,
                new { timeout }
            );

        // Query by test ID

        /// <summary>
        /// Gets an element by its data-testid attribute.
        /// </summary>
        public object GetByTestId(string testId) =>
            Script.Call<object>("TestingLibrary.screen.getByTestId", testId);

        /// <summary>
        /// Queries for an element by test ID.
        /// </summary>
        public object QueryByTestId(string testId) =>
            Script.Call<object>("TestingLibrary.screen.queryByTestId", testId);

        /// <summary>
        /// Finds an element by test ID (waits).
        /// </summary>
        public Task<object> FindByTestId(string testId, int timeout = 5000) =>
            Script.Call<Task<object>>(
                "TestingLibrary.screen.findByTestId",
                testId,
                new { timeout }
            );

        // Query by label

        /// <summary>
        /// Gets an element by its associated label text.
        /// </summary>
        public object GetByLabelText(string text) =>
            Script.Call<object>("TestingLibrary.screen.getByLabelText", text);

        /// <summary>
        /// Queries for an element by label text.
        /// </summary>
        public object QueryByLabelText(string text) =>
            Script.Call<object>("TestingLibrary.screen.queryByLabelText", text);

        // Query by CSS selector (escape hatch)

        /// <summary>
        /// Queries using a CSS selector on the container.
        /// </summary>
        public object QuerySelector(string selector)
        {
            _ = Container;
            return Script.Write<object>("container.querySelector(selector)");
        }

        /// <summary>
        /// Queries all matching elements using a CSS selector.
        /// </summary>
        public object[] QuerySelectorAll(string selector)
        {
            _ = Container;
            var nodeList = Script.Write<object>("container.querySelectorAll(selector)");
            return Script.Call<object[]>("Array.from", nodeList);
        }

        // Assertions

        /// <summary>
        /// Asserts that an element is in the document.
        /// </summary>
        public void ExpectToBeInDocument(object element)
        {
            Script.Call<object>("expect", element);
            Script.Call<object>("expect(arguments[0]).toBeInTheDocument");
        }

        /// <summary>
        /// Asserts that an element has specific text content.
        /// </summary>
        public void ExpectToHaveTextContent(object element, string text)
        {
            _ = Script.Call<object>("expect", element);
            Script.Write("expectResult.toHaveTextContent(text)");
        }
    }
}
