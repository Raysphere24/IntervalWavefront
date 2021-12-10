#nullable enable

using MyUtilities;

namespace IntervalWavefront;

public abstract class Ridge
{
	public readonly DVector3 Position;
	public readonly Face Face;

	public Ridge(DVector3 position, Face face) { Position = position; Face = face; }
}

public abstract class ChildRidge : Ridge
{
	public Ridge? Parent;

	public bool IsGlobal;

	public ChildRidge(DVector3 position, Face face)
		: base(position, face)
	{
	}

	public abstract void AddToBuffer(LineBuffer local, LineBuffer global, DVector3 pos);

	public abstract bool Contains(Vertex vertex);

	public abstract bool ComputeIsGlobal(ChildRidge singularity);

	public override string ToString() => GetType().Name;
}

public class RidgeSource : ChildRidge
{
	public readonly Vertex Vertex;

	public RidgeSource(Vertex vertex, Face face)
		: base(vertex.Position, face)
	{
		Vertex = vertex;
	}

	public override void AddToBuffer(LineBuffer local, LineBuffer global, DVector3 pos)
	{
		(IsGlobal ? global : local).Add(pos, Position, Face);
	}

	public override bool Contains(Vertex vertex) => Vertex == vertex;

	public override bool ComputeIsGlobal(ChildRidge singularity)
	{
		foreach (HalfEdge edge in Vertex.OutgoingEdges) {
			Vertex a = edge.Head, b = edge.Next.Head;

			if (singularity.Contains(a) && singularity.Contains(b))
				return true;
		}

		return false;
	}
}

public class RidgeRun : ChildRidge
{
	public readonly ChildRidge Child;

	public RidgeRun(DVector3 position, Face face, ChildRidge child)
		: base(position, face)
	{
		Child = child;
		Child.Parent = this;

		IsGlobal = child.IsGlobal;
	}

	public override void AddToBuffer(LineBuffer local, LineBuffer global, DVector3 pos)
	{
		(IsGlobal ? global : local).Add(pos, Position, Face);

		Child.AddToBuffer(local, global, Position);
	}

	public override bool Contains(Vertex vertex) => Child.Contains(vertex);

	public override bool ComputeIsGlobal(ChildRidge singularity)
		=> Child.ComputeIsGlobal(singularity);
}

public class RidgeCollision : ChildRidge
{
	public readonly ChildRidge Child1, Child2;

	public RidgeCollision(DVector3 position, Face face, ChildRidge child1, ChildRidge child2)
		: base(position, face)
	{
		Child1 = child1;
		Child2 = child2;

		Child1.Parent = this;
		Child2.Parent = this;

		IsGlobal = Child1.IsGlobal || Child2.IsGlobal || ComputeIsGlobal(this);
	}

	public override void AddToBuffer(LineBuffer local, LineBuffer global, DVector3 pos)
	{
		(IsGlobal ? global : local).Add(pos, Position, Face);

		Child1.AddToBuffer(local, global, Position);
		Child2.AddToBuffer(local, global, Position);
	}

	public override bool Contains(Vertex vertex)
		=> Child1.Contains(vertex) || Child2.Contains(vertex);

	public override bool ComputeIsGlobal(ChildRidge singularity)
		=> Child1.ComputeIsGlobal(singularity) || Child2.ComputeIsGlobal(singularity);
}

public class RidgeSink : Ridge
{
	public readonly ChildRidge Child1, Child2, Child3;

	public RidgeSink(DVector3 position, Face face, ChildRidge child1, ChildRidge child2, ChildRidge child3)
		: base(position, face)
	{
		Child1 = child1;
		Child2 = child2;
		Child3 = child3;

		Child1.Parent = this;
		Child2.Parent = this;
		Child3.Parent = this;
	}

	public void AddToBuffer(LineBuffer local, LineBuffer global)
	{
		Child1.AddToBuffer(local, global, Position);
		Child2.AddToBuffer(local, global, Position);
		Child3.AddToBuffer(local, global, Position);
	}
}
