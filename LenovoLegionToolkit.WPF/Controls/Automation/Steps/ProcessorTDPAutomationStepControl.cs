using LenovoLegionToolkit.Lib.Automation.Steps;
using LenovoLegionToolkit.Lib;
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Common;
using NumberBox = Wpf.Ui.Controls.NumberBox;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Controllers;
using System.Linq;

namespace LenovoLegionToolkit.WPF.Controls.Automation.Steps;

public class ProcessorTDPAutomationStepControl : AbstractAutomationStepControl<ProcessorTDPAutomationStep>
{
    private readonly ProcessorController _controller = IoCContainer.Resolve<ProcessorController>();

    private readonly NumberBox _stapm = new()
    {
        PlaceholderText = "Stapm limit",
        Width = 150,
        Maximum = 120,
        Minimum = 0,
        MaxDecimalPlaces = 0,
        SmallChange = 1,
        LargeChange = 5
    };

    private readonly NumberBox _fast = new()
    {
        PlaceholderText = "Fast limit",
        Width = 150,
        Maximum = 120,
        Minimum = 0,
        MaxDecimalPlaces = 0,
        SmallChange = 1,
        LargeChange = 5
    };

    private readonly NumberBox _slow = new()
    {
        PlaceholderText = "Slow limit",
        Width = 150,
        Maximum = 120,
        Minimum = 0,
        MaxDecimalPlaces = 0,
        SmallChange = 1,
        LargeChange = 5
    };

    private readonly CheckBox _useMSR = new()
    {
        Content = "Use MSR Limits?",
        HorizontalContentAlignment = HorizontalAlignment.Right,
        IsChecked = false
    };

    private readonly CheckBox _maintainTDP = new()
    {
        Content = "Maintain TDP Limits?",
        HorizontalContentAlignment = HorizontalAlignment.Right,
        IsChecked = false
    };

    private readonly NumberBox _interval = new()
    {
        PlaceholderText = "Interval",
        Width = 150,
        Maximum = 120,
        Minimum = 0,
        MaxDecimalPlaces = 0,
        SmallChange = 1,
        LargeChange = 5
    };

    private readonly StackPanel _stackPanel = new();

    private ProcessorTDPState _state;

    public ProcessorTDPAutomationStepControl(ProcessorTDPAutomationStep step) : base(step)
    {
        Icon = SymbolRegular.DeveloperBoardLightning20;
        Title = "Processor TDP";
        Subtitle = "Change the TDP limits of the processor.\n\nNOTE: This action uses RyzenAdj for AMD,\nand KX utility for Intel.";
    }

    public override IAutomationStep CreateAutomationStep()
    {
        _state.Stapm = _stapm.Value is null ? 0 : (int)_stapm.Value;
        _state.Fast = _fast.Value is null ? 0 : (int)_fast.Value;
        _state.Slow = _slow.Value is null ? 0 : (int)_slow.Value;
        _state.UseMSR = _useMSR.IsChecked == null ? false : (bool)_useMSR.IsChecked;
        _state.MaintainTDP = _maintainTDP.IsChecked == null ? false : (bool)_maintainTDP.IsChecked;
        _state.Interval = _interval.Value is null ? 0 : (int)_interval.Value;
        return new ProcessorTDPAutomationStep(_state);
    }

    protected override UIElement? GetCustomControl()
    {
        ProcessorController processor = _controller.GetCurrent();
        if (processor.GetType() == typeof(AMDProcessorController))
        {
            _stapm.ValueChanged += (_, _) =>
            {
                if (_stapm.Value != AutomationStep.State.Stapm)
                    RaiseChanged();
            };

            _stackPanel.Children.Add(_stapm);
        }

        _fast.ValueChanged += (_, _) =>
        {
            if (_fast.Value != AutomationStep.State.Fast)
                RaiseChanged();
        };

        _slow.ValueChanged += (_, _) =>
        {
            if (_slow.Value != AutomationStep.State.Slow)
                RaiseChanged();
        };

        if (processor.GetType() == typeof(IntelProcessorController))
        {
            _useMSR.Checked += (_, _) =>
            {
                if (_useMSR.IsChecked != AutomationStep.State.UseMSR)
                    RaiseChanged();
            };

            _stackPanel.Children.Add(_useMSR);
        }

        _maintainTDP.Checked += (_, _) =>
        {
            if (_maintainTDP.IsChecked != AutomationStep.State.MaintainTDP)
                RaiseChanged();
        };

        _interval.ValueChanged += (_, _) =>
        {
            if (_interval.Value != AutomationStep.State.Interval)
                RaiseChanged();
        };

        _stackPanel.Children.Add(_fast);
        _stackPanel.Children.Add(_slow);
        _stackPanel.Children.Add(_maintainTDP);
        _stackPanel.Children.Add(_interval);

        return _stackPanel;
    }

    protected override async Task RefreshAsync() {
        var state = await AutomationStep.GetAllStatesAsync();
        var stateList = state.ToList();

        _stapm.Value = AutomationStep.State.Stapm;
        _fast.Value = AutomationStep.State.Fast;
        _slow.Value = AutomationStep.State.Slow;
        _useMSR.IsChecked = AutomationStep.State.UseMSR;
        _maintainTDP.IsChecked = AutomationStep.State.MaintainTDP;
        _interval.Value = AutomationStep.State.Interval;
    }

    protected override void OnFinishedLoading() { }
}
