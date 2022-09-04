using LenovoLegionToolkit.Lib.Controllers;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.WPF.Windows;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using LenovoLegionToolkit.Lib.Settings;

namespace LenovoLegionToolkit.WPF.Utils
{
    public class ProcessorManager
    {
        #region imports
        /// <summary>
        /// Retrieves the active overlay power scheme and returns a GUID that identifies the scheme.
        /// </summary>
        /// <param name="EffectiveOverlayPolicyGuid">A pointer to a GUID structure.</param>
        /// <returns>Returns zero if the call was successful, and a nonzero value if the call failed.</returns>
        [DllImportAttribute("powrprof.dll", EntryPoint = "PowerGetEffectiveOverlayScheme")]
        private static extern uint PowerGetEffectiveOverlayScheme(out Guid EffectiveOverlayPolicyGuid);

        /// <summary>
        /// Sets the active power overlay power scheme.
        /// </summary>
        /// <param name="OverlaySchemeGuid">The identifier of the overlay power scheme.</param>
        /// <returns>Returns zero if the call was successful, and a nonzero value if the call failed.</returns>
        [DllImportAttribute("powrprof.dll", EntryPoint = "PowerSetActiveOverlayScheme")]
        private static extern uint PowerSetActiveOverlayScheme(Guid OverlaySchemeGuid);
        #endregion

        private readonly ProcessorController _controller = IoCContainer.Resolve<ProcessorController>();

        // timers
        private Timer powerWatchdog;

        private Timer cpuWatchdog;
        protected object cpuLock = new();

        private Timer gfxWatchdog;
        protected object gfxLock = new();

        public event LimitChangedHandler PowerLimitChanged;
        public delegate void LimitChangedHandler(PowerType type, int limit);

        public event ValueChangedHandler PowerValueChanged;
        public delegate void ValueChangedHandler(PowerType type, float value);

        public event StatusChangedHandler ProcessorStatusChanged;
        public delegate void StatusChangedHandler(bool CanChangeTDP, bool CanChangeGPU);

        // TDP limits
        private double[] FallbackTDP = new double[3];   // used to store fallback TDP
        private double[] StoredTDP = new double[3];     // used to store TDP
        private double[] CurrentTDP = new double[5];    // used to store current TDP

        // GPU limits
        private double FallbackGfxClock;
        private double StoredGfxClock;
        private double CurrentGfxClock;

        // Power modes
        private Guid RequestedPowerMode;

        private readonly ProcessorSettings _settings;

        public ProcessorManager(ProcessorSettings settings)
        {
            _settings = settings;

            // initialize timer(s)
            powerWatchdog = new Timer() { Interval = 3000, AutoReset = true, Enabled = false };
            powerWatchdog.Elapsed += powerWatchdog_Elapsed;

            cpuWatchdog = new Timer() { Interval = 3000, AutoReset = true, Enabled = false };
            cpuWatchdog.Elapsed += cpuWatchdog_Elapsed;

            gfxWatchdog = new Timer() { Interval = 3000, AutoReset = true, Enabled = false };
            gfxWatchdog.Elapsed += gfxWatchdog_Elapsed;

            // initialize processor
            _controller = _controller.GetCurrent();
            _controller.ValueChanged += Processor_ValueChanged;
            _controller.StatusChanged += Processor_StatusChanged;
            _controller.LimitChanged += Processor_LimitChanged;
            _controller.MiscChanged += Processor_MiscChanged;

            // initialize settings
            //var TDPdown = Properties.Settings.Default.QuickToolsPerformanceTDPEnabled ? Properties.Settings.Default.QuickToolsPerformanceTDPSustainedValue : 0;
            //var TDPup = Properties.Settings.Default.QuickToolsPerformanceTDPEnabled ? Properties.Settings.Default.QuickToolsPerformanceTDPBoostValue : 0;

            var TDPdown = _settings.Store.IsEnabled ? _settings.Store.State.Mode[_settings.Store.State.Mode.Keys.First()].Slow : 0;
            var TDPup = _settings.Store.IsEnabled ? _settings.Store.State.Mode[_settings.Store.State.Mode.Keys.First()].Fast : 0;

            //TDPdown = TDPdown != 0 ? TDPdown : MainWindow.handheldDevice.nTDP[(int)PowerType.Slow];
            //TDPup = TDPup != 0 ? TDPup : MainWindow.handheldDevice.nTDP[(int)PowerType.Fast];

            RequestTDP(PowerType.Slow, TDPdown);
            RequestTDP(PowerType.Stapm, TDPdown);
            RequestTDP(PowerType.Fast, TDPup);

            //var GPU = Properties.Settings.Default.QuickToolsPerformanceGPUEnabled ? Properties.Settings.Default.QuickToolsPerformanceGPUValue : 0;
            //if (GPU != 0)
            //    RequestGPUClock(GPU, true);

            cpuWatchdog.Start();
            //gfxWatchdog.Start();  
        }

