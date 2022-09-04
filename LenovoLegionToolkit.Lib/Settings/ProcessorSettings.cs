using System.Collections.Generic;

namespace LenovoLegionToolkit.Lib.Settings
{
    public class ProcessorSettings : AbstractSettings<ProcessorSettings.ProcessorSettingsStore>
    {
        public class ProcessorSettingsStore
        {
            public bool IsEnabled { get; set; }
            public TDPState State { get; set; }
        }

        protected override string FileName => "processor_settings.json";

        public override ProcessorSettingsStore Default => new()
        {
            IsEnabled = false,
            State = new(new Dictionary<TDPMode, TDPLimits> {
                { TDPMode.AC, new(0, 0, 0, 0, 0) },
                { TDPMode.DC, new(15, 15, 15, 80, 80) },
                { TDPMode.GameMode, new(20, 20, 45, 90, 90) },
                { TDPMode.PreGameMode, new(0, 0, 0, 0, 0) },
            })
        };
    }
}
