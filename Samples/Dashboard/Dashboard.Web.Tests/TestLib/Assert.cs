namespace Dashboard.Tests.TestLib
{
    using System;
    using System.Collections.Generic;
    using H5;

    /// <summary>
    /// Assertion helpers for tests.
    /// </summary>
    public static class Assert
    {
        /// <summary>
        /// Asserts that a condition is true.
        /// </summary>
        public static void IsTrue(bool condition, string message = null)
        {
            if (!condition)
            {
                throw new AssertionException(message ?? "Expected true but was false");
            }
        }

        /// <summary>
        /// Asserts that a condition is false.
        /// </summary>
        public static void IsFalse(bool condition, string message = null)
        {
            if (condition)
            {
                throw new AssertionException(message ?? "Expected false but was true");
            }
        }

        /// <summary>
        /// Asserts that two values are equal.
        /// </summary>
        public static void AreEqual<T>(T expected, T actual, string message = null)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new AssertionException(
                    message ?? $"Expected '{expected}' but was '{actual}'"
                );
            }
        }

        /// <summary>
        /// Asserts that two values are not equal.
        /// </summary>
        public static void AreNotEqual<T>(T notExpected, T actual, string message = null)
        {
            if (EqualityComparer<T>.Default.Equals(notExpected, actual))
            {
                throw new AssertionException(
                    message ?? $"Expected not '{notExpected}' but was equal"
                );
            }
        }

        /// <summary>
        /// Asserts that a value is null.
        /// </summary>
        public static void IsNull(object value, string message = null)
        {
            if (value != null)
            {
                throw new AssertionException(message ?? $"Expected null but was '{value}'");
            }
        }

        /// <summary>
        /// Asserts that a value is not null.
        /// </summary>
        public static void IsNotNull(object value, string message = null)
        {
            if (value == null)
            {
                throw new AssertionException(message ?? "Expected not null but was null");
            }
        }

        /// <summary>
        /// Asserts that an element exists in the DOM.
        /// </summary>
        public static void ElementExists(object element, string message = null)
        {
            if (element == null)
            {
                throw new AssertionException(message ?? "Element not found in DOM");
            }
        }

        /// <summary>
        /// Asserts that an element does not exist in the DOM.
        /// </summary>
        public static void ElementNotExists(object element, string message = null)
        {
            if (element != null)
            {
                throw new AssertionException(message ?? "Element should not exist in DOM");
            }
        }

        /// <summary>
        /// Asserts that text is visible in the document.
        /// </summary>
        public static void TextVisible(string text, string message = null)
        {
            var found = Script.Call<bool>("document.body.textContent.includes", text);
            if (!found)
            {
                throw new AssertionException(message ?? $"Text '{text}' not found in document");
            }
        }

        /// <summary>
        /// Asserts that text is not visible in the document.
        /// </summary>
        public static void TextNotVisible(string text, string message = null)
        {
            var found = Script.Call<bool>("document.body.textContent.includes", text);
            if (found)
            {
                throw new AssertionException(
                    message ?? $"Text '{text}' should not be visible in document"
                );
            }
        }

        /// <summary>
        /// Asserts that an element has a specific class.
        /// </summary>
        public static void HasClass(object element, string className, string message = null)
        {
            var classList = Script.Get<object>(element, "classList");
            var hasClass = Script.Call<bool>(classList, "contains", className);
            if (!hasClass)
            {
                throw new AssertionException(
                    message ?? $"Element does not have class '{className}'"
                );
            }
        }

        /// <summary>
        /// Asserts that an element has specific text content.
        /// </summary>
        public static void HasTextContent(
            object element,
            string expectedText,
            string message = null
        )
        {
            var textContent = Script.Get<string>(element, "textContent");
            if (!textContent.Contains(expectedText))
            {
                throw new AssertionException(
                    message
                        ?? $"Element does not contain text '{expectedText}'. Actual: '{textContent}'"
                );
            }
        }

        /// <summary>
        /// Asserts that an element has a specific attribute value.
        /// </summary>
        public static void HasAttribute(
            object element,
            string attributeName,
            string expectedValue,
            string message = null
        )
        {
            var actualValue = Script.Call<string>(element, "getAttribute", attributeName);
            if (actualValue != expectedValue)
            {
                throw new AssertionException(
                    message
                        ?? $"Expected attribute '{attributeName}' to be '{expectedValue}' but was '{actualValue}'"
                );
            }
        }

        /// <summary>
        /// Asserts that a collection has a specific count.
        /// </summary>
        public static void Count<T>(int expected, T[] collection, string message = null)
        {
            if (collection.Length != expected)
            {
                throw new AssertionException(
                    message
                        ?? $"Expected collection count {expected} but was {collection.Length}"
                );
            }
        }

        /// <summary>
        /// Asserts that a collection is empty.
        /// </summary>
        public static void IsEmpty<T>(T[] collection, string message = null)
        {
            if (collection.Length != 0)
            {
                throw new AssertionException(
                    message ?? $"Expected empty collection but had {collection.Length} items"
                );
            }
        }

        /// <summary>
        /// Asserts that a collection is not empty.
        /// </summary>
        public static void IsNotEmpty<T>(T[] collection, string message = null)
        {
            if (collection.Length == 0)
            {
                throw new AssertionException(
                    message ?? "Expected non-empty collection but was empty"
                );
            }
        }

        /// <summary>
        /// Fails the test with a message.
        /// </summary>
        public static void Fail(string message)
        {
            throw new AssertionException(message);
        }
    }

    /// <summary>
    /// Exception thrown when an assertion fails.
    /// </summary>
    public class AssertionException : Exception
    {
        /// <summary>
        /// Creates a new assertion exception.
        /// </summary>
        public AssertionException(string message)
            : base(message) { }
    }
}
