#nullable enable

using System;
using System.Collections.Immutable;
using System.Collections.Generic;
using System.IO;
using System.Numerics;

using static System.Linq.Enumerable;
using static System.Math;

namespace MyUtilities;

public abstract class IndexedElement
{
	public readonly int Index;

	public IndexedElement(int index) { Index = index; }
}

public abstract class DistancePoint : IndexedElement
{
	// 位置
	public readonly DVector3 Position;

	public DistancePoint(DVector3 position, int index) : base(index) { Position = position; }

	public abstract bool IsFrontFacing { get; }
}

// 頂点
public class Vertex : DistancePoint
{
	// この頂点を Tail に持つような HalfEdge のひとつ
	public HalfEdge AnOutgoingEdge = null!;

	public double TotalAngle { get; internal set; }

	// 隣接する頂点の数
	public int Degree { get; internal set; }

	public Vertex(DVector3 position, int index) : base(position, index) { }

	public override bool IsFrontFacing => OutgoingEdges.Any(edge => edge.Face.IsFrontFacing);

	public IEnumerable<HalfEdge> OutgoingEdges
	{
		get {
			HalfEdge edge = AnOutgoingEdge;
			do {
				yield return edge;

				edge = edge.Prev.Adj;
			} while (edge != AnOutgoingEdge);
		}
	}

	public override string ToString() => $"{Index}";
}

public class EdgeMidpoint : DistancePoint
{
	public readonly HalfEdge AnEdge;

	public EdgeMidpoint(HalfEdge edge, int index)
		: base(0.5 * (edge.Tail.Position + edge.Head.Position), index)
	{
		AnEdge = edge;
	}

	public override bool IsFrontFacing => AnEdge.IsFrontFacing;
}

// 有向辺
public class HalfEdge : IndexedElement
{
	public readonly DVector3 Direction;
	public readonly double Length;
	public readonly Vertex Tail, Head;
	public readonly Face Face;
	public HalfEdge Prev, Next, Adj;
	public EdgeMidpoint Midpoint { get; internal set; }

	public HalfEdge(Vertex tail, Vertex head, Face face, int index)
		: base(index)
	{
		DVector3 v = head.Position - tail.Position;
		Direction = DVector3.Normalize(v);
		Length = DVector3.Length(v);

		Head = head;
		Tail = tail;
		Face = face;

		Prev = Next = Adj = null!;
		Midpoint = null!;
	}

	public DVector3 Origin => Tail.Position;

	public DVector3 GetCartesianPos(double t)
		=> t == Length ? Head.Position : Origin + Direction * t;

	public double GetProjectedParametricPos(DVector3 p)
		=> DVector3.Dot(p - Origin, Direction);

	public DVector3 UnfoldPointToAdjFace(DVector3 p)
	{
		DVector3 u = Direction;
		DVector3 v = DVector3.Cross(Face.Normal, u);
		DVector3 w = DVector3.Cross(Adj.Face.Normal, u);

		DVector3 r = p - Origin;

		return DVector3.Dot(r, u) * u + DVector3.Dot(r, v) * w + Origin;
	}

	// Face を含む平面上の点 p からこの有向辺への符号付き距離 (Face 側を正とする) を求める
	public double SignedDistance(DVector3 p)
	{
		return DVector3.Det(Origin - p, Direction, Face.Normal);
	}

	public double IntersectDirectedLineParametric(DVector3 p, DVector3 d)
	{
		DVector3 v = DVector3.Cross(d, Face.Normal);
		return DVector3.Dot(p - Origin, v) / DVector3.Dot(Direction, v);
	}

	// Face を含む平面上の直線 pq に対し, この有向辺との交点を求める
	public double IntersectLineParametric(DVector3 p, DVector3 q)
	{
		DVector3 v = DVector3.Cross(q - p, Face.Normal);
		return DVector3.Dot(p - Origin, v) / DVector3.Dot(Direction, v);
	}

	public DVector3 IntersectLineCartesian(DVector3 p, DVector3 q)
		=> GetCartesianPos(IntersectLineParametric(p, q));

	public DVector3 GetRotatedDirection(double angle)
		=> Cos(angle) * Direction + Sin(angle) * DVector3.Cross(Face.Normal, Direction);

	public bool IsFrontFacing => Face.IsFrontFacing || Adj.Face.IsFrontFacing;

	public override string ToString() => $"{Tail.Index} → {Head.Index}";
}

// 面
public class Face : IndexedElement
{
	public readonly ImmutableArray<HalfEdge> Edges;
	public readonly DVector3 Normal;

	private bool isFrontFacing;

