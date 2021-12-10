#nullable enable

//using SharpDX;
//using System.Collections.Generic;

using MyUtilities;

using static System.Math;
//using static System.Diagnostics.Debug;
using static MyUtilities.DebugHelper;

namespace IntervalWavefront;

using EventQueue = PriorityQueue<Event>;

public class EdgeInterval
{
	public readonly Segment ParentSegment;

	// 対象とする HalfEdge
	public readonly HalfEdge Edge;

	// Edge 上の範囲
	public IntervalExtent Extent;

	// 両隣に来る EdgeInterval を表す．Adjoin 関数が呼ばれるまでは null
	public EdgeInterval Prev = null!, Next = null!;

	public Event? Event;

	public bool IsPropagated;
	public bool IsCrossed;

	//public ChildRidge? Ridge;

	private ChildRidge? ridge;

	public Segment? ChildSegment;

	public EdgeInterval(Segment parent, HalfEdge edge, IntervalExtent extent)
	{
		ParentSegment = parent;
		Edge = edge;
		Extent = extent;

		parent.Children.Add(this);
	}

	public ChildRidge? Ridge
	{
		get => ridge;
		set {
			ridge = value;

			if (value != null) {
				ParentSegment.Ridges.Add(value);

				var s = Prev.ParentSegment;
				if (s == ParentSegment) s = Prev.Prev.ParentSegment;
				Assert(s != ParentSegment);

				s.Ridges.Add(value);
			}
		}
	}

	public EdgeInterval? Parent => ParentSegment.Parent;

	public DVector3 Center => ParentSegment.Center;

	public DVector3 PointL => Edge.GetCartesianPos(Extent.L);

	public DVector3 PointU => Edge.GetCartesianPos(Extent.U);

	public double Distance(DVector3 p) => ParentSegment.Distance(p);

	//public double Distance(double x) => Distance(Edge.GetCartesianPos(x));

	public static DVector3 CalcArcBoundary(EdgeInterval i, EdgeInterval j, double r)
	{
		if (i.ParentSegment == j.ParentSegment) {
			if (i.Parent == null) {
				DVector3 p = i.Edge.GetCartesianPos(i.Extent.U);
				return i.Center + r * DVector3.Normalize(p - i.Center);
			}

			return new DVector3();
		}

		if (i.Edge.Adj.Face == j.Edge.Face) {
			double s = i.Edge.GetProjectedParametricPos(i.Center);
			double discriminant = r * r + s * s - DVector3.DistanceSquared(i.Center, i.Edge.Origin);
			//Assert(discriminant >= -1e-4);
			if (discriminant < 0) discriminant = 0;

			double t = s - Sqrt(discriminant);
			return i.Edge.GetCartesianPos(t);
		}

		if (j.Edge.Adj.Face == i.Edge.Face) {
			double s = j.Edge.GetProjectedParametricPos(j.Center);
			double discriminant = r * r + s * s - DVector3.DistanceSquared(j.Center, j.Edge.Origin);
			//Assert(discriminant >= -1e-4);
			if (discriminant < 0) discriminant = 0;

			double t = s + Sqrt(discriminant);
			return j.Edge.GetCartesianPos(t);
		}

		if (i.Edge.Face == j.Edge.Face) {
			DVector3 p = i.Center, q = j.Center;
			DVector3 N = i.Edge.Face.Normal;

			DVector3 M = 0.5 * (p + q);
			DVector3 Y = DVector3.Normalize(DVector3.Cross(N, p - q));

			double y2 = r * r - DVector3.DistanceSquared(p, M);

			//Assert(y2 >= 0);
			if (y2 < 0) y2 = 0;

			return M + Sqrt(y2) * Y;
		}

		return new DVector3();
	}

