#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;

using static System.Linq.Enumerable;

namespace MyUtilities;

public static class StaticMethods
{
	[DebuggerHidden]
	public static T? ArgMin<T>(this IEnumerable<T> collection, Func<T, double> selector) where T : class
	{
		double min = double.PositiveInfinity;
		T? result = null;

		foreach (T item in collection) {
			double value = selector(item);

			if (min > value) {
				min = value;
				result = item;
			}
		}

		return result;
	}

	public static Vector2 ToVector2(this Vector3 v) => new(v.X, v.Y);
}

public static class DebugHelper
{
	[Conditional("DEBUG"), DebuggerHidden]
	public static void Assert(bool condition)
	{
		if (!condition) throw new Exception("Assertion failed");
	}
}