	public Face(Vertex a, Vertex b, Vertex c, int index)
		: base(index)
	{
		int i = index * 3;

		Edges = ImmutableArray.Create(
			new HalfEdge(a, b, this, i),
			new HalfEdge(b, c, this, i + 1),
			new HalfEdge(c, a, this, i + 2)
		);

		Edges[1].Prev = Edges[2].Next = Edges[0];
		Edges[2].Prev = Edges[0].Next = Edges[1];
		Edges[0].Prev = Edges[1].Next = Edges[2];

		a.TotalAngle += DVector3.Angle(b.Position - a.Position, c.Position - a.Position);
		b.TotalAngle += DVector3.Angle(c.Position - b.Position, a.Position - b.Position);
		c.TotalAngle += DVector3.Angle(a.Position - c.Position, b.Position - c.Position);

		DVector3 N = DVector3.Cross(Edges[0].Direction, Edges[1].Direction);

		//Assert(DVector3.Length(N) > 0.0001);

		Normal = DVector3.Normalize(N);
	}

	// この面とレイの交差判定を行い, より近い位置で交差する場合レイを更新する
	public void Intersect(HitTestRay ray)
	{
		// レイと交差しない場合を除外する
		foreach (var edge in Edges) {
			if (DVector3.Det(ray.Origin - edge.Origin, edge.Direction, ray.Direction) < 0)
				return;
		}

		// レイの始点から平面までの垂線で測った符号付き距離
		double s = DVector3.Dot(Edges[0].Origin - ray.Origin, Normal);

		// レイの始点から交点までの符号付き距離
		double t = s / DVector3.Dot(ray.Direction, Normal);

		if (t > 0 && t < ray.Length) {
			ray.Length = t;
			ray.Face = this;
		}
	}

	public bool Contains(DVector3 pos)
		=> Edges.All(edge => edge.SignedDistance(pos) > 0);

	public bool IsFrontFacing => isFrontFacing;

	public void UpdateIsFrontFacing(DVector3 eye)
	{
		DVector3 v = eye - Edges[0].Origin;
		isFrontFacing = DVector3.Dot(v, Normal) > 0;
	}
}

public class SearchMesh
{
	public readonly ImmutableArray<Vertex> Vertices;
	public readonly ImmutableArray<Face> Faces;
	public readonly ImmutableArray<EdgeMidpoint> Midpoints;

	public readonly Vector3 Center, Size;
	public readonly Matrix4x4 ModelMatrix, InverseModelMatrix;

	public SearchMesh(ImmutableArray<Vertex> vertices, ImmutableArray<Face> faces)
	{
		var midpoints = ImmutableArray.CreateBuilder<EdgeMidpoint>();

		var edgeDict = new Dictionary<int, HalfEdge>();

		int MakeHash(int a, int b) => a * vertices.Length + b;

		var upper = new Vector3(float.NegativeInfinity);
		var lower = new Vector3(float.PositiveInfinity);

		foreach (var vertex in vertices) {
			var position = (Vector3)vertex.Position;

			upper = Vector3.Max(upper, position);
			lower = Vector3.Min(lower, position);
		}

		foreach (var face in faces) {
			foreach (var edge in face.Edges) {
				edgeDict[MakeHash(edge.Tail.Index, edge.Head.Index)] = edge;
			}
		}

		foreach (var edge in edgeDict.Values) {
			edge.Adj = edgeDict[MakeHash(edge.Head.Index, edge.Tail.Index)];

			edge.Tail.Degree++;
			edge.Tail.AnOutgoingEdge = edge;

			if (edge.Tail.Index < edge.Head.Index) {
				var e = new EdgeMidpoint(edge, vertices.Length + midpoints.Count);

				edge.Midpoint = e;
				edge.Adj.Midpoint = e;

				midpoints.Add(e);
			}
		}

		Center = 0.5f * (upper + lower);
		Size = upper - Center;
		float scale = Max(Size.Y, Max(Size.X, Size.Z) * (7 / 8f));

		ModelMatrix = Matrix4x4.CreateTranslation(-Center) * Matrix4x4.CreateScale(1 / scale);
		InverseModelMatrix = Matrix4x4.CreateScale(scale) * Matrix4x4.CreateTranslation(Center);

		Vertices = vertices;
		Faces = faces;
		Midpoints = midpoints.ToImmutable();
	}

	public static SearchMesh CreateFromTxtFile(string filename)
	{
		static string[] Split(string s)
			=> s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

		using var file = new StreamReader(filename);

		string[] s = Split(file.ReadLine()!);
		int numVertices = int.Parse(s[0]);
		int numFaces = int.Parse(s[1]);

		var vertices = ImmutableArray.CreateBuilder<Vertex>(numVertices);
		var faces = ImmutableArray.CreateBuilder<Face>(numFaces);

		foreach (int i in Range(0, numVertices)) {
			s = Split(file.ReadLine()!);

			DVector3 position;
			position.X = double.Parse(s[0]);
			position.Y = double.Parse(s[1]);
			position.Z = double.Parse(s[2]);

			vertices.Add(new Vertex(position, i));
		}

		foreach (int i in Range(0, numFaces)) {
			s = Split(file.ReadLine()!);

			int a = int.Parse(s[0]);
			int b = int.Parse(s[1]);
			int c = int.Parse(s[2]);

			faces.Add(new Face(vertices[a], vertices[b], vertices[c], i));
		}

		return new SearchMesh(vertices.ToImmutable(), faces.ToImmutable());
	}

