using Newtonsoft.Json;

namespace LenovoLegionToolkit.Lib.Automation.Steps;

public class DisplayBrightnessAutomationStep : AbstractFeatureAutomationStep<int>
{
    [JsonConstructor]
    public DisplayBrightnessAutomationStep(int state) : base(state) { }

    IAutomationStep IAutomationStep.DeepCopy() => new DisplayBrightnessAutomationStep(Brightness);
}
