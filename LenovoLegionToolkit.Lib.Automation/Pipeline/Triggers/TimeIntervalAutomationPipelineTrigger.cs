using System;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.System;
using Newtonsoft.Json;

namespace LenovoLegionToolkit.Lib.Automation.Pipeline.Triggers;

public class TimeIntervalAutomationPipelineTrigger : IAutomationPipelineTrigger
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

    public async Task<bool> IsSatisfiedAsync(IAutomationEvent automationEvent)
    {
        PowerAdapterStatus powerAdapterStatus = await Power.IsPowerAdapterConnectedAsync().ConfigureAwait(false);

        if (automationEvent is StartupAutomationEvent)
            return false;

        if (automationEvent is not TimeIntervalAutomationEvent timeIntervalAutomationEvent)
            return false;

        if (powerAdapterStatus == PowerAdapterStatus.Connected && ACInterval == timeIntervalAutomationEvent.Interval)
            return true;

        if (powerAdapterStatus == PowerAdapterStatus.Disconnected && DCInterval == timeIntervalAutomationEvent.Interval)
            return true;

        return false;
    }

    public IAutomationPipelineTrigger DeepCopy(int? acInterval, int? dcInterval) => new TimeIntervalAutomationPipelineTrigger(acInterval, dcInterval);

    public IAutomationPipelineTrigger DeepCopy() => new TimeIntervalAutomationPipelineTrigger(ACInterval, DCInterval);
    
    public override bool Equals(object? obj)
    {
        return obj is TimeIntervalAutomationPipelineTrigger trigger &&
               ACInterval == trigger.ACInterval &&
               DCInterval == trigger.DCInterval;
    }

    public override int GetHashCode() => HashCode.Combine(ACInterval, DCInterval);
}
