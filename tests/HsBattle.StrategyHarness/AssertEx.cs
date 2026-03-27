using System;

namespace HsBattle.StrategyHarness
{
    internal static class AssertEx
    {
        public static void True(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        public static void Equal<T>(T expected, T actual, string message)
        {
            if (!Equals(expected, actual))
            {
                throw new InvalidOperationException(message + " Expected=" + expected + " Actual=" + actual);
            }
        }
    }
}
