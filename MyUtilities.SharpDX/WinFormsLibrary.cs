using System;
using System.Numerics;
using System.Windows.Forms;

using SharpDX.Windows;

namespace MyUtilities;

public class MyRenderControl : RenderControl
{
	public event MouseEventHandler MouseHWheel;

	protected override void WndProc(ref Message m)
	{
		if (m.Msg == 0x20E /*WM_MOUSEHWHEEL*/) {
			int delta = unchecked((int)(long)m.WParam >> 16);
			int x = unchecked((short)(long)m.LParam);
			int y = unchecked((int)(long)m.LParam >> 16);

			MouseHWheel?.Invoke(this, new MouseEventArgs(MouseButtons.None, 0, x, y, delta));
		}

		base.WndProc(ref m);
	}
}

public abstract class MouseRotation
{
	public float Sensitivity { get; set; } = 4.0f;
	public event Action MatrixChanged;

	protected readonly MyRenderControl control;
	protected System.Drawing.Point prev;

	public Matrix4x4 Matrix
	{
		get => matrix;
		set { matrix = value; MatrixChanged?.Invoke(); }
	}

	private Matrix4x4 matrix = Matrix4x4.Identity;

	public MouseRotation(MyRenderControl control)
	{
		this.control = control;
		control.MouseDown += MouseDown;
		control.MouseMove += MouseMove;
		control.MouseWheel += MouseWheel;
		control.MouseHWheel += MouseHWheel;
	}

	public void MouseDown(object sender, MouseEventArgs e)
	{
		prev = e.Location;
	}

	public void MouseMove(object sender, MouseEventArgs e)
	{
		if (e.Button != MouseButtons.Left) return;
		if (e.X == prev.X && e.Y == prev.Y) return;

		float mouseScale = Sensitivity / control.ClientSize.Height;

		HandleDelta((e.X - prev.X) * mouseScale, (e.Y - prev.Y) * mouseScale);

		prev = e.Location;
	}

	private void MouseWheel(object sender, MouseEventArgs e)
	{
		if ((Control.ModifierKeys & Keys.Control) == 0) {
			HandleDelta(0, e.Delta / 1200f);
		}
	}

	private void MouseHWheel(object sender, MouseEventArgs e)
	{
		HandleDelta(e.Delta / -1200f, 0);
	}

	protected abstract void HandleDelta(float dx, float dy);

	public abstract void SetIdentity();
}

public class TrackballRotation : MouseRotation
{
	public TrackballRotation(MyRenderControl control) : base(control) { }

	protected override void HandleDelta(float dx, float dy)
	{
		var delta = new Vector3(dy, dx, 0);
		float angle = delta.Length();
		if (angle == 0) return;

		Matrix *= Matrix4x4.CreateFromAxisAngle(delta / angle, angle);
	}

	public override void SetIdentity()
	{
		Matrix = Matrix4x4.Identity;
	}
}

public class PolarRotation : MouseRotation
{
	public float Theta { get; private set; }
	public float Phi { get; private set; }

	public PolarRotation(MyRenderControl control, float theta = 0, float phi = 0) : base(control)
	{
		Theta = theta;
		Phi = phi;

		UpdateMatrix();
	}

	protected override void HandleDelta(float dx, float dy)
	{
		Theta += dy;
		Phi += dx;

		UpdateMatrix();
	}

	public override void SetIdentity()
	{
		Theta = Phi = 0;
		Matrix = Matrix4x4.Identity;
	}

	public void UpdateMatrix()
	{
		Matrix = Matrix4x4.CreateRotationY(Phi) * Matrix4x4.CreateRotationX(Theta);
	}
}