        private void powerWatchdog_Elapsed(object? sender, ElapsedEventArgs e)
        {
            // Checking if active power shceme has changed
            if (PowerGetEffectiveOverlayScheme(out Guid activeScheme) == 0)
                if (activeScheme != RequestedPowerMode)
                    PowerSetActiveOverlayScheme(RequestedPowerMode);
        }

        private void cpuWatchdog_Elapsed(object? sender, ElapsedEventArgs e)
        {
            lock (cpuLock)
            {
                // read current values and (re)apply requested TDP if needed
                foreach (PowerType type in (PowerType[])Enum.GetValues(typeof(PowerType)))
                {
                    int idx = (int)type;

                    // skip msr
                    if (idx >= StoredTDP.Length)
                        break;

                    double TDP = StoredTDP[idx];

                    //if (processor.GetType() == typeof(AMDProcessor))
                    //{
                    //    // AMD reduces TDP by 10% when OS power mode is set to Best power efficiency
                    //    if (RequestedPowerMode == PowerMode.BetterBattery)
                    //        TDP = (int)Math.Truncate(TDP * 0.9);
                    //}
                    //else if (processor.GetType() == typeof(IntelProcessor))
                    //{
                    //    // Intel doesn't have stapm
                    //    if (type == PowerType.Stapm)
                    //        continue;
                    //}

                    // not ready yet
                    if (CurrentTDP[idx] == 0)
                        break;

                    // only request an update if current limit is different than stored
                    if (CurrentTDP[idx] != TDP)
                        _controller.SetTDPLimit(type, TDP);
                }

                // processor specific
                if (_controller.GetType() == typeof(IntelProcessorController))
                {
                    // not ready yet
                    if (CurrentTDP[(int)PowerType.MsrSlow] == 0 || CurrentTDP[(int)PowerType.MsrFast] == 0)
                        return;

                    int TDPslow = (int)StoredTDP[(int)PowerType.Slow];
                    int TDPfast = (int)StoredTDP[(int)PowerType.Fast];

                    // only request an update if current limit is different than stored
                    if (CurrentTDP[(int)PowerType.MsrSlow] != TDPslow ||
                        CurrentTDP[(int)PowerType.MsrFast] != TDPfast)
                        ((IntelProcessorController)_controller).SetMSRLimit(TDPslow, TDPfast);
                }
            }
        }

        private void gfxWatchdog_Elapsed(object? sender, ElapsedEventArgs e)
        {
            lock (gfxLock)
            {
                if (_controller.GetType() == typeof(AMDProcessorController))
                {
                    // not ready yet
                    if (CurrentGfxClock == 0)
                        return;
                }
                else if (_controller.GetType() == typeof(IntelProcessorController))
                {
                    // not ready yet
                    if (CurrentGfxClock == 12750)
                        return;
                }

                // not ready yet
                if (StoredGfxClock == 0)
                    return;

                // only request an update if current gfx clock is different than stored
                if (CurrentGfxClock != StoredGfxClock)
                    _controller.SetGPUClock(StoredGfxClock);
            }
        }

        public void RequestTDP(PowerType type, double value, bool UserRequested = true)
        {
            int idx = (int)type;

            if (UserRequested)
                FallbackTDP[idx] = value;

            // update value read by timer
            StoredTDP[idx] = value;
        }

        public void RequestTDP(double[] values, bool UserRequested = true)
        {
            if (UserRequested)
                FallbackTDP = values;

            // update value read by timer
            StoredTDP = values;
        }

        public void RequestGPUClock(double value, bool UserRequested = true)
        {
            if (UserRequested)
                FallbackGfxClock = value;

            // update value read by timer
            StoredGfxClock = value;
        }

        //public void RequestPowerMode(int idx)
        //{
        //    RequestedPowerMode = PowerMode.PowerModes[idx];
        //    LogManager.LogInformation("User requested power scheme: {0}", RequestedPowerMode);

        //    PowerSetActiveOverlayScheme(RequestedPowerMode);
        //}

        #region events
        private void Processor_StatusChanged(bool CanChangeTDP, bool CanChangeGPU)
        {
            ProcessorStatusChanged?.Invoke(CanChangeTDP, CanChangeGPU);
        }

        private void Processor_ValueChanged(PowerType type, float value)
        {
            PowerValueChanged?.Invoke(type, value);
        }

        private void Processor_LimitChanged(PowerType type, int limit)
        {
            int idx = (int)type;
            CurrentTDP[idx] = limit;

            // raise event
            PowerLimitChanged?.Invoke(type, limit);
        }

        private void Processor_MiscChanged(string misc, float value)
        {
            switch (misc)
            {
                case "gfx_clk":
                    {
                        CurrentGfxClock = value;
                    }
                    break;
            }
        }
        #endregion

        internal void Start()
        {
            _controller.Initialize();
            powerWatchdog.Start();
        }

        internal void Stop()
        {
            _controller.Stop();
            powerWatchdog.Stop();
        }
    }
}
