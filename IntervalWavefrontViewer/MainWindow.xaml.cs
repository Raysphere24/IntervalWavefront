#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;

//using SharpDX;
using SharpDX.Direct2D1;

using MyUtilities;

using static System.Linq.Enumerable;

using DW = SharpDX.DirectWrite;
using Controls = System.Windows.Controls;

using Matrix = System.Numerics.Matrix4x4;
using MathUtil = SharpDX.MathUtil;

namespace IntervalWavefront;

public partial class MainWindow : Window
{
	private readonly DispatcherTimer timer;
	private readonly WindowRenderTarget context;
	private readonly D2DRenderer d2dRenderer;
	private readonly Projection projection;
	private readonly PolarRotation rotation;
	private readonly LineBuffer shortestPathBuffer = new();
	private readonly LineBuffer pathBuffer = new();
	private readonly LineBuffer wavefrontBuffer = new();
	private readonly LineBuffer localCutLocusBuffer = new();
	private readonly LineBuffer globalCutLocusBuffer = new();
	private readonly List<SearchMesh> subdividedModels = new();
	private readonly List<Vector3> singularityPoints = new();
	private readonly Simulator simulator = new();
	private readonly Transformation transformation = new();

	private Matrix projectionMatrix;
	private SearchMesh mesh = null!;
	private MouseEventArgs? mouseDown;
	private Vector3 eyeInView;
	private Face? sourceFace;
	private DVector3 sourcePos;

	[STAThread]
	private static void Main()
	{
		try {
			new System.Windows.Application().Run(new MainWindow());
		}
		catch (Exception e) {
			System.Windows.MessageBox.Show(e.ToString());
		}
	}

	public MainWindow()
	{
		timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(0.125) };
		timer.Tick += (s, e) => SearchStep();

		InitializeComponent();

#if DEBUG
		if (SystemParameters.PrimaryScreenWidth >= 2560) {
			Width = SystemParameters.PrimaryScreenWidth * 0.625;
			Height = Width * 0.625;
			WindowStartupLocation = WindowStartupLocation.CenterScreen;
			WindowState = System.Windows.WindowState.Normal;
		}
#endif

		using Factory factory = new();
		context = new(factory, new(), new HwndRenderTargetProperties {
			Hwnd = RenderControl.Handle,
		});

		using DW.Factory dwFactory = new();
		DW.TextFormat textFormat = new(dwFactory, "Segoe UI", 18);

		d2dRenderer = new(context, textFormat, transformation);

		projection = new Projection { BoundY = 1.25f };

#if true
		rotation = new PolarRotation(RenderControl, MathUtil.Pi / 24, MathUtil.Pi / 24);
#else
		rotation = new TrackballRotation(RenderControl);
#endif
		var modelNameList = new[] {
			"Octahedron",
			"Icosahedron",
			"EllipticIcosahedron",
			"Ellipse",
			"Cube",
			"ConvexHullOfBunny",
			"ConvexHullOfElephantRotated",
		};

		foreach (string modelName in modelNameList)
			ModelListBox.Items.Add(modelName);

		ModelListBox.SelectedIndex = 0;

