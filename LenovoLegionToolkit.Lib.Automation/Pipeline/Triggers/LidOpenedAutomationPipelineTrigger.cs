﻿using System;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Automation.Resources;
using Newtonsoft.Json;

namespace LenovoLegionToolkit.Lib.Automation.Pipeline.Triggers;

public class LidOpenedAutomationPipelineTrigger : INativeWindowsMessagePipelineTrigger
{
    [JsonIgnore]
    public string DisplayName => Resource.LidOpenedAutomationPipelineTrigger_DisplayName;

    public Task<bool> IsSatisfiedAsync(IAutomationEvent automationEvent)
    {
        var result = automationEvent is NativeWindowsMessageEvent { Message: NativeWindowsMessage.LidOpened };
        return Task.FromResult(result);
    }

    public IAutomationPipelineTrigger DeepCopy() => new LidOpenedAutomationPipelineTrigger();

    public override bool Equals(object? obj) => obj is LidOpenedAutomationPipelineTrigger;

    public override int GetHashCode() => HashCode.Combine(DisplayName);
}
