using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using LenovoLegionToolkit.Lib.Automation.Steps;
using LenovoLegionToolkit.WPF.Resources;
using Wpf.Ui.Common;
using NumberBox = Wpf.Ui.Controls.NumberBox;

namespace LenovoLegionToolkit.WPF.Controls.Automation.Steps;

public class DisplayBrightnessAutomationStepControl : AbstractAutomationStepControl<DisplayBrightnessAutomationStep>
{
    private readonly NumberBox _brightness = new()
    {
        Width = 150,
        ClearButtonEnabled = false,
        MaxDecimalPlaces = 0,
        Minimum = 0,
        Maximum = 100,
        SmallChange = 5,
        LargeChange = 5
    };

    private readonly Grid _grid = new();

    public DisplayBrightnessAutomationStepControl(DisplayBrightnessAutomationStep step) : base(step)
    {
        Icon = SymbolRegular.BrightnessHigh48;
        Title = Resource.DisplayBrightnessAutomationStepControl_Title;
        Subtitle = Resource.DisplayBrightnessAutomationStepControl_Message;
    }

    private void DisplayBrightnessAutomationStepControl_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!e.WidthChanged)
            return;

        var newWidth = e.NewSize.Width / 3;
        _brightness.Width = newWidth;
    }

    public override IAutomationStep CreateAutomationStep() => new DisplayBrightnessAutomationStep(_brightness.Value is null ? 0 : (int)_brightness.Value);

    protected override UIElement GetCustomControl()
    {
        _brightness.ValueChanged += (_, _) =>
        {
            if (_brightness.Value != AutomationStep.Brightness)
                RaiseChanged();
        };

        _grid.Children.Add(_brightness);

        return _grid;
    }

    protected override void OnFinishedLoading() { }

    protected override Task RefreshAsync()
    {
        _brightness.Value = AutomationStep.Brightness;
        return Task.CompletedTask;
    }
}
