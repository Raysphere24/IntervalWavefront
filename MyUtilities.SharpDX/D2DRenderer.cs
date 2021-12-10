#nullable enable

using SharpDX;
using SharpDX.Direct2D1;

using DW = SharpDX.DirectWrite;

using Vector3 = System.Numerics.Vector3;

namespace MyUtilities;

public class D2DRenderer : IRenderer
{
	private readonly RenderTarget context;
	private readonly DW.TextFormat textFormat;

	private readonly Transformation transformation;
	private SolidColorBrush? brush;
	private float lineWidth = 2;

	public System.Numerics.Vector2 Size => new(context.Size.Width, context.Size.Height);

	public D2DRenderer(RenderTarget context, DW.TextFormat textFormat, Transformation transformation)
	{
		this.context = context;
		this.textFormat = textFormat;
		this.transformation = transformation;
	}

	public void Dispose()
	{
		brush?.Dispose();
	}

	public void SetColor(uint color, bool isLineGroup)
	{
		brush?.Dispose();
		brush = new SolidColorBrush(context, Color.FromBgra(color | 0xFF000000));
	}

	public void SetLineWidth(float width)
	{
		lineWidth = width;
	}

	public void DrawSprite(Vector3 position, SpriteType type)
	{
		var (x, y) = transformation.Apply(position);

		if (type == SpriteType.Circle) {
			const float c = 5;

			var ellipse = new Ellipse { Point = new(x, y), RadiusX = c, RadiusY = c };
			context.FillEllipse(ellipse, brush);
		}

		if (type == SpriteType.Cross) {
			const float c = 5;

			context.DrawLine(new(x - c, y - c), new(x + c, y + c), brush, lineWidth);
			context.DrawLine(new(x + c, y - c), new(x - c, y + c), brush, lineWidth);
		}

		if (type == SpriteType.Plus) {
			const float c = 5 * 1.41421356f;

			context.DrawLine(new(x - c, y), new(x + c, y), brush, lineWidth);
			context.DrawLine(new(x, y - c), new(x, y + c), brush, lineWidth);
		}

		if (type == SpriteType.Square) {
			const float c = 5;

			var rectangle = new RectangleF(x - c, y - c, 2 * c, 2 * c);
			context.FillRectangle(rectangle, brush);
		}
	}

	public void DrawLine(Vector3 p, Vector3 q)
	{
		var (x_1, y_1) = transformation.Apply(p);
		var (x_2, y_2) = transformation.Apply(q);

		context.DrawLine(new(x_1, y_1), new(x_2, y_2), brush, lineWidth);
	}

	public void DrawText(Vector3 position, string text)
	{
		var (x, y) = transformation.Apply(position);
		var rect = new RectangleF(x, y, 64, 0);

		context.DrawText(text, textFormat, rect, brush);
	}
}
