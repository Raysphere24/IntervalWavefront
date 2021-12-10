using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace MyUtilities.WPF;

internal class StepperCell : UniformGrid
{
	public StepperCell()
	{
		Columns = 2; Rows = 1;

		var dnButton = new Button { Content = "▼" };
		var upButton = new Button { Content = "▲" };

		Children.Add(dnButton);
		Children.Add(upButton);
	}
}

public class Stepper : RangeBase
{
	static Stepper()
	{
		var template = new ControlTemplate();
		template.VisualTree = new FrameworkElementFactory(typeof(StepperCell));

		TemplateProperty.OverrideMetadata(typeof(Stepper), new FrameworkPropertyMetadata(template));
	}

	public override void OnApplyTemplate()
	{
		var cell = (Panel)GetVisualChild(0);

		var dnButton = (Button)cell.Children[0];
		var upButton = (Button)cell.Children[1];

		dnButton.Click += (s, e) => Value -= LargeChange;
		upButton.Click += (s, e) => Value += LargeChange;
	}
}

public class StepperPanel : ValuePanel<Stepper> { }
