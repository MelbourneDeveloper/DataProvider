namespace Dashboard.Tests.TestLib
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using H5;

    /// <summary>
    /// Test runner for browser-based tests.
    /// </summary>
    public static class TestRunner
    {
        private static readonly List<TestCase> _tests = new List<TestCase>();
        private static readonly List<TestResult> _results = new List<TestResult>();

        /// <summary>
        /// Registers a test case.
        /// </summary>
        public static void Test(string name, Func<Task> testFn) => _tests.Add(new TestCase { Name = name, TestFn = testFn });

        /// <summary>
        /// Registers a synchronous test case.
        /// </summary>
        public static void Test(string name, Action testFn) => _tests.Add(
                new TestCase
                {
                    Name = name,
                    TestFn = () =>
                    {
                        testFn();
                        return Task.CompletedTask;
                    },
                }
            );

        /// <summary>
        /// Groups related tests together.
        /// </summary>
        public static void Describe(string name, Action tests)
        {
            Log($"\n📦 {name}");
            tests();
        }

        /// <summary>
        /// Runs all registered tests.
        /// </summary>
        public static async Task RunAll()
        {
            Log("\n🧪 Running Dashboard Tests\n");
            Log("═══════════════════════════════════════════════════════════════");

            var passed = 0;
            var failed = 0;

            foreach (var test in _tests)
            {
                try
                {
                    await test.TestFn();
                    _results.Add(
                        new TestResult
                        {
                            Name = test.Name,
                            Passed = true,
                        }
                    );
                    Log($"✅ {test.Name}");
                    passed++;
                }
                catch (Exception ex)
                {
                    _results.Add(
                        new TestResult
                        {
                            Name = test.Name,
                            Passed = false,
                            Error = ex.Message,
                        }
                    );
                    Log($"❌ {test.Name}");
                    Log($"   Error: {ex.Message}");
                    failed++;
                }
                finally
                {
                    // Cleanup after each test
                    TestingLibrary.Cleanup();
                }
            }

            Log("\n═══════════════════════════════════════════════════════════════");
            Log($"\n📊 Results: {passed} passed, {failed} failed, {_tests.Count} total");

            if (failed == 0)
            {
                Log("\n🎉 All tests passed!");
            }
            else
            {
                Log($"\n💥 {failed} test(s) failed!");
            }

            // Store results on window for external access
            Script.Set("window", "testResults", _results);
            Script.Set("window", "testsPassed", passed);
            Script.Set("window", "testsFailed", failed);
        }

        /// <summary>
        /// Clears all registered tests.
        /// </summary>
        public static void Clear()
        {
            _tests.Clear();
            _results.Clear();
        }

        private static void Log(string message) => Script.Call<object>("console.log", message);
    }

    /// <summary>
    /// A test case.
    /// </summary>
    public class TestCase
    {
        /// <summary>
        /// Name of the test.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Test function to execute.
        /// </summary>
        public Func<Task> TestFn { get; set; }
    }

    /// <summary>
    /// Result of a test execution.
    /// </summary>
    public class TestResult
    {
        /// <summary>
        /// Name of the test.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Whether the test passed.
        /// </summary>
        public bool Passed { get; set; }

        /// <summary>
        /// Error message if failed.
        /// </summary>
        public string Error { get; set; }
    }
}
