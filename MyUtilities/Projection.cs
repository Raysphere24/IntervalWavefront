using System;
using System.Numerics;
using System.Runtime.InteropServices;

namespace MyUtilities;

public class Projection
{
	public float Aspect, BoundY, ConvergenceZ;

	public Matrix4x4 CalcMatrix(float eyeX, float eyeZ)
	{
		float boundX = Aspect * BoundY;
		float xx = eyeZ / boundX;
		float xz = eyeX / boundX;
		float nearZ = 0.5f * eyeZ;

		return new Matrix4x4(
			xx, 0, 0, 0,
			0, eyeZ / BoundY, 0, 0,
			-xz, 0, -1, -1,
			xz * ConvergenceZ, 0, nearZ, eyeZ
		);
	}

	public Matrix4x4 CalcMatrixTransposed(float eyeX, float eyeZ)
	{
		float boundX = Aspect * BoundY;
		float xx = eyeZ / boundX;
		float xz = eyeX / boundX;
		float nearZ = 0.5f * eyeZ;

		return new Matrix4x4(
			xx, 0, -xz, xz * ConvergenceZ,
			0, eyeZ / BoundY, 0, 0,
			0, 0, -1, nearZ,
			0, 0, -1, eyeZ
		);
	}
}
