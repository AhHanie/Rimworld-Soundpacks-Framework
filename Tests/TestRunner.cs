using System;
using System.Collections.Generic;

namespace Soundpacks_Framework.Tests
{
    public sealed class AssertionFailedException : Exception
    {
        public AssertionFailedException(string message) : base(message) { }
    }

    public static class Assert
    {
        public static void True(bool condition, string message)
        {
            if (!condition) throw new AssertionFailedException("Expected true: " + message);
        }

        public static void False(bool condition, string message)
        {
            if (condition) throw new AssertionFailedException("Expected false: " + message);
        }

        public static void Equal<T>(T expected, T actual, string message)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new AssertionFailedException($"{message}: expected <{expected}> but was <{actual}>");
            }
        }

        public static void NotNull(object value, string message)
        {
            if (value == null) throw new AssertionFailedException("Expected non-null: " + message);
        }

        public static void Null(object value, string message)
        {
            if (value != null) throw new AssertionFailedException("Expected null: " + message);
        }

        public static void Throws(Action action, string message)
        {
            try
            {
                action();
            }
            catch
            {
                return;
            }
            throw new AssertionFailedException("Expected an exception: " + message);
        }
    }

    public sealed class TestCase
    {
        public string Name;
        public Action Body;
    }

    public sealed class TestRunner
    {
        private readonly List<TestCase> _cases = new List<TestCase>();

        public void Add(string name, Action body)
        {
            _cases.Add(new TestCase { Name = name, Body = body });
        }

        public int RunAll()
        {
            int passed = 0;
            int failed = 0;
            foreach (var testCase in _cases)
            {
                try
                {
                    testCase.Body();
                    passed++;
                    Console.WriteLine("PASS  " + testCase.Name);
                }
                catch (Exception ex)
                {
                    failed++;
                    Console.WriteLine("FAIL  " + testCase.Name + " :: " + ex.Message);
                }
            }
            Console.WriteLine();
            Console.WriteLine(passed + " passed, " + failed + " failed, " + _cases.Count + " total");
            return failed;
        }
    }
}
