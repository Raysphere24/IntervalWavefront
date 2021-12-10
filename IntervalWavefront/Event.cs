#nullable enable

using MyUtilities;

//using System.Collections.Generic;

//using static System.Linq.Enumerable;
//using static System.Diagnostics.Debug;
using static MyUtilities.DebugHelper;

namespace IntervalWavefront;

public abstract class Event : PriorityQueueNode
{
	// 対象とする EdgeInterval
	public readonly EdgeInterval Interval;

	// イベントが起こる点の位置
	public readonly DVector3 Position;

	public Event(EdgeInterval interval, DVector3 position)
		: base(interval.Distance(position))
	{ Interval = interval; Position = position; }

	public abstract void Handle(Simulator simulator);

	public override string ToString() => $"{Priority:0.000000} {GetType().Name}";

	public string Name => GetType().Name;
}

public class VertexEvent : Event
{
	public VertexEvent(EdgeInterval interval)
		: base(interval, interval.Edge.Origin) { }

	public override void Handle(Simulator simulator)
	{
		Assert(Interval.Event == this);
		Interval.Event = null;

		Vertex v = Interval.Edge.Tail;

		bool FirstIntervalHasLessAngle(EdgeInterval i_1, EdgeInterval i_2)
		{
			double c_1 = DVector3.Dot(i_1.Edge.Direction, DVector3.Normalize(v.Position - i_1.Center));
			double c_2 = DVector3.Dot(i_2.Edge.Direction, DVector3.Normalize(i_2.Center - v.Position));

			return c_1 > c_2;
		}

		EdgeInterval i_1 = Interval.Prev, i_2 = Interval;

		while (i_1.IsPropagated && i_1.Edge.Head == v) {
			Assert(i_1.Extent.U == i_1.Edge.Length);
			i_1.Remove();
			i_1 = i_1.Prev;
		}

		while (i_2.IsPropagated && i_2.Edge.Tail == v) {
			Assert(i_2.Extent.L == 0);
			i_2.Remove();
			i_2 = i_2.Next;
		}

		EdgeInterval a_1 = i_1.Prev, a_2 = i_2.Next;

		EdgeInterval? j_1 = i_1, j_2 = i_2;

		while (i_1.Edge.Adj != i_2.Edge) {
			// (i_1 == j_1) means j_1 must be computed

			if (i_1 == j_1) j_1 = i_1.Edge.Head == v ? i_1.Project(simulator, i_1.Edge.Adj.Prev) : null;
			if (i_2 == j_2) j_2 = i_2.Edge.Tail == v ? i_2.Project(simulator, i_2.Edge.Adj.Next) : null;

			bool use_1;

			if (j_1 != null && j_2 != null)
				use_1 = FirstIntervalHasLessAngle(j_1, j_2);
			else if (j_1 != null)
				use_1 = true;
			else if (j_2 != null)
				use_1 = false;
			else
				break;

			if (use_1) {
				j_1!.InsertAfter(i_1);
				i_1.Project(simulator, i_1.Edge.Adj.Next)?.InsertAfter(i_1);
				i_1.IsPropagated = true;
				i_1 = j_1;
			}
			else {
				j_2!.InsertBefore(i_2);
				i_2.Project(simulator, i_2.Edge.Adj.Prev)?.InsertBefore(i_2);
				i_2.IsPropagated = true;
				i_2 = j_2;
			}
		}

		if (i_1.Edge.Adj != i_2.Edge) {
			if (i_1.Edge.Head == v) {
				i_1.Project(simulator, i_1.Edge.Adj.Next)!.InsertAfter(i_1);
				i_1.IsPropagated = true;
			}

			if (i_2.Edge.Tail == v) {
				i_2.Project(simulator, i_2.Edge.Adj.Prev)!.InsertBefore(i_2);
				i_2.IsPropagated = true;

				i_2.Prev.Ridge = new RidgeSource(v, i_2.Prev.Edge.Face);
			}
			else {
				i_2.Ridge = new RidgeSource(v, i_2.Edge.Face);
			}
		}
		else if (FirstIntervalHasLessAngle(i_1, i_2)) {
			i_1.Project(simulator, i_1.Edge.Adj.Next)!.InsertAfter(i_1);
			i_1.IsPropagated = true;
			i_2.Ridge = new RidgeSource(v, i_2.Edge.Face);
		}
		else {
			i_2.Project(simulator, i_2.Edge.Adj.Prev)!.InsertBefore(i_2);
			i_2.IsPropagated = true;
			i_2.Prev.Ridge = new RidgeSource(v, i_2.Prev.Edge.Face);
		}

		if (simulator.DoNotTrim) return;
		EdgeInterval.Update(simulator.Queue, a_1, a_2);
	}
}

