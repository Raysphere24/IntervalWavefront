#nullable enable

using System.Collections.Generic;

using MyUtilities;

using static System.Linq.Enumerable;

namespace IntervalWavefront;

using EventQueue = PriorityQueue<Event>;

public class Simulator
{
	public readonly EventQueue Queue = new();
	public readonly List<Event> History = new();
	public readonly Dictionary<System.Type, int> EventCount = new();

	public MultivaluedArray<Segment> Segments = null!;

	public Segment? RootSegment;
	public RidgeSink? RidgeSink;

	public double Radius { get; private set; }
	public int StepCount { get; private set; }

	public bool DoNotTrim;

	public void Initialize(SearchMesh model, Face face, DVector3 pos)
	{
		if (Segments == null || Segments.Length < model.Faces.Length)
			Segments = new MultivaluedArray<Segment>(model.Faces.Length);
		else
			Segments.Clear();

		Queue.Clear();

		RootSegment = Segment.Root(face, pos);
		Segments.AddItem(face.Index, RootSegment);

		//var intervals = element.GetSeedIntervals(pos).ToList();

		var intervals = face.Edges.Select(e => new EdgeInterval(RootSegment, e, new(0, e.Length))).ToList();

		var prev = intervals.Last();
		foreach (var interval in intervals) {
			EdgeInterval.Adjoin(prev, interval);
			prev = interval;
		}

		EdgeInterval.Update(Queue, prev, prev);

		History.Clear();

		EventCount[typeof(VertexEvent)] = 0;
		EventCount[typeof(CrossEvent)] = 0;
		EventCount[typeof(CollisionEvent)] = 0;
		EventCount[typeof(CWSwapEvent)] = 0;
		EventCount[typeof(CCWSwapEvent)] = 0;

		RidgeSink = null;
		Radius = 0;
		StepCount = 0;
		DoNotTrim = false;
	}

	public bool SearchStep(int numSteps)
	{
		if (numSteps < 0) numSteps = (int)Queue.Count;

		for (int i = 0; i < numSteps; i++) {
			Radius = Queue.Top.Priority;
			StepCount++;

			History.Add(Queue.Top);
			EventCount[Queue.Top.GetType()]++;

			Queue.Pop().Handle(this);
			if (Queue.IsEmpty) return true;
		}

		return false;
	}
}
