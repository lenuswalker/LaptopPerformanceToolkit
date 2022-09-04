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

        private readonly StackPanel _stackPanel = new();

        public ProcessorTDPAutomationStepControl(ProcessorTDPAutomationStep step) : base(step)
        {
            Icon = SymbolRegular.DeveloperBoardLightning20;
            Title = "Processor TDP";
            Subtitle = "Change the TDP limits of the processor.\n\nNOTE: This action uses RyzenAdj for AMD,\nand KX utility for Intel.";
        }

        public override IAutomationStep CreateAutomationStep() => new ProcessorTDPAutomationStep(_stapm.Value, _fast.Value, _slow.Value);

        protected override UIElement? GetCustomControl()
        {
            ProcessorController processor = _controller.GetCurrent();
            if (processor.GetType() == typeof(AMDProcessorController))
            {
                _stapm.TextChanged += (s, e) =>
                {
                    if (_stapm.Value != AutomationStep.Stapm)
                        RaiseChanged();
                };

                _stackPanel.Children.Add(_stapm);
            }

            _fast.TextChanged += (s, e) =>
            {
                if (_fast.Value != AutomationStep.Fast)
                    RaiseChanged();
            };

            _stackPanel.Children.Add(_fast);

            _slow.TextChanged += (s, e) =>
            {
                if (_slow.Value != AutomationStep.Slow)
                    RaiseChanged();
            };

            _stackPanel.Children.Add(_slow);

            return _stackPanel;
        }

        protected override void OnFinishedLoading() { }

        protected override Task RefreshAsync()
        {
            _stapm.Value = AutomationStep.Stapm;
            _fast.Value = AutomationStep.Fast;
            _slow.Value = AutomationStep.Slow;
            return Task.CompletedTask;
        }
    }
}
