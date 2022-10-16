using LenovoLegionToolkit.Lib.Automation.Steps;
using LenovoLegionToolkit.Lib;
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Common;
using NumberBox = Wpf.Ui.Controls.NumberBox;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Controllers;

namespace LenovoLegionToolkit.WPF.Controls.Automation.Steps
{
    public class ProcessorTDPAutomationStepControl : AbstractAutomationStepControl<ProcessorTDPAutomationStep>
    {
        private readonly ProcessorController _controller = IoCContainer.Resolve<ProcessorController>();

        private readonly NumberBox _stapm = new()
        {
            PlaceholderText = "Stapm limit",
            Width = 150,
            Max = 120,
            Min = 0,
            Value = 25,
            DecimalPlaces = 0
        };

        private readonly NumberBox _fast = new()
        {
            PlaceholderText = "Fast limit",
            Width = 150,
            Max = 120,
            Min = 0,
            Value = 25,
            DecimalPlaces = 0
        };

        private readonly NumberBox _slow = new()
        {
            PlaceholderText = "Slow limit",
            Width = 150,
            Max = 120,
            Min = 0,
            Value = 15,
            DecimalPlaces = 0
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
            Value = 5,
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
            _state.UseMSR = (bool)_useMSR.IsChecked;
            _state.MaintainTDP = (bool)_maintainTDP.IsChecked;
            _state.Interval = (int)_interval.Value;
            return new ProcessorTDPAutomationStep(_state);
        }

        protected override UIElement? GetCustomControl()
        {
            ProcessorController processor = _controller.GetCurrent();
            if (processor.GetType() == typeof(AMDProcessorController))
            {
                _stapm.TextChanged += (s, e) =>
                {
                    if (_stapm.Value != AutomationStep.State.Stapm)
                        RaiseChanged();
                };

                _stackPanel.Children.Add(_stapm);
            }

            _fast.TextChanged += (s, e) =>
            {
                if (_fast.Value != AutomationStep.State.Fast)
                    RaiseChanged();
            };

            _stackPanel.Children.Add(_fast);

            _slow.TextChanged += (s, e) =>
            {
                if (_slow.Value != AutomationStep.State.Slow)
                    RaiseChanged();
            };

            _stackPanel.Children.Add(_slow);

            if (processor.GetType() == typeof(IntelProcessorController))
            {
                _useMSR.IsChecked = false;
                _useMSR.Checked += (s, e) =>
                {
                    if (_useMSR.IsChecked != AutomationStep.State.UseMSR)
                        RaiseChanged();
                };

                _stackPanel.Children.Add(_useMSR);
            }

            _maintainTDP.IsChecked = false;
            _maintainTDP.Checked += (s, e) =>
            {
                if (_maintainTDP.IsChecked != AutomationStep.State.MaintainTDP)
                    RaiseChanged();
            };

            _stackPanel.Children.Add(_maintainTDP);

            _interval.TextChanged += (s, e) =>
            {
                if (_interval.Value != AutomationStep.State.Interval)
                    RaiseChanged();
            };

            _stackPanel.Children.Add(_interval);

            return _stackPanel;
        }

        protected override void OnFinishedLoading() { }

        protected override Task RefreshAsync()
        {
            _stapm.Value = AutomationStep.State.Stapm;
            _fast.Value = AutomationStep.State.Fast;
            _slow.Value = AutomationStep.State.Slow;
            _useMSR.IsChecked = AutomationStep.State.UseMSR;
            _maintainTDP.IsChecked = AutomationStep.State.MaintainTDP;
            _interval.Value = AutomationStep.State.Interval;

            if (_maintainTDP.IsChecked != null)
                if ((bool)_maintainTDP.IsChecked)
                    _interval.IsEnabled = true;
                else
                    _interval.IsEnabled = false;

            return Task.CompletedTask;
        }
    }
}
