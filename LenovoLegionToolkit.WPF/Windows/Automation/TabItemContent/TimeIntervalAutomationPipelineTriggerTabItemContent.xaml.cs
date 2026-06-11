using LenovoLegionToolkit.Lib.Automation.Pipeline.Triggers;

namespace LenovoLegionToolkit.WPF.Windows.Automation.TabItemContent;

public partial class TimeIntervalAutomationPipelineTriggerTabItemContent : IAutomationPipelineTriggerTabItemContent<ITimeIntervalAutomationPipelineTrigger>
{
    private readonly ITimeIntervalAutomationPipelineTrigger _trigger;

    public TimeIntervalAutomationPipelineTriggerTabItemContent(ITimeIntervalAutomationPipelineTrigger trigger)
    {
        _trigger = trigger;

        InitializeComponent();

        _acTimeIntervalSeconds.Value = trigger.ACInterval;
        _dcTimeIntervalSeconds.Value = trigger.DCInterval;
    }

    public ITimeIntervalAutomationPipelineTrigger GetTrigger()
    {
        var acInterval = (int?)_acTimeIntervalSeconds.Value ?? 0;
        var dcInterval = (int?)_dcTimeIntervalSeconds.Value ?? 0;

        return _trigger.DeepCopy(acInterval, dcInterval);
    }
}
