using LenovoLegionToolkit.Lib.Automation.Pipeline.Triggers;

namespace LenovoLegionToolkit.WPF.Windows.Automation.TabItemContent;

public partial class TimeIntervalAutomationPipelineTriggerTabItemContent : IAutomationPipelineTriggerTabItemContent<ITimeIntervalAutomationPipelineTrigger>
{
    private readonly ITimeIntervalAutomationPipelineTrigger _trigger;
    private readonly int? _acInterval;
    private readonly int? _dcInterval;

    public TimeIntervalAutomationPipelineTriggerTabItemContent(ITimeIntervalAutomationPipelineTrigger trigger)
    {
        _trigger = trigger;
        _acInterval = trigger.ACInterval;
        _dcInterval = trigger.DCInterval;

        InitializeComponent();
    }

    public ITimeIntervalAutomationPipelineTrigger GetTrigger()
    {
        int acInterval = (int)_acTimeIntervalSeconds.Value;
        int dcInterval = (int)_dcTimeIntervalSeconds.Value;

        return _trigger.DeepCopy(acInterval, dcInterval);
    }
}