	public EdgeInterval? Project(Simulator simulator, HalfEdge target)
	{
		Assert(target.Face == Edge.Adj.Face && target.Adj != Edge);

		if (ChildSegment == null) {
			ChildSegment = Segment.FromParent(this);
			simulator.Segments.AddItem(target.Face.Index, ChildSegment);
		}

		DVector3 newCenter = ChildSegment.Center;

		if (target.SignedDistance(newCenter) < 0) return null;

		DVector3 N = Edge.Adj.Face.Normal;

		var v = DVector3.Cross(Edge.GetCartesianPos(Extent.L) - newCenter, N);
		var w = DVector3.Cross(Edge.GetCartesianPos(Extent.U) - newCenter, N);

		double a = -DVector3.Dot(v, target.Direction);
		double b = -DVector3.Dot(w, target.Direction);

		//if (Max(Abs(a), Abs(b)) < 0.0001 * Edge.Length)
		//	return null;

		var extent = new IntervalExtent(0, target.Length);

		if (Edge.Tail != target.Tail || Extent.L > 0)
			extent.IntersectHalfLine(DVector3.Dot(v, target.Origin - newCenter) / a, a > 0);

		if (Edge.Head != target.Head || Extent.U < Edge.Length)
			extent.IntersectHalfLine(DVector3.Dot(w, target.Origin - newCenter) / b, b < 0);

		//if (extent.IsEmpty) return null;

		return new EdgeInterval(ChildSegment, target, extent);
	}

	// この EdgeInterval を interval の前に挿入する
	public void InsertBefore(EdgeInterval interval)
	{
		Adjoin(interval.Prev, this);
		Adjoin(this, interval);
	}

	// この EdgeInterval を interval の後に挿入する
	public void InsertAfter(EdgeInterval interval)
	{
		Adjoin(this, interval.Next);
		Adjoin(interval, this);
	}

	public void Remove()
	{
		Adjoin(Prev, Next);
	}

	// a.Next, ..., b に対して TrimBoundary を呼び出す
	// a.Next, ..., b.Prev に対して Redundant Pairs を除去する
	// a に対して Extent.IsEmpty なら CheckCollapseEvent を呼び出す
	// a.Next, ..., b に対して Update を呼び出す
	public static void Update(EventQueue queue, EdgeInterval a, EdgeInterval b)
	{
		EdgeInterval i = a;

		do {
			i = i.Next;
			TrimBoundary(i.Prev, i);
		} while (i != b);

		if (a.RemoveIfRedundant(queue)) {
			Assert(a != b);
			a = a.Prev;
		}

		for (i = a.Next; i != b; i = i.Next) {
			i.RemoveIfRedundant(queue);
		}

		if (a != b && b.RemoveIfRedundant(queue)) {
			b = b.Next;
		}

		if (a != b && a.Extent.IsEmpty)
			a.RegisterEvent(queue, a.CheckArcCollapseEvents());

		i = a;

		do {
			i = i.Next;
			i.RegisterEvent(queue, i.CheckEvents());
		} while (i != b);
	}

	private bool RemoveIfRedundant(EventQueue queue)
	{
		if (!Extent.IsEmpty) return false;

		//if (Parent != Next.Parent && Parent != Prev.Parent) return false;

		if (Parent == Next.Parent) {
			if (Next.Extent.IsEmpty && Prev.Edge == Edge) return false;
			Next.Ridge = Ridge;
		}
		else {
			if (Parent != Prev.Parent) return false;
		}

		if (Event != null)
			queue.Delete(Event);

		Remove();
		TrimBoundary(Prev, Next);

		return true;
	}

	private Event? CheckEvents()
	{
		if (Extent.IsEmpty)
			return CheckArcCollapseEvents();

		if (Extent.L > 0) {
			if (Prev.Edge == Edge && !IsCrossed && !Prev.Extent.IsEmpty)
				return new CrossEvent(this, Edge.GetCartesianPos(Extent.L));
		}
		else {
			if (Edge.Tail == Prev.Edge.Head)
				return Event ?? new VertexEvent(this);
		}

		return null;
	}

	private Event? CheckArcCollapseEvents()
	{
		if (Parent == Prev.Parent || Parent == Next.Parent)
			return null;

		if (Prev == Parent && !Next.Extent.IsEmpty)
			return new CWSwapEvent(this, Next.Edge.GetCartesianPos(Next.Extent.L));

		if (Next == Parent && !Prev.Extent.IsEmpty)
			return new CCWSwapEvent(this, Prev.Edge.GetCartesianPos(Prev.Extent.U));

		if (Prev.Edge.Face != Edge.Face || Next.Edge.Face != Edge.Face)
			return null;

		// Compute the center of the circumscribed circle
		var position = DVector3.CalcCircumCenter(Center, Prev.Center, Next.Center);

		return new CollisionEvent(this, position);
	}

