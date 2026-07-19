#nullable enable

using System;
using System.Collections.Generic;

namespace Icebreaker.Shared
{
    internal static class ContractGuards
    {
        public static string Required(string? value, string paramName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Value cannot be null, empty, or whitespace.", paramName);
            }

            return value;
        }

        public static int NonNegative(int value, string paramName)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(paramName, value, "Value cannot be negative.");
            }

            return value;
        }

        public static long NonNegative(long value, string paramName)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(paramName, value, "Value cannot be negative.");
            }

            return value;
        }

        public static float NonNegative(float value, string paramName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
            {
                throw new ArgumentOutOfRangeException(paramName, value, "Value must be finite and non-negative.");
            }

            return value;
        }

        public static double NonNegative(double value, string paramName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d)
            {
                throw new ArgumentOutOfRangeException(paramName, value, "Value must be finite and non-negative.");
            }

            return value;
        }

        public static int Positive(int value, string paramName)
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(paramName, value, "Value must be greater than zero.");
            }

            return value;
        }

        public static float Positive(float value, string paramName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
            {
                throw new ArgumentOutOfRangeException(paramName, value, "Value must be finite and greater than zero.");
            }

            return value;
        }

        public static float Probability(float value, string paramName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f || value > 1f)
            {
                throw new ArgumentOutOfRangeException(paramName, value, "Probability must be between zero and one.");
            }

            return value;
        }

        public static IReadOnlyList<T> Copy<T>(IReadOnlyList<T>? values, string paramName)
        {
            if (values == null)
            {
                throw new ArgumentNullException(paramName);
            }

            var copy = new T[values.Count];
            for (var index = 0; index < values.Count; index++)
            {
                copy[index] = values[index];
            }

            return Array.AsReadOnly(copy);
        }

        public static T NotNull<T>(T? value, string paramName) where T : class
        {
            return value ?? throw new ArgumentNullException(paramName);
        }
    }
}
