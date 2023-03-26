using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Common;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Automation.Steps;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.WPF.Extensions;
using LenovoLegionToolkit.WPF.Windows.Automation;
using LenovoLegionToolkit.Lib.Automation.Pipeline.Triggers;
using System.Collections.Generic;

namespace LenovoLegionToolkit.WPF.Controls.Automation.Steps;

public class ProcessAutomationStepControl : AbstractAutomationStepControl<ProcessAutomationStep>
{
    private readonly Button _buttonProcesses = new ()
    {
        Content = "Processes",
        MinWidth = 150,
    };

    private readonly ComboBox _comboBoxState = new()
    {
        MinWidth = 150,
        Visibility = Visibility.Hidden,
    };

    private readonly StackPanel _stackPanel = new();

    private ProcessAutomationState _state;

    public ProcessAutomationStepControl(ProcessAutomationStep step) : base(step)
    {
        Icon = SymbolRegular.Apps24;
        Title = "Process Automation";
        Subtitle = "Select Processes to start or stop.";
    }

    public override IAutomationStep CreateAutomationStep() => new ProcessAutomationStep(_state);

    protected override UIElement? GetCustomControl()
    {
        _buttonProcesses.Click += (s, e) =>
        {
            ProcessInfo[] processes;
            if (AutomationStep.State.Processes != null)
                processes = AutomationStep.State.Processes;
            else
                processes = new ProcessInfo[] { };

            List<IProcessesAutomationPipelineTrigger> triggers = new();

            if (_state.State == ProcessState.Start) 
                triggers.Add(new ProcessesAreRunningAutomationPipelineTrigger(processes));
            else 
                triggers.Add(new ProcessesStopRunningAutomationPipelineTrigger(processes));

            var window = new AutomationPipelineTriggerConfigurationWindow(triggers)
            {
                Owner = Window.GetWindow(this),
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ShowInTaskbar = false,
            };
            window.OnSave += (s, e) =>
            {
                IProcessesAutomationPipelineTrigger triggers;

                if (_state.State == ProcessState.Start)
                    triggers = (ProcessesAreRunningAutomationPipelineTrigger)e;
                else
                    triggers = (ProcessesStopRunningAutomationPipelineTrigger)e;

                _state.Processes = triggers.Processes;
                RaiseChanged();
            };
            window.ShowDialog();
        };

        _stackPanel.Children.Add(_buttonProcesses);

        _comboBoxState.SelectionChanged += ComboBox_SelectionChanged;

        _stackPanel.Children.Add(_comboBoxState);

        return _stackPanel;
    }

    private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_comboBoxState.TryGetSelectedItem(out ProcessState selectedState) || _state.Equals(selectedState))
            return;

        _state.State = selectedState;

        RaiseChanged();
    }

    protected override void OnFinishedLoading() => _comboBoxState.Visibility = Visibility.Visible;

    protected override Task RefreshAsync()
    {
        var items = AutomationStep.GetAllStatesAsync();
        var selectedItem = AutomationStep.State.State;

        static string displayName(ProcessState value)
        {
            if (value is Enum e)
                return e.GetDisplayName();
            return value.ToString() ?? throw new InvalidOperationException("Unsupported type");
        }

        _state.State = selectedItem;
        _comboBoxState.SetItems(items.Result, selectedItem, displayName);
        _comboBoxState.IsEnabled = items.Result.Any();

        _state.Processes = AutomationStep.State.Processes;

        return Task.CompletedTask;
    }
}
