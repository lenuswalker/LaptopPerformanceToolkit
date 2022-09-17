using LenovoLegionToolkit.Lib.Automation;
using LenovoLegionToolkit.Lib.Automation.Steps;
using Wpf.Ui.Common;

namespace LenovoLegionToolkit.WPF.Controls.Automation.Steps
{
    public class MaintainProcessorTDPAutomationStepControl : AbstractComboBoxAutomationStepCardControl<MaintainProcessorTDPAutomationStepState>
    {
        public MaintainProcessorTDPAutomationStepControl(MaintainProcessorTDPAutomationStep step) : base(step)
        {
            Icon = SymbolRegular.DeveloperBoardLightning20;
            Title = "Maintain Processor TDP Limits";
            Subtitle = "Keep the processor at set TDP Limits.\n\nWARNING: This may use additional system resources.";
        }
    }
}