	public void RegisterEvent(EventQueue queue, Event? newEvent)
	{
		if (Event == newEvent) return;

		if (newEvent != null) {
			if (Event != null)
				queue.Replace(Event, newEvent);
			else
				queue.Push(newEvent);
		}
		else {
			if (Event != null)
				queue.Delete(Event);
		}

		Event = newEvent;
	}

	public static void Adjoin(EdgeInterval i, EdgeInterval j)
	{
		i.Next = j;
		j.Prev = i;
	}

	private static void TrimBoundary(EdgeInterval i, EdgeInterval j)
	{
		if (i.Parent == j.Parent) return;
		if (i.Extent.IsEmpty && j.Extent.IsEmpty) return;

		if (i.Edge == j.Edge) {
			double x = CalcTiePoint(i, j, i.Edge);

			Assert(!double.IsNaN(x));

			i.Extent.U = Min(i.Extent.U, x);
			j.Extent.L = Max(j.Extent.L, x);
		}
		else if (i.Edge.Tail == j.Edge.Head) {
			double x = CalcTiePoint(i, j, i.Edge);

			if (!double.IsNaN(x) && x >= 0) {
				Assert(x <= i.Edge.Length);

				// The tie point is in i.Edge, j will be empty
				i.Extent.U = Min(i.Extent.U, x);
				j.Extent.U = 0;
			}
			else {
				// The tie point is in j.Edge, i will be empty
				x = CalcTiePoint(i, j, j.Edge);
				Assert(!double.IsNaN(x));

				j.Extent.L = Max(j.Extent.L, x);
				i.Extent.U = 0;
			}
		}
		else if (i.Edge.Head == j.Edge.Tail) {
			double x_i = CalcTiePoint(i, j, i.Edge);
			double x_j = CalcTiePoint(i, j, j.Edge);

			if (!double.IsNaN(x_i) && x_i >= 0)
				i.Extent.U = Min(i.Extent.U, x_i);
			else
				i.Extent.U = 0;

			if (!double.IsNaN(x_j) && x_j <= j.Edge.Length)
				j.Extent.L = Max(j.Extent.L, x_j);
			else
				j.Extent.U = 0;
		}
	}

	private static double CalcTiePoint(EdgeInterval i_1, EdgeInterval i_2, HalfEdge edge)
	{
		double u_1 = edge.GetProjectedParametricPos(i_1.Center);
		double u_2 = edge.GetProjectedParametricPos(i_2.Center);

		double c_1 = DVector3.DistanceSquared(i_1.Center, edge.Origin);
		double c_2 = DVector3.DistanceSquared(i_2.Center, edge.Origin);

		double P = u_2 - u_1;

		if (P <= 0) return double.NaN;
		return 0.5 * (c_2 - c_1) / P;
	}

	public void AddExtentToBuffer(LineBuffer buffer, IntervalExtent extent)
	{
		if (extent.IsEmpty) return;

		buffer.Add(Edge.GetCartesianPos(extent.L), Edge.GetCartesianPos(extent.U), Edge.Face);
	}

	public void AddWavefrontToBuffer(LineBuffer buffer, double distance)
	{
		EdgeInterval i = Parent != Prev.Parent || Parent == null ? this : Prev;
		EdgeInterval j = Parent != Next.Parent || Parent == null ? this : Next;

		DVector3 lowerEnd = CalcArcBoundary(i.Prev, i, distance);
		DVector3 upperEnd = CalcArcBoundary(j, j.Next, distance);

		//Assert(Abs(Distance(lowerEnd) - distance) < 1e-6);
		//Assert(Abs(Distance(upperEnd) - distance) < 1e-6);

		DVector3 a = lowerEnd - Center;
		DVector3 b = upperEnd - Center;

		// Spherical linear interpolation

		double theta = DVector3.Angle(a, b);
		int numPartition = (int)(256 / PI * theta);

		double dt = theta / numPartition;
		double c = 1 / Sin(theta);

		DVector3 p = lowerEnd;

		for (int n = 1; n < numPartition; n++) {
			double t = dt * n;

			DVector3 q = c * Sin(theta - t) * a + c * Sin(t) * b + Center;

			if (n % 2 == 0 || Edge.Face.Contains(q)) {
				buffer.Add(p, q, Edge.Face);
			}

			p = q;
		}

		buffer.Add(p, upperEnd, Edge.Face);
	}

	public override string ToString() => $"{Edge} {Event?.Name}";
}
