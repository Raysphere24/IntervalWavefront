#nullable enable

using System;
using System.IO;
using System.Numerics;

namespace MyUtilities;

public class Transformation
{
	public Matrix4x4 ViewProjMatrix;
	public Vector2 Center;

	public (float, float) Apply(Vector3 p)
	{
		var a = new Vector4(p.X, p.Y, p.Z, 1);
		var b = Vector4.Transform(a, ViewProjMatrix);
		return (Center.X * (1 + b.X / b.W), Center.Y * (1 - b.Y / b.W));
	}
}

public enum SpriteType { Circle, Cross, Plus, Square }

public interface IRenderer
{
	public void SetColor(uint color, bool isLineGroup);

	public void DrawSprite(Vector3 position, SpriteType type);
	public void DrawLine(Vector3 p, Vector3 q);
	public void DrawText(Vector3 position, string text);
}

public class SvgRenderer : IRenderer, IDisposable
{
	private readonly StreamWriter stream;
	private readonly Transformation transformation;
	private bool isInsideGroup;

	public SvgRenderer(string filename, Transformation transformation, double lineWidth)
	{
		stream = new StreamWriter(filename) { NewLine = "\n" };

		this.transformation = transformation;

		float w = transformation.Center.X * 2;
		float h = transformation.Center.Y * 2;

		stream.WriteLine("<?xml version='1.0'?>");
		stream.WriteLine(
			$"<svg viewBox='0 0 {w} {h}' stroke-width='{lineWidth}' xmlns='http://www.w3.org/2000/svg'>");
		stream.WriteLine($"<rect x='0' y='0' width='{w}' height='{h}'/>");
	}

	public void Dispose()
	{
		if (isInsideGroup)
			stream.WriteLine("</g>");

		stream.WriteLine("</svg>");

		stream.Dispose();
	}

	public void DrawLine(Vector3 p, Vector3 q)
	{
		var (x_1, y_1) = transformation.Apply(p);
		var (x_2, y_2) = transformation.Apply(q);

		stream.WriteLine($"<line x1='{x_1}' y1='{y_1}' x2='{x_2}' y2='{y_2}'/>");
	}

	public void DrawSprite(Vector3 position, SpriteType type)
	{
		var (x, y) = transformation.Apply(position);

		if (type == SpriteType.Circle) {
			stream.WriteLine($"<circle cx='{x}' cy='{y}' r='5'/>");
		}

		if (type == SpriteType.Cross) {
			const float c = 5;

			stream.WriteLine($"<line x1='{x - c}' y1='{y - c}' x2='{x + c}' y2='{y + c}'/>");
			stream.WriteLine($"<line x1='{x + c}' y1='{y - c}' x2='{x - c}' y2='{y + c}'/>");
		}

		if (type == SpriteType.Plus) {
			const float c = 5 * 1.41421356f;

			stream.WriteLine($"<line x1='{x - c}' y1='{y}' x2='{x + c}' y2='{y}'/>");
			stream.WriteLine($"<line x1='{x}' y1='{y - c}' x2='{x}' y2='{y + c}'/>");
		}

		if (type == SpriteType.Square) {
			const float c = 5;

			stream.WriteLine($"<rect x='{x - c}' y='{y - c}' width='{2 * c}' height='{2 * c}'/>");
		}
	}

	public void SetColor(uint color, bool isLineGroup)
	{
		if (isInsideGroup)
			stream.WriteLine("</g>");

		string hex = color.ToString("X6");

		if (isLineGroup)
			stream.WriteLine($"<g fill='none' stroke='#{hex}'>");
		else
			stream.WriteLine($"<g fill='#{hex}' stroke='#{hex}'>");

		isInsideGroup = true;
	}

	public void DrawText(Vector3 position, string text)
	{
		var (x, y) = transformation.Apply(position);

		stream.WriteLine($"<text x='{x}' y='{y}'>{text}</text>");
	}
}