		rotation.MatrixChanged += RenderControl.Invalidate;
	}

	private void RenderControl_Resize(object sender, EventArgs e)
	{
		int width = RenderControl.ClientSize.Width;
		int height = RenderControl.ClientSize.Height;

		context.Resize(new(width, height));

		projection.Aspect = (float)width / height;

		OnProjArgsChanged();
	}

	private void OnProjArgsChanged()
	{
		eyeInView.Z = 8 * projection.BoundY;

		projectionMatrix = projection.CalcMatrix(0, eyeInView.Z);

		RenderControl.Invalidate();
	}

	private void RenderControl_Paint(object sender, PaintEventArgs e)
	{
		Matrix viewProjMatrix = mesh.ModelMatrix * rotation.Matrix * projectionMatrix;

		transformation.ViewProjMatrix = viewProjMatrix;
		transformation.Center = d2dRenderer.Size * 0.5f;

		d2dRenderer.SetLineWidth((float)LineWidthSlider.Value);

		var inverseViewMatrix = Matrix.Transpose(rotation.Matrix) * mesh.InverseModelMatrix;
		var eye = (DVector3)Vector3.Transform(eyeInView, inverseViewMatrix);
		mesh.UpdateIsFrontFacing(eye);

		context.BeginDraw();
		context.Clear(SharpDX.Color.Black);

		DrawUsingRenderer(d2dRenderer);

		context.EndDraw();
	}

	private void DrawUsingRenderer(IRenderer renderer)
	{
		if (DrawHiddenLinesCheckBox.IsChecked == true) {
			renderer.SetColor(0x7F7F7F, true);
			mesh.Draw(renderer, false);

			if (DimLocalCutLocusCheckBox.IsChecked == true) {
				renderer.SetColor(0x007F00, true);
				localCutLocusBuffer.Draw(renderer, false);

				renderer.SetColor(0x00FF00, true);
				globalCutLocusBuffer.Draw(renderer, false);
			}
			else {
				renderer.SetColor(0x003F00, true);
				globalCutLocusBuffer.Draw(renderer, false);
				localCutLocusBuffer.Draw(renderer, false);
			}

			renderer.SetColor(0x7F7F00, true);
			wavefrontBuffer.Draw(renderer, false);

			renderer.SetColor(0x007F7F, true);
			pathBuffer.Draw(renderer, false);

			renderer.SetColor(0x7F0000, true);
			shortestPathBuffer.Draw(renderer, false);
		}

		renderer.SetColor(0xFFFFFF, true);
		mesh.Draw(renderer, true);

		if (DimLocalCutLocusCheckBox.IsChecked == true) {
			renderer.SetColor(0x007F00, true);
			localCutLocusBuffer.Draw(renderer, true);

			renderer.SetColor(0x00FF00, true);
			globalCutLocusBuffer.Draw(renderer, true);
		}
		else {
			renderer.SetColor(0x00FF00, true);
			globalCutLocusBuffer.Draw(renderer, true);
			localCutLocusBuffer.Draw(renderer, true);
		}

		renderer.SetColor(0xFFFF00, true);
		wavefrontBuffer.Draw(renderer, true);

		renderer.SetColor(0x00FFFF, true);
		pathBuffer.Draw(renderer, true);

		renderer.SetColor(0xFF0000, true);
		shortestPathBuffer.Draw(renderer, true);

		DrawSprites(renderer);
	}

	private void DrawSprites(IRenderer renderer)
	{
		renderer.SetColor(0x00FF00, false);
		if (DrawIndicesCheckBox.IsChecked == true) {
			mesh.DrawIndices(renderer, true);
		}

		renderer.SetColor(0xFF0000, false);
		if (DrawSingularitiesCheckBox.IsChecked == true) {
			foreach (var p in singularityPoints) {
				renderer.DrawSprite(p, SpriteType.Circle);
			}
		}

		if (IntervalListBox.SelectedItem is EdgeInterval interval) {
			if (interval.Parent != null)
				renderer.DrawSprite((Vector3)interval.Center, SpriteType.Plus);
			if (interval.Event != null)
				renderer.DrawSprite((Vector3)interval.Event.Position, SpriteType.Square);
		}

		renderer.SetColor(0xFFFF00, false);
		if (DrawFarthestPointCheckBox.IsChecked == true && simulator.RidgeSink != null) {
			DVector3 position = simulator.RidgeSink.Position;
			renderer.DrawSprite((Vector3)position, SpriteType.Circle);
		}

		if (sourceFace != null) {
			renderer.DrawSprite((Vector3)sourcePos, SpriteType.Cross);
		}
	}

	private void InitializeAndSearch(int numSteps)
	{
		try {
			if (sourceFace == null) return;

			timer.Stop();

			ExceptionTextBlock.Text = null;

			simulator.Initialize(mesh, sourceFace, sourcePos);
			simulator.SearchStep(numSteps);

			UpdateForSearch();
		}
		catch (Exception ex) { HandleException(ex); }
	}

	private void SearchInitialize()
	{
		if (sourceFace == null) return;

		if (ImmediateModeRadioButton.IsChecked == true) {
			InitializeAndSearch(int.MaxValue);
			return;
		}

		InitializeAndSearch(0);

		if (AnimationModeRadioButton.IsChecked == true) {
			timer.Start();
		}
	}

	private void SearchStep()
	{
		try {
			if (simulator.Queue.IsEmpty) {
				if (simulator.RidgeSink == null)
					SearchInitialize();
				return;
			}

			int numSteps = 1;
			if (ImmediateModeRadioButton.IsChecked == true)
				numSteps = int.MaxValue;
			else if (FastModeCheckBox.IsChecked == true) {
				if (StepModeRadioButton.IsChecked == true)
					numSteps = 10;
				if (AnimationModeRadioButton.IsChecked == true)
					numSteps = -1;
			}

			simulator.DoNotTrim = DoNotTrimCheckBox.IsChecked!.Value;

			if (simulator.SearchStep(numSteps)) {
				timer.Stop();
			}

			UpdateForSearch();
		}
		catch (Exception ex) { HandleException(ex); }
	}

	private void UpdateForSearch()
	{
		try {
			//shortestPathBuffer.Clear();
			//pathBuffer.Clear();

			StepCountLabel.Content = simulator.StepCount;

			EventCountListView.Items.Clear();

			foreach (var item in simulator.EventCount) {
				EventCountListView.Items.Add(new { item.Key.Name, item.Value });
			}

			HistoryListBox.ItemsSource = null;
			HistoryListBox.ItemsSource = simulator.History;

			IntervalListBox.Items.Clear();

			if (simulator.Queue.IsEmpty) {
				RadiusSlider.Minimum = simulator.Radius;
				RadiusSlider.Maximum = simulator.Radius;
			}
			else {
				var nextEvent = simulator.Queue.Top;
				var interval = nextEvent.Interval;

				do {
					IntervalListBox.Items.Add(interval);

					interval = interval.Next;
				} while (interval != nextEvent.Interval);

				//IntervalListBox.SelectedIndex = 0;

				DebugHelper.Assert(!double.IsNaN(nextEvent.Priority));

				RadiusSlider.Minimum = simulator.Radius;
				RadiusSlider.Maximum = nextEvent.Priority;
				RadiusSlider.Value = nextEvent.Priority;
			}
		}
		catch (Exception ex) { HandleException(ex); }

		//UpdateForUI();

		RenderControl.Invalidate();
	}

	private void UpdateForUI()
	{
		wavefrontBuffer.Clear();
		localCutLocusBuffer.Clear();
		globalCutLocusBuffer.Clear();
		singularityPoints.Clear();

		simulator.RidgeSink?.AddToBuffer(localCutLocusBuffer, globalCutLocusBuffer);

		double r = RadiusSlider.Value;

		if (!simulator.Queue.IsEmpty) {
			var nextEvent = simulator.Queue.Top;
			var i = nextEvent.Interval;

			do {
				try {
					if (i.Parent != i.Prev.Parent || i.Parent == null)
						i.AddWavefrontToBuffer(wavefrontBuffer, r);

					if (i.Ridge != null) {
						DVector3 position = EdgeInterval.CalcArcBoundary(i.Prev, i, r);
						singularityPoints.Add((Vector3)position);
						i.Ridge.AddToBuffer(localCutLocusBuffer, globalCutLocusBuffer, position);
					}
				}
				catch (Exception ex) { HandleException(ex); }

				i = i.Next;
			} while (i != nextEvent.Interval);
		}

		var interval = (EdgeInterval?)IntervalListBox.SelectedItem;

		shortestPathBuffer.Clear();
		pathBuffer.Clear();

		if (interval != null) {
			try {
				interval.AddWavefrontToBuffer(shortestPathBuffer, r);
				interval.AddExtentToBuffer(shortestPathBuffer, interval.Extent);
				// interval.AddExtentToBuffer(pathBuffer, interval.OriginalExtent);
			}
			catch (Exception ex) { HandleException(ex); }
		}

		RenderControl.Invalidate();
	}

	private void UpdatePath(HitTestRay ray)
	{
		pathBuffer.Clear();
		shortestPathBuffer.Clear();

		if (ray.Face == null) return;

		Face face = ray.Face;
		DVector3 pos = ray.Position;

		Segment? nearest = simulator.Segments.GetItems(face.Index).ArgMin(u => u.Distance(pos));

		//IntervalListBox.SelectedItem = nearest;

		if (nearest == null) return;

		Segment.AddPathToBuffer(nearest, shortestPathBuffer, pos);

		foreach (var segment in simulator.Segments.GetItems(face.Index)) {
			if (segment != nearest) {
				Segment.AddPathToBuffer(segment, pathBuffer, pos);
			}
		}

		RenderControl.Invalidate();
	}

	private void HandleException(Exception ex)
	{
		ExceptionTextBlock.Text = ex.ToString();
		ExceptionTabItem.Focus();
		timer.Stop();
	}

	private void RadiusSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		UpdateForUI();
	}

	private void IntervalListBox_SelectionChanged(object sender, Controls.SelectionChangedEventArgs e)
	{
		UpdateForUI();

		PropertiesListView.Items.Clear();

		var i = (EdgeInterval?)IntervalListBox.SelectedItem;
		if (i == null) return;

		PropertiesListView.Items.Add(new { Property = "Edge", Value = i.Edge });
		PropertiesListView.Items.Add(new { Property = "Extent", Value = i.Extent });
		PropertiesListView.Items.Add(new { Property = "Parent.Edge", Value = i.Parent?.Edge });
		PropertiesListView.Items.Add(new { Property = "Event", Value = i.Event?.Name });
		PropertiesListView.Items.Add(new { Property = "Event.Priority", Value = i.Event?.Priority });
		PropertiesListView.Items.Add(new { Property = nameof(i.IsPropagated), Value = i.IsPropagated });
		PropertiesListView.Items.Add(new { Property = nameof(i.IsCrossed), Value = i.IsCrossed });
		PropertiesListView.Items.Add(new { Property = nameof(i.Ridge), Value = i.Ridge });
	}

	private void HistoryListBox_SelectionChanged(object sender, Controls.SelectionChangedEventArgs e)
	{
		if (HistoryListBox.SelectedIndex >= 0) {
			InitializeAndSearch(HistoryListBox.SelectedIndex);
		}
	}

	private void OnModelChanged()
	{
		sourceFace = null;

		shortestPathBuffer?.Clear();
		pathBuffer?.Clear();
		wavefrontBuffer?.Clear();
		localCutLocusBuffer?.Clear();
		globalCutLocusBuffer?.Clear();

		singularityPoints.Clear();
		IntervalListBox.Items.Clear();

		simulator.RootSegment = null;
		simulator.RidgeSink = null;

		VertexCountLabel.Content = mesh.Vertices.Length;
		FaceCountLabel.Content = mesh.Faces.Length;
	}

	private void LoadFile(string filename)
	{
		timer.Stop();

		subdividedModels.Clear();

		mesh = SearchMesh.CreateFromTxtFile(filename);
		OnModelChanged();

		subdividedModels.Add(mesh);

		SubdivideStepperPanel.Value = 0;

		OnProjArgsChanged();
	}

	private void ModelListBox_SelectionChanged(object sender, Controls.SelectionChangedEventArgs e)
	{
		string modelName = (string)ModelListBox.SelectedItem;
		string filename = @"..\..\..\..\ModelData\" + modelName + ".txt";
		LoadFile(filename);
	}

	private void SubdivideStepperPanel_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		int subdivideLevel = (int)e.NewValue;

		while (subdividedModels.Count <= subdivideLevel)
			subdividedModels.Add(subdividedModels.Last().CreateSubdividedMesh());

		mesh = subdividedModels[subdivideLevel];
		OnModelChanged();

		OnProjArgsChanged();
	}

	private void MoveSelectedInterval(bool next)
	{
		if (IntervalListBox.Items.IsEmpty) return;

		var interval = (EdgeInterval?)IntervalListBox.SelectedItem;

		if (interval == null)
			IntervalListBox.SelectedItem = IntervalListBox.Items[0];
		else
			IntervalListBox.SelectedItem = next ? interval.Next : interval.Prev;
	}

	private void ReadSourceInformationTextBox()
	{
		try {
			string text = SourceInformationTextBox.Text;
			string[] s = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
			sourcePos.X = double.Parse(s[0]);
			sourcePos.Y = double.Parse(s[1]);
			sourcePos.Z = double.Parse(s[2]);
			sourceFace = mesh.Faces[int.Parse(s[3])];
		}
		catch (Exception ex) { HandleException(ex); }
	}

	private void NextOrPauseButton_Click(object sender, RoutedEventArgs e)
	{
		if (StepModeRadioButton.IsChecked == true)
			SearchStep();
		else
			timer.IsEnabled = !timer.IsEnabled;
	}

	private void BackButton_Click(object sender, RoutedEventArgs e)
	{
		if (simulator.StepCount > 0) {
			InitializeAndSearch(simulator.StepCount - 1);
		}
	}

	private void RestartButton_Click(object sender, RoutedEventArgs e)
	{
		SearchInitialize();
		RenderControl.Focus();
	}

	private void CopySourceInformationButton_Click(object sender, RoutedEventArgs e)
	{
		SourceInformationTextBox.SelectAll();
		SourceInformationTextBox.Copy();
	}

	private void PasteSourceInformationButton_Click(object sender, RoutedEventArgs e)
	{
		SourceInformationTextBox.SelectAll();
		SourceInformationTextBox.Paste();
		ReadSourceInformationTextBox();
		SearchInitialize();
	}

	private void SaveToSVG_Click(object sender, RoutedEventArgs e)
	{
		var dialog = new Microsoft.Win32.SaveFileDialog {
			FileName = (string)ModelListBox.SelectedItem,
			DefaultExt = ".svg",
			Filter = "SVG files (*.svg)|*.svg|All files (*.*)|*.*",
		};

		if (dialog.ShowDialog() != true) return;

		using SvgRenderer svgRenderer = new(dialog.FileName, transformation, LineWidthSlider.Value);

		DrawUsingRenderer(svgRenderer);
	}

	private void SaveUnfoldingToSVG_Click(object sender, RoutedEventArgs e)
	{
		if (simulator.RootSegment == null) {
			System.Windows.MessageBox.Show("Cut locus is not computed");
			return;
		}

		var dialog = new Microsoft.Win32.SaveFileDialog {
			FileName = (string)ModelListBox.SelectedItem + "_unfolding",
			DefaultExt = ".svg",
			Filter = "SVG files (*.svg)|*.svg|All files (*.*)|*.*",
		};

		if (dialog.ShowDialog() != true) return;

		simulator.RootSegment.ComputeUnfolding().WriteTo(dialog.FileName, simulator.Radius);
	}

	private void RenderControl_MouseDown(object sender, MouseEventArgs e)
	{
		mouseDown = e;
	}

	private void Window_DragEnter(object sender, System.Windows.DragEventArgs e)
	{
		if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
			e.Effects = System.Windows.DragDropEffects.Copy;
	}

	private void Window_Drop(object sender, System.Windows.DragEventArgs e)
	{
		try {
			LoadFile(((string[])e.Data.GetData(System.Windows.DataFormats.FileDrop))[0]);
		}
		catch (Exception ex) { HandleException(ex); }
	}

	private void RenderControl_MouseMove(object sender, MouseEventArgs e)
	{
		if ((Control.ModifierKeys & Keys.Shift) != 0 && simulator.RootSegment != null) {
			UpdatePath(CastRayFromMousePos(e));
		}
	}

	private void RenderControl_MouseUp(object sender, MouseEventArgs e)
	{
		if (e.Button == MouseButtons.Left && e.Location == mouseDown!.Location) {
			HitTestRay ray = CastRayFromMousePos(e);

			if (ray.Face == null) return;

			Face f = ray.Face;
			DVector3 p = ray.Position;

			sourceFace = f;
			sourcePos = p;

			SourceInformationTextBox.Text = p.ToString("F10") + " " + mesh.Faces.IndexOf(f);
			SearchInitialize();
		}
	}

	private void RenderControl_MouseWheel(object sender, MouseEventArgs e)
	{
		if ((Control.ModifierKeys & Keys.Control) != 0) {
			projection.BoundY *= 1 - e.Delta / 1200f;
			rotation.Sensitivity = 1.25f * projection.BoundY;
			OnProjArgsChanged();
		}
	}

	protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
	{
		if (e.Key == System.Windows.Input.Key.Escape)
			Close();
		else if (e.Key == System.Windows.Input.Key.Enter)
			SearchStep();
		else if (e.Key == System.Windows.Input.Key.Left)
			MoveSelectedInterval(next: false);
		else if (e.Key == System.Windows.Input.Key.Right)
			MoveSelectedInterval(next: true);

		e.Handled = true;
	}

	private HitTestRay CastRayFromMousePos(MouseEventArgs e)
	{
		float centerX = RenderControl.Width / 2f;
		float centerY = RenderControl.Height / 2f;

		var posInView = new Vector3(e.X - centerX, centerY - e.Y, 0);
		posInView *= projection.BoundY / centerY;
		posInView *= (eyeInView.Z - projection.ConvergenceZ) / eyeInView.Z;
		posInView.Z += projection.ConvergenceZ;

		var inverseViewMatrix = Matrix.Transpose(rotation.Matrix) * mesh.InverseModelMatrix;
		var eye = Vector3.Transform(eyeInView, inverseViewMatrix);
		var pos = Vector3.Transform(posInView, inverseViewMatrix);

		var ray = new HitTestRay((DVector3)eye, (DVector3)(pos - eye));

		mesh.IntersectFaces(ray);

		return ray;
	}

	private void OnParameterChanged(object sender, RoutedEventArgs e)
	{
		RenderControl?.Invalidate();
	}

	private void OnProjArgChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		OnProjArgsChanged();
	}
}