	public SearchMesh CreateSubdividedMesh()
	{
		int numVertices = Vertices.Length + Midpoints.Length;
		int numFaces = Faces.Length * 4;

		var vertices = new TypedIndexedArray<DistancePoint, Vertex>(numVertices);
		var faces = ImmutableArray.CreateBuilder<Face>(numFaces);

		foreach (var vertex in Vertices) {
			double beta = vertex.Degree > 3 ? 3.0 / (8.0 * vertex.Degree) : 3.0 / 16.0;
			DVector3 position = (1 - beta * vertex.Degree) * vertex.Position;

			foreach (HalfEdge outgoingEdge in vertex.OutgoingEdges) {
				// outgoingEdge.Head : an adjacent vertex of the vertex
				position += beta * outgoingEdge.Head.Position;
			}

			var evenVertex = new Vertex(position, vertex.Index);
			vertices[vertex] = evenVertex;
		}

		foreach (var face in Faces) {
			foreach (var edge in face.Edges) {
				if (vertices[edge.Midpoint] != null) continue;

				DVector3 u = edge.Tail.Position + edge.Head.Position;
				DVector3 v = edge.Next.Head.Position + edge.Adj.Next.Head.Position;
				DVector3 position = 3.0 / 8.0 * u + 1.0 / 8.0 * v;

				var oddVertex = new Vertex(position, edge.Midpoint.Index);
				vertices[edge.Midpoint] = oddVertex;
			}

			Vertex a = vertices[face.Edges[0].Tail];
			Vertex b = vertices[face.Edges[1].Tail];
			Vertex c = vertices[face.Edges[2].Tail];

			Vertex d = vertices[face.Edges[0].Midpoint];
			Vertex e = vertices[face.Edges[1].Midpoint];
			Vertex f = vertices[face.Edges[2].Midpoint];

			faces.Add(new Face(a, d, f, faces.Count));
			faces.Add(new Face(b, e, d, faces.Count));
			faces.Add(new Face(c, f, e, faces.Count));
			faces.Add(new Face(d, e, f, faces.Count));
		}

		return new SearchMesh(vertices.ToImmutableArray(), faces.ToImmutable());
	}

	public void CalcContour(LineBuffer buffer, TypedIndexedArray<DistancePoint, double> distance, double r, bool useMidpoints)
	{
		void AppendContourPoint(DistancePoint a, DistancePoint b, Face face)
		{
			double u = distance[a] - r;
			double v = distance[b] - r;

			if (double.IsNaN(u) || double.IsNaN(v)) return;

			if (u * v < 0) {
				buffer.Add((v * a.Position - u * b.Position) / (v - u), face);
			}
		}

		void AppendContourLineSegment(DistancePoint a, DistancePoint b, DistancePoint c, Face face)
		{
			AppendContourPoint(a, b, face);
			AppendContourPoint(b, c, face);
			AppendContourPoint(c, a, face);
		}

		buffer.Clear();

		if (useMidpoints) {
			foreach (Face face in Faces) {
				foreach (HalfEdge edge in face.Edges)
					AppendContourLineSegment(edge.Prev.Midpoint, edge.Tail, edge.Midpoint, face);

				foreach (HalfEdge edge in face.Edges)
					AppendContourPoint(edge.Prev.Midpoint, edge.Midpoint, face);
			}
		}
		else {
			foreach (Face face in Faces) {
				foreach (HalfEdge edge in face.Edges)
					AppendContourPoint(edge.Tail, edge.Head, face);
			}
		}
	}

	public void DrawIndices(IRenderer renderer, bool drawFront)
	{
		foreach (var vertex in Vertices) {
			if (vertex.IsFrontFacing == drawFront)
				renderer.DrawText((Vector3)vertex.Position, vertex.Index.ToString());
		}
	}

	public void Draw(IRenderer renderer, bool drawFront)
	{
		foreach (var midpoint in Midpoints) {
			HalfEdge edge = midpoint.AnEdge;

			if (edge.IsFrontFacing == drawFront)
				renderer.DrawLine((Vector3)edge.Tail.Position, (Vector3)edge.Head.Position);
		}
	}

	public void UpdateIsFrontFacing(DVector3 eye)
	{
		foreach (var face in Faces)
			face.UpdateIsFrontFacing(eye);
	}

	public void IntersectFaces(HitTestRay ray)
	{
		foreach (var face in Faces)
			face.Intersect(ray);
	}
}
