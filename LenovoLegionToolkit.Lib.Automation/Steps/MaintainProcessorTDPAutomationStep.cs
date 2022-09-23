using System;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Automation.Utils;

namespace LenovoLegionToolkit.Lib.Automation.Steps
{
    public class MaintainProcessorTDPAutomationStep : IAutomationStep<MaintainProcessorTDPAutomationStepState>, IDisallowDuplicatesAutomationStep
    {
        //private readonly ProcessorManager _manager = IoCContainer.Resolve<ProcessorManager>();
        private ProcessorManager? _manager;
        
        public MaintainProcessorTDPAutomationStepState State { get; }

        public MaintainProcessorTDPAutomationStep(MaintainProcessorTDPAutomationStepState state) => State = state;

        public Task<MaintainProcessorTDPAutomationStepState[]> GetAllStatesAsync() => Task.FromResult(Enum.GetValues<MaintainProcessorTDPAutomationStepState>());
        
        public Task<bool> IsSupportedAsync() => Task.FromResult(true);


        public async Task RunAsync()
        {
            _manager = IoCContainer.Resolve<ProcessorManager>();

            if (!_manager.IsSupported())
                return;

            switch (State)
            {
                case MaintainProcessorTDPAutomationStepState.Enabled:
                    await _manager.MaintainTDP().ConfigureAwait(false);
                    break;
                case MaintainProcessorTDPAutomationStepState.Disabled:
                    break;
            }
        }
        
        IAutomationStep IAutomationStep.DeepCopy() => new MaintainProcessorTDPAutomationStep(State);
    }
}
