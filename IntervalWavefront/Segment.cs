#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Numerics;

using MyUtilities;

using static System.Math;
using static System.Linq.Enumerable;

namespace IntervalWavefront;

public class Segment : MultivaluedArrayNode<Segment>
{
	public readonly EdgeInterval? Parent;
	public readonly List<EdgeInterval> Children = new();
	public readonly List<ChildRidge> Ridges = new();

	public readonly Face Face;

	// 等距離線 (円弧) の中心
	public readonly DVector3 Center;

	public readonly Matrix4x4 UnfoldingMatrix;

	private Segment(EdgeInterval? parent, Face face, DVector3 center, Matrix4x4 matrix)
	{
		Parent = parent;
		Face = face;
		Center = center;
		UnfoldingMatrix = matrix;
	}

	public static Segment Root(Face face, DVector3 center)
	{
		Vector3 p = (Vector3)center;

		Vector3 from = (Vector3)face.Normal;
		Vector3 to = Vector3.UnitZ;
		Vector3 axis = Vector3.Normalize(Vector3.Cross(from, to));

		Matrix4x4 matrix = Matrix4x4.CreateTranslation(-p) * RotationMatrix(axis, from, to);
		return new Segment(null, face, center, matrix);
	}

	public static Segment FromParent(EdgeInterval parent)
	{
		HalfEdge edge = parent.Edge;

		DVector3 p = edge.Origin;
		DVector3 u = edge.Direction;
		DVector3 v = DVector3.Cross(edge.Face.Normal, u);
		DVector3 w = DVector3.Cross(edge.Adj.Face.Normal, u);

		DVector3 r = parent.Center - p;

		//Assert(Abs(DVector3.Dot(r, Face.Normal)) < 0.0001);

		DVector3 center = DVector3.Dot(r, u) * u + DVector3.Dot(r, v) * w + p;

		Vector3 pf = (Vector3)p;

		Matrix4x4 R = RotationMatrix((Vector3)u, (Vector3)w, (Vector3)v);
		Matrix4x4 M = Matrix4x4.CreateTranslation(-pf) * R * Matrix4x4.CreateTranslation(pf)
			* parent.ParentSegment.UnfoldingMatrix;

		return new Segment(parent, edge.Adj.Face, center, M);
	}

	// Face 内の点 p と始点の距離
	public double Distance(DVector3 p) => DVector3.Distance(p, Center);

	// Edge.Face 内の点 point に対し, 始点までの測地線を求めて buffer に LineList の形式で追加する
	public static void AddPathToBuffer(Segment u, LineBuffer buffer, DVector3 point)
	{
		DVector3 prev = point;

		while (true) {
			EdgeInterval? i = u.Parent;

			if (i == null) {
				buffer.Add(prev, u.Center, u.Face);
				return;
			}

			double x = i.Edge.IntersectLineParametric(point, u.Center);

			if (!i.Extent.Contains(x))
				return;

			point = i.Edge.GetCartesianPos(x);

			buffer.Add(prev, point, u.Face);

			u = i.ParentSegment;
			prev = point;
		}
	}

	public Unfolding ComputeUnfolding()
	{
		Unfolding unfolding = new();

		ComputeUnfolding(unfolding);

		return unfolding;
	}

	private void ComputeUnfolding(Unfolding unfolding)
	{
		foreach (EdgeInterval i in Children) {
			if (!i.Extent.IsEmpty)
				unfolding.Add(unfolding.Edges, i.PointL, i.PointU, UnfoldingMatrix);

			i.ChildSegment?.ComputeUnfolding(unfolding);
		}

		foreach (ChildRidge r in Ridges) {
			unfolding.Add(unfolding.Ridges, r.Position, r.Parent!.Position, UnfoldingMatrix);
		}
	}

	private static Matrix4x4 RotationMatrix(Vector3 axis, Vector3 from, Vector3 to)
	{
		static Matrix4x4 DirectProduct(Vector3 a, Vector3 b)
			=> new(
				a.X * b.X, a.X * b.Y, a.X * b.Z, 0,
				a.Y * b.X, a.Y * b.Y, a.Y * b.Z, 0,
				a.Z * b.X, a.Z * b.Y, a.Z * b.Z, 0,
				0, 0, 0, 0
				);

		Vector3 from2 = Vector3.Cross(axis, from), to2 = Vector3.Cross(axis, to);

		Matrix4x4 m = DirectProduct(axis, axis) + DirectProduct(from, to) + DirectProduct(from2, to2);

		m.M44 = 1;

		return m;
	}
}

public class Unfolding
{
	public readonly List<(Vector2, Vector2)> Edges = new(), Ridges = new();

	public Vector2 Lower, Upper;

	public void Add(List<(Vector2, Vector2)> buffer, DVector3 a, DVector3 b, Matrix4x4 matrix)
	{
		Vector2 p = Vector3.Transform((Vector3)a, matrix).ToVector2();
		Vector2 q = Vector3.Transform((Vector3)b, matrix).ToVector2();

		Lower = Vector2.Min(Lower, p);
		Lower = Vector2.Min(Lower, q);

		Upper = Vector2.Max(Upper, p);
		Upper = Vector2.Max(Upper, q);

		buffer.Add((p, q));
	}

	public void WriteTo(string filename, double maxRadius)
	{
		using StreamWriter stream = new(filename) { NewLine = "\n" };

		float cl = Min(Lower.X, Lower.Y);
		float cu = Max(Upper.X, Upper.Y);
		float c = Max(-cl, cu);
		float b = 256;
		float s = b / c;

		void WriteLineGroup(string color, List<(Vector2, Vector2)> lines)
		{
			stream.WriteLine($"<g fill='none' stroke='{color}'>");

			foreach (var (a, b) in lines) {
				stream.WriteLine($"<line x1='{a.X * s}' y1='{-a.Y * s}' x2='{b.X * s}' y2='{-b.Y * s}'/>");
			}

			stream.WriteLine("</g>");
		}

		stream.WriteLine("<?xml version='1.0'?>");
		stream.WriteLine(
			$"<svg viewBox='-{b} -{b} {2 * b} {2 * b}' stroke-width='1' xmlns='http://www.w3.org/2000/svg'>");

		stream.WriteLine("<circle cx='0' cy='0' r='4'/>");

		if (maxRadius > 0) {
			stream.WriteLine("<g fill='none' stroke='cyan'>");

			double r = maxRadius * s;
			const int numSteps = 8;
			foreach (int i in Range(1, numSteps)) {
				stream.WriteLine($"<circle cx='0' cy='0' r='{r * i / numSteps}'/>");
			}

			stream.WriteLine("</g>");
		}

		WriteLineGroup("black", Edges);
		WriteLineGroup("lime", Ridges);

		stream.WriteLine("</svg>");
	}
}
