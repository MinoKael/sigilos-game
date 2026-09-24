using System;
using System.Collections.Generic;
using System.Linq;

namespace Sigilos.Tests
{
	internal sealed class AssertionException : Exception
	{
		public AssertionException(string message) : base(message)
		{
		}
	}

	internal static class Assert
	{
		public static void True(bool condition, string message)
		{
			if (!condition)
				throw new AssertionException(message);
		}

		public static void False(bool condition, string message) => True(!condition, message);

		public static void Equal<T>(T expected, T actual, string what)
		{
			if (!EqualityComparer<T>.Default.Equals(expected, actual))
				throw new AssertionException($"{what}: esperado <{expected}>, veio <{actual}>");
		}

		public static void Near(double expected, double actual, string what, double tolerance = 1e-9)
		{
			if (Math.Abs(expected - actual) > tolerance)
				throw new AssertionException($"{what}: esperado <{expected}>, veio <{actual}>");
		}

		public static void Empty(IEnumerable<string> problems, string what)
		{
			var list = problems.ToList();
			if (list.Count > 0)
				throw new AssertionException($"{what}:\n- " + string.Join("\n- ", list));
		}
	}
}
