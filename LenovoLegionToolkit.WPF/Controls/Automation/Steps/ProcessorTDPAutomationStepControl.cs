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
        Max = 120,
        Min = 0,
        DecimalPlaces = 0,
        IntegersOnly = true
    };

    private readonly NumberBox _fast = new()
    {
        PlaceholderText = "Fast limit",
        Width = 150,
        Max = 120,
        Min = 0,
        DecimalPlaces = 0,
        IntegersOnly = true
    };

    private readonly NumberBox _slow = new()
    {
        PlaceholderText = "Slow limit",
        Width = 150,
        Max = 120,
        Min = 0,
        DecimalPlaces = 0,
        IntegersOnly = true
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
        Max = 120,
        Min = 0,
        DecimalPlaces = 0,
        IntegersOnly = true
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
        _state.Stapm = _stapm.Value;
        _state.Fast = _fast.Value;
        _state.Slow = _slow.Value;
        _state.UseMSR = _useMSR.IsChecked == null ? false : (bool)_useMSR.IsChecked;
        _state.MaintainTDP = _maintainTDP.IsChecked == null ? false : (bool)_maintainTDP.IsChecked;
        _state.Interval = (int)_interval.Value;
        return new ProcessorTDPAutomationStep(_state);
    }

    protected override UIElement? GetCustomControl()
    {
        ProcessorController processor = _controller.GetCurrent();
        if (processor.GetType() == typeof(AMDProcessorController))
        {
            _stapm.TextChanged += (_, _) =>
            {
                if (_stapm.Text != AutomationStep.State.Stapm.ToString())
                    RaiseChanged();
            };

            _stackPanel.Children.Add(_stapm);
        }

        _fast.TextChanged += (_, _) =>
        {
            if (_fast.Text != AutomationStep.State.Fast.ToString())
                RaiseChanged();
        };

        _slow.TextChanged += (_, _) =>
        {
            if (_slow.Text != AutomationStep.State.Slow.ToString())
                RaiseChanged();
        };

        if (processor.GetType() == typeof(IntelProcessorController))
        {
            //_useMSR.IsChecked = false;
            _useMSR.Checked += (_, _) =>
            {
                if (_useMSR.IsChecked != AutomationStep.State.UseMSR)
                    RaiseChanged();
            };

            _stackPanel.Children.Add(_useMSR);
        }

        //_maintainTDP.IsChecked = false;
        _maintainTDP.Checked += (_, _) =>
        {
            if (_maintainTDP.IsChecked != AutomationStep.State.MaintainTDP)
                RaiseChanged();
        };

        _interval.TextChanged += (_, _) =>
        {
            if (_interval.Text != AutomationStep.State.Interval.ToString())
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


        _stapm.Text = AutomationStep.State.Stapm.ToString();
        _fast.Text = AutomationStep.State.Fast.ToString();
        _slow.Text = AutomationStep.State.Slow.ToString();
        _useMSR.IsChecked = AutomationStep.State.UseMSR;
        _maintainTDP.IsChecked = AutomationStep.State.MaintainTDP;
        _interval.Text = AutomationStep.State.Interval.ToString();

        //if ((bool)_maintainTDP.IsChecked)
        //    _interval.IsEnabled = true;
        //else
        //    _interval.IsEnabled = false;

    }

    protected override void OnFinishedLoading() { }
}
