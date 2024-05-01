using System;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.System;
using Newtonsoft.Json;

namespace LenovoLegionToolkit.Lib.Automation.Pipeline.Triggers;

public class TimeIntervalAutomationPipelineTrigger : ITimeIntervalAutomationPipelineTrigger
{
    public int? ACInterval { get; }
    public int? DCInterval { get; }

    public string DisplayName => "At specified interval";

    [JsonConstructor]
    public TimeIntervalAutomationPipelineTrigger(int? acInterval, int? dcInterval)
    {
        ACInterval = acInterval;
        DCInterval = dcInterval;
    }

    public async Task<bool> IsMatchingEvent(IAutomationEvent automationEvent) 
    {
        if (automationEvent is not TimeIntervalAutomationEvent e)
            return false;

        PowerAdapterStatus powerAdapterStatus = await Power.IsPowerAdapterConnectedAsync().ConfigureAwait(false);

        if (powerAdapterStatus == PowerAdapterStatus.Connected && ACInterval == e.Interval)
            return true;

        if (powerAdapterStatus == PowerAdapterStatus.Disconnected && DCInterval == e.Interval)
            return true;


        return false;
    }

    public async Task<bool> IsMatchingState() 
    {
        if (ACInterval is int && (int)ACInterval > 0) 
            return true;

        if (DCInterval is int && (int)DCInterval > 0) 
            return true;

        return false;
    }

    public void UpdateEnvironment(AutomationEnvironment environment)
    {
        environment.ACInterval = ACInterval;
        environment.DCInterval = DCInterval;
    }

    public IAutomationPipelineTrigger DeepCopy() => new TimeIntervalAutomationPipelineTrigger(ACInterval, DCInterval);

    public ITimeIntervalAutomationPipelineTrigger DeepCopy(int? acInterval, int? dcInterval) => new TimeIntervalAutomationPipelineTrigger(ACInterval, DCInterval);
    
    public override bool Equals(object? obj)
    {
        return obj is TimeIntervalAutomationPipelineTrigger trigger &&
               ACInterval == trigger.ACInterval &&
               DCInterval == trigger.DCInterval;
    }

    public override int GetHashCode() => HashCode.Combine(ACInterval, DCInterval);

    public override string ToString() => $"{nameof(ACInterval)}: {ACInterval}, {nameof(DCInterval)}: {DCInterval}";
}
