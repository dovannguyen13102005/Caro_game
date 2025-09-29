using System;
using System.Collections.Generic;

namespace Xunit
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class FactAttribute : Attribute
    {
    }

    public static class Assert
    {
        public static void True(bool condition, string? message = null)
        {
            if (!condition)
            {
                throw new AssertionException(message ?? "Assert.True failed.");
            }
        }

        public static void False(bool condition, string? message = null)
        {
            if (condition)
            {
                throw new AssertionException(message ?? "Assert.False failed.");
            }
        }

        public static void Equal<T>(T expected, T actual, string? message = null)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new AssertionException(message ?? $"Assert.Equal failed. Expected: {expected}, Actual: {actual}");
            }
        }

        private sealed class AssertionException : Exception
        {
            public AssertionException(string message) : base(message)
            {
            }
        }
    }
}
