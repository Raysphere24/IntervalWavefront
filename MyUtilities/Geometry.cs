#nullable enable

using System.Numerics;

using static System.Math;

namespace MyUtilities;

public struct DVector3
{
	public double X, Y, Z;

	public DVector3(double x, double y, double z)
	{
		X = x;
		Y = y;
		Z = z;
	}

	public static explicit operator DVector3(Vector3 v)
		=> new(v.X, v.Y, v.Z);

	public static explicit operator Vector3(DVector3 v)
		=> new((float)v.X, (float)v.Y, (float)v.Z);

	public static DVector3 operator -(DVector3 v)
		=> new(-v.X, -v.Y, -v.Z);

	public static DVector3 operator +(DVector3 u, DVector3 v)
		=> new(u.X + v.X, u.Y + v.Y, u.Z + v.Z);

	public static DVector3 operator -(DVector3 u, DVector3 v)
		=> new(u.X - v.X, u.Y - v.Y, u.Z - v.Z);

	public static DVector3 operator *(double c, DVector3 v)
		=> new(c * v.X, c * v.Y, c * v.Z);

	public static DVector3 operator *(DVector3 v, double c)
		=> new(v.X * c, v.Y * c, v.Z * c);

	public static DVector3 operator /(DVector3 v, double c)
		=> new(v.X / c, v.Y / c, v.Z / c);

	public static double Dot(DVector3 u, DVector3 v)
		=> u.X * v.X + u.Y * v.Y + u.Z * v.Z;

	public static DVector3 Cross(DVector3 u, DVector3 v)
		=> new(u.Y * v.Z - u.Z * v.Y, u.Z * v.X - u.X * v.Z, u.X * v.Y - u.Y * v.X);

	public static double Det(DVector3 u, DVector3 v, DVector3 w)
		=> Dot(Cross(u, v), w);

	public static double Length(DVector3 v) => Sqrt(Dot(v, v));
	public static double LengthSquared(DVector3 v) => Dot(v, v);

	public static double Distance(DVector3 u, DVector3 v) => Length(u - v);
	public static double DistanceSquared(DVector3 u, DVector3 v) => LengthSquared(u - v);

	public static DVector3 Normalize(DVector3 v) => v / Length(v);

	public static double Angle(DVector3 u, DVector3 v)
		=> Acos(Dot(u, v) / Sqrt(Dot(u, u) * Dot(v, v)));

	public static double AngleNormalized(DVector3 u, DVector3 v)
		=> Acos(Min(Dot(u, v), 1));

	public static DVector3 CalcCircumCenter(DVector3 p, DVector3 q, DVector3 r)
	{
		DVector3 u = q - r, v = p - r, w = p - q;

		double a = LengthSquared(u) * Dot(v, w);
		double b = LengthSquared(v) * Dot(u, w) * (-1);
		double c = LengthSquared(w) * Dot(u, v);

		return (a * p + b * q + c * r) / (a + b + c);
	}

	public override string ToString() => X.ToString() + ' ' + Y.ToString() + ' ' + Z.ToString();

	public string ToString(string format)
		=> X.ToString(format) + ' ' + Y.ToString(format) + ' ' + Z.ToString(format);
}

public struct IntervalExtent
{
	public double L, U;

	public IntervalExtent(double lower, double upper) { L = lower; U = upper; }

	public bool IsEmpty => L >= U;
	//public bool IsEmpty => U - L <= 1e-4;

	public bool Contains(double x) => L <= x && x <= U;

	public double Clamp(double x) => Min(Max(x, L), U);

	public void IntersectHalfLine(double x, bool positive)
	{
		if (positive)
			L = Max(L, x);
		else
			U = Min(U, x);
	}

	public override string ToString() => IsEmpty ? "Empty" : $"({L:0.000000}, {U:0.000000})";
}

public class DirectionRange
{
	public readonly DVector3 Lower, Upper;

	public DirectionRange(DVector3 lower, DVector3 upper) { Lower = lower; Upper = upper; }

	public static DirectionRange Create(DVector3 u, DVector3 v, DVector3 normal)
	{
		if (DVector3.Det(u, v, normal) >= 0)
			return new DirectionRange(u, v);
		else
			return new DirectionRange(v, u);
	}

	public static DirectionRange? Intersect(DirectionRange a, DirectionRange b, DVector3 normal)
	{
		if (DVector3.Det(a.Upper, b.Lower, normal) >= 0) return null;
		if (DVector3.Det(b.Upper, a.Lower, normal) >= 0) return null;

		return new DirectionRange(
			DVector3.Det(a.Lower, b.Lower, normal) >= 0 ? b.Lower : a.Lower,
			DVector3.Det(a.Upper, b.Upper, normal) >= 0 ? a.Upper : b.Upper
		);
	}

	public bool Contains(DVector3 v, DVector3 normal)
	{
		return DVector3.Det(Lower, v, normal) > 0 && DVector3.Det(v, Upper, normal) > 0;
	}
}

public class HitTestRay
{
	public readonly DVector3 Origin, Direction;
	public double Length;
	public Face? Face;

	public HitTestRay(DVector3 position, DVector3 direction)
	{
		Origin = position;
		Direction = direction;
		Length = double.PositiveInfinity;
	}

	public DVector3 Position => Origin + Length * Direction;
}
