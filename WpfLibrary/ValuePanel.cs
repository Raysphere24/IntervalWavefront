using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace MyUtilities.WPF;

public class ValuePanel<T> : Grid where T : RangeBase, new()
{
	public Label CaptionLabel { get; }
	public Label ValueLabel { get; }
	public T RangeControl { get; }

	public object Caption
	{
		get => CaptionLabel.Content;
		set => CaptionLabel.Content = value;
	}

	public string Format
	{
		get => ValueLabel.ContentStringFormat;
		set => ValueLabel.ContentStringFormat = value;
	}

	public double Minimum
	{
		get => RangeControl.Minimum;
		set => RangeControl.Minimum = value;
	}

	public double Maximum
	{
		get => RangeControl.Maximum;
		set => RangeControl.Maximum = value;
	}

	public double Value
	{
		get => RangeControl.Value;
		set => RangeControl.Value = value;
	}

	public double LargeChange
	{
		get => RangeControl.LargeChange;
		set => RangeControl.LargeChange = value;
	}

	public double SmallChange
	{
		get => RangeControl.SmallChange;
		set => RangeControl.SmallChange = value;
	}

	public event RoutedPropertyChangedEventHandler<double> ValueChanged;

	public ValuePanel()
	{
		ColumnDefinitions.Add(new ColumnDefinition());
		ColumnDefinitions.Add(new ColumnDefinition());

		Children.Add(CaptionLabel = new Label());
		Children.Add(ValueLabel = new Label { HorizontalContentAlignment = HorizontalAlignment.Right });
		Children.Add(RangeControl = new T());

		SetColumn(CaptionLabel, 0);
		SetColumn(ValueLabel, 0);
		SetColumn(RangeControl, 1);

		RangeControl.ValueChanged += RangeControl_ValueChanged;
	}

	private void RangeControl_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		ValueLabel.Content = e.NewValue;
		ValueChanged?.Invoke(this, e);
	}
}

public class SliderPanel : ValuePanel<Slider>
{
	public bool IsMoveToPointEnabled
	{
		get => RangeControl.IsMoveToPointEnabled;
		set => RangeControl.IsMoveToPointEnabled = value;
	}

	public bool IsSnapToTickEnabled
	{
		get => RangeControl.IsSnapToTickEnabled;
		set => RangeControl.IsSnapToTickEnabled = value;
	}

	public double TickFrequency
	{
		get => RangeControl.TickFrequency;
		set => RangeControl.TickFrequency = value;
	}

	public TickPlacement TickPlacement
	{
		get => RangeControl.TickPlacement;
		set => RangeControl.TickPlacement = value;
	}

	public SliderPanel()
	{
		RangeControl.Minimum = 0;
		RangeControl.Maximum = 1;

		RangeControl.IsMoveToPointEnabled = true;
	}
}
