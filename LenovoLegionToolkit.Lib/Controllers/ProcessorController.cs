using LenovoLegionToolkit.Lib.Settings;
using LenovoLegionToolkit.Lib.Utils;
using System;
using System.Collections.Generic;
using System.Management;
using System.Threading.Tasks;
using System.Timers;

namespace LenovoLegionToolkit.Lib.Controllers
{
    public class ProcessorController
    {
        private static ManagementClass managClass = new ManagementClass("win32_processor");

        private static ProcessorController processor;
        private static string Manufacturer;

        protected string Name, ProcessorID;

        protected bool CanChangeTDP, CanChangeGPU;
        protected object IsBusy = new();

        protected Timer updateTimer = new Timer() { Interval = 3000, AutoReset = true };

        protected Dictionary<PowerType, int> m_Limits = new();
        protected Dictionary<PowerType, int> m_PrevLimits = new();

        protected Dictionary<string, float> m_Misc = new();
        protected Dictionary<string, float> m_PrevMisc = new();

        #region events
        public event LimitChangedHandler LimitChanged;
        public delegate void LimitChangedHandler(PowerType type, int limit);

        public event ValueChangedHandler ValueChanged;
        public delegate void ValueChangedHandler(PowerType type, float value);

        public event GfxChangedHandler MiscChanged;
        public delegate void GfxChangedHandler(string misc, float value);

        public event StatusChangedHandler StatusChanged;
        public delegate void StatusChangedHandler(bool CanChangeTDP, bool CanChangeGPU);
        #endregion

        private readonly ProcessorSettings _settings;

        public static ProcessorController GetCurrent()
        {
            if (processor != null)
                return processor;

            Manufacturer = GetProcessorDetails("Manufacturer");

            switch (Manufacturer)
            {
                case "GenuineIntel":
                    processor = new IntelProcessorController();
                    break;
                case "AuthenticAMD":
                    processor = new AMDProcessorController();
                    break;
            }

            return processor;
        }

        private static string GetProcessorDetails(string value)
        {
            var managCollec = managClass.GetInstances();
            foreach (ManagementObject managObj in managCollec)
                return managObj.Properties[value].Value.ToString();

            return "";
        }

        public ProcessorController()
        {
            _settings = new();
            Name = GetProcessorDetails("Name");
            ProcessorID = GetProcessorDetails("processorID");

            // write default miscs
            m_Misc["gfx_clk"] = m_PrevMisc["gfx_clk"] = 0;
        }

        public async Task<List<Dictionary<TDPMode, TDPLimits>>> GetSettingsAsync()
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Getting perfboostmode settings...");

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Getting power plans...");


            var result = new List<Dictionary<TDPMode, TDPLimits>>();
            foreach (TDPMode mode in Enum.GetValues(typeof(TDPMode)))
            {
                try
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Getting RyzenAdjMode from settings file for {mode}...");

                    Dictionary<TDPMode, TDPLimits> settings = new();

                    settings.Add(mode, _settings.Store.State.Mode[mode]);

                    if (mode != TDPMode.PreGameMode)
                        result.Add(settings);
                }
                catch (Exception ex)
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Failed to load settings for {mode}.", ex);
                }
            }

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"RyzenAdj settings retrieved.");

            return result;
        }

        public async Task SetSettingsAsync(TDPMode mode, TDPLimits limits)
        {
            _settings.Store.State.Mode[mode] = limits;

            _settings.SynchronizeStore();
            
            await Task.CompletedTask;
        }

        public virtual void Initialize()
        {
            StatusChanged?.Invoke(CanChangeTDP, CanChangeGPU);

            if (CanChangeTDP)
                updateTimer.Start();
        }

        public virtual void Stop()
        {
            if (CanChangeTDP)
                updateTimer.Stop();
        }

        public virtual void SetTDPLimit(PowerType type, double limit, int result = 0)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"User requested {type} TDP limit: {limit}, error code: {result}");
        }

        public virtual void SetGPUClock(double clock, int result = 0)
        {
            /*
             * #define ADJ_ERR_FAM_UNSUPPORTED      -1
             * #define ADJ_ERR_SMU_TIMEOUT          -2
             * #define ADJ_ERR_SMU_UNSUPPORTED      -3
             * #define ADJ_ERR_SMU_REJECTED         -4
             * #define ADJ_ERR_MEMORY_ACCESS        -5
             */
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"User requested GPU clock: {clock}, error code: {result}");
        }

        protected virtual void UpdateTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            // search for limit changes
            foreach (KeyValuePair<PowerType, int> pair in m_Limits)
            {
                if (m_PrevLimits[pair.Key] == pair.Value)
                    continue;

                LimitChanged?.Invoke(pair.Key, pair.Value);

                m_PrevLimits[pair.Key] = pair.Value;
            }

            // search for misc changes
            foreach (KeyValuePair<string, float> pair in m_Misc)
            {
                if (m_PrevMisc[pair.Key] == pair.Value)
                    continue;

                MiscChanged?.Invoke(pair.Key, pair.Value);

                m_PrevMisc[pair.Key] = pair.Value;
            }
        }
    }
}
