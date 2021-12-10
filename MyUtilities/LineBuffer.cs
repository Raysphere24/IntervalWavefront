#nullable enable

using System.Collections.Generic;
using System.Numerics;

namespace MyUtilities;

public class LineBuffer
{
	public const int initialCapacity = 64 * 2 * 3;

	protected float[] array = new float[initialCapacity];
	protected int numFloats, numVertices;

	protected bool needsUpdateBuffer;

	private readonly List<Face> faces = new();

	public int VertexCount
	{
		get => numVertices;
		set {
			if (numVertices < value)
				throw new System.InvalidOperationException();

			numVertices = value;
			numFloats = value * 3;
		}
	}

	public void Add(DVector3 p, Face face)
	{
		Append(p);

		if ((numVertices & 1) == 0) {
			faces.Add(face);
			needsUpdateBuffer = true;
		}
	}

	public void Add(DVector3 p, DVector3 q, Face face)
	{
		Append(p);
		Append(q);

		faces.Add(face);
		needsUpdateBuffer = true;
	}

	private void Append(DVector3 p)
	{
		if (numFloats >= array.Length) {
			var newArray = new float[array.Length * 2];

			array.CopyTo(newArray, 0);
			array = newArray;
		}

		array[numFloats++] = (float)p.X;
		array[numFloats++] = (float)p.Y;
		array[numFloats++] = (float)p.Z;
		numVertices++;
	}

	public void Clear()
	{
		numFloats = 0;
		numVertices = 0;
		faces.Clear();
	}

	public void Draw(IRenderer renderer, bool drawFront)
	{
		int i = 0;

		foreach (var face in faces) {
			Vector3 p = new(array[i++], array[i++], array[i++]);
			Vector3 q = new(array[i++], array[i++], array[i++]);

			if (face.IsFrontFacing == drawFront)
				renderer.DrawLine(p, q);
		}
	}

	public string GetString() => string.Join(' ', array[..numFloats]);
}