class CrossEvent : Event
{
	public CrossEvent(EdgeInterval interval, DVector3 position)
		: base(interval, position) { }

	public override void Handle(Simulator simulator)
	{
		Assert(Interval.Event == this);
		Interval.IsCrossed = true;
		Interval.Event = null;

		EdgeInterval i = Interval.Prev, j = Interval;
		EdgeInterval a = i.Prev, b = j.Next;

		bool jWasPropagated = j.IsPropagated;

		if (i.IsPropagated) {
			i.Remove();
		}
		else {
			i.Project(simulator, i.Edge.Adj.Prev)?.InsertAfter(i);
			i.Project(simulator, i.Edge.Adj.Next)?.InsertAfter(i);
			i.IsPropagated = true;
		}

		if (j.IsPropagated) {
			j.Remove();
		}
		else {
			j.Project(simulator, j.Edge.Adj.Next)?.InsertBefore(j);
			j.Project(simulator, j.Edge.Adj.Prev)?.InsertBefore(j);
			j.IsPropagated = true;
		}

		EdgeInterval c = jWasPropagated ? b : j.Prev.Prev.Parent == j ? j.Prev.Prev : j.Prev;

		Assert(j.Ridge != null);

		c.Ridge = new RidgeRun(Position, c.Edge.Face, j.Ridge!);
		j.Ridge = null;

		if (simulator.DoNotTrim) return;
		EdgeInterval.Update(simulator.Queue, a, b);
	}
}

class CollisionEvent : Event
{
	public CollisionEvent(EdgeInterval interval, DVector3 position)
		: base(interval, position) { }

	public override void Handle(Simulator simulator)
	{
		Assert(Interval.Event == this);
		Interval.Event = null;

		EdgeInterval i = Interval;
		EdgeInterval a = i.Prev, b = i.Next;

		if (b.Next == a) {
			Assert(i.Ridge != null);
			Assert(a.Ridge != null);
			Assert(b.Ridge != null);

			simulator.RidgeSink = new RidgeSink(
				Position, i.Edge.Face, a.Ridge!, i.Ridge!, b.Ridge!);

			a.RegisterEvent(simulator.Queue, null);
			b.RegisterEvent(simulator.Queue, null);
		}
		else {
			i.Remove();

			Assert(i.Ridge != null);
			Assert(b.Ridge != null);

			i.Next.Ridge = new RidgeCollision(
				Position, i.Edge.Face, i.Ridge!, b.Ridge!);

			if (simulator.DoNotTrim) return;
			EdgeInterval.Update(simulator.Queue, a, b);
		}
	}
}

class CWSwapEvent : Event
{
	public CWSwapEvent(EdgeInterval interval, DVector3 position)
		: base(interval, position) { }

	public override void Handle(Simulator simulator)
	{
		Assert(Interval.Event == this);
		Interval.Event = null;

		EdgeInterval i = Interval, j = Interval.Next;
		EdgeInterval a = i.Prev, b = j.Next;

		bool jWasPropagated = j.IsPropagated;

		i.Parent!.Extent.U = 0;
		i.Remove();

		if (j.IsPropagated) {
			j.Remove();
		}
		else {
			j.Project(simulator, j.Edge.Adj.Next)?.InsertBefore(j);
			j.Project(simulator, j.Edge.Adj.Prev)?.InsertBefore(j);
			j.IsPropagated = true;
		}

		EdgeInterval c = jWasPropagated ? b : j.Prev.Prev.Parent == j ? j.Prev.Prev : j.Prev;

		Assert(j.Ridge != null);

		c.Ridge = new RidgeRun(Position, c.Edge.Face, j.Ridge!);
		j.Ridge = null;

		if (simulator.DoNotTrim) return;
		EdgeInterval.Update(simulator.Queue, a, b);
	}
}

class CCWSwapEvent : Event
{
	public CCWSwapEvent(EdgeInterval interval, DVector3 position)
		: base(interval, position) { }

	public override void Handle(Simulator simulator)
	{
		Assert(Interval.Event == this);
		Interval.Event = null;

		EdgeInterval i = Interval.Prev, j = Interval;
		EdgeInterval a = i.Prev, b = j.Next;

		j.Parent!.Extent.U = 0;
		j.Remove();

		if (i.IsPropagated) {
			i.Remove();
		}
		else {
			i.Project(simulator, i.Edge.Adj.Prev)?.InsertAfter(i);
			i.Project(simulator, i.Edge.Adj.Next)?.InsertAfter(i);
			i.IsPropagated = true;
		}

		Assert(j.Ridge != null);

		j.Next.Ridge = new RidgeRun(Position, j.Next.Edge.Face, j.Ridge!);

		if (simulator.DoNotTrim) return;
		EdgeInterval.Update(simulator.Queue, a, b);
	}
}
