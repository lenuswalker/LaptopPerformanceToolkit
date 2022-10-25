using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Controllers;
using LenovoLegionToolkit.Lib.Listeners;
using LenovoLegionToolkit.Lib.System;
using LenovoLegionToolkit.Lib.Utils;

namespace LenovoLegionToolkit.Lib.Features
{
    public class PowerModeFeature : AbstractLenovoGamezoneWmiFeature<PowerModeState>
    {
        private readonly AIModeController _aiModeController;
        private readonly PowerModeListener _listener;

        private static readonly Dictionary<PowerModeState, string> defaultGenericPowerModes = new()
        {
            { PowerModeState.Efficiency , "961cc777-2547-4f9d-8174-7d86181b8a7a" },
            { PowerModeState.Balance , "00000000-0000-0000-0000-000000000000" },
            { PowerModeState.Performance , "ded574b5-45a0-4f42-8737-46345c09c238" },
        };

        public PowerModeFeature(AIModeController aiModeController, PowerModeListener listener)
            : base("SmartFanMode", 1, "IsSupportSmartFan")
        {
            _aiModeController = aiModeController ?? throw new ArgumentNullException(nameof(aiModeController));
            _listener = listener ?? throw new ArgumentNullException(nameof(listener));
        }

        public override async Task<PowerModeState[]> GetAllStatesAsync()
        {
            var mi = await Compatibility.GetMachineInformationAsync().ConfigureAwait(false);
            if (mi.Properties.SupportsGodMode)
                return new[] { PowerModeState.Quiet, PowerModeState.Balance, PowerModeState.Performance, PowerModeState.GodMode };

            var compatibility = await Compatibility.IsCompatibleAsync().ConfigureAwait(false);
            if (compatibility.isCompatible)
                return new[] { PowerModeState.Quiet, PowerModeState.Balance, PowerModeState.Performance };
            else
                return new[] { PowerModeState.Efficiency, PowerModeState.Balance, PowerModeState.Performance };
        }

        public override async Task<PowerModeState> GetStateAsync()
        {
            var compatibility = await Compatibility.IsCompatibleAsync().ConfigureAwait(false);
            if (compatibility.isCompatible)
                return base.GetStateAsync().Result;
            else
            {
                PowerModeState state = PowerModeState.Balance;
                uint result = PowerGetEffectiveOverlayScheme(out Guid currentMode);
                switch (currentMode.ToString())
                {
                    case "961cc777-2547-4f9d-8174-7d86181b8a7a":
                        state = PowerModeState.Efficiency;
                        break;
                    case "00000000-0000-0000-0000-000000000000":
                        state = PowerModeState.Balance;
                        break;
                    case "ded574b5-45a0-4f42-8737-46345c09c238":
                        state = PowerModeState.Performance;
                        break;
                }
                return state;
            }
        }

        public override async Task SetStateAsync(PowerModeState state)
        {
            var allStates = await GetAllStatesAsync().ConfigureAwait(false);
            if (!allStates.Contains(state))
                throw new InvalidOperationException($"Unsupported power mode {state}.");

            var compatibility = await Compatibility.IsCompatibleAsync().ConfigureAwait(false);
            if (compatibility.isCompatible)
            {
                var currentState = await GetStateAsync().ConfigureAwait(false);

                await _aiModeController.StopAsync(currentState).ConfigureAwait(false);

                // Workaround: Performance mode doesn't update the dGPU temp limit (and possibly other properties) on some Gen 7 devices.
                var mi = await Compatibility.GetMachineInformationAsync().ConfigureAwait(false);
                if (mi.Properties.HasPerformanceModeSwitchingBug && currentState == PowerModeState.Quiet && state == PowerModeState.Performance)
                    await base.SetStateAsync(PowerModeState.Balance).ConfigureAwait(false);

                await base.SetStateAsync(state).ConfigureAwait(false);
            }
            else
            {
                PowerSetActiveOverlayScheme(new Guid(defaultGenericPowerModes[state]));
            }

            await _listener.NotifyAsync(state).ConfigureAwait(false);
        }

        public async Task EnsureCorrectPowerPlanIsSetAsync()
        {
            var compatibility = await Compatibility.IsCompatibleAsync().ConfigureAwait(false);
            if (compatibility.isCompatible)
            {
                var state = await GetStateAsync().ConfigureAwait(false);
                await Power.ActivatePowerPlanAsync(state, true).ConfigureAwait(false);
            }
        }

        public async Task EnsureAIModeIsSetAsync()
        {
            var compatibility = await Compatibility.IsCompatibleAsync().ConfigureAwait(false);
            if (compatibility.isCompatible)
            {
                var state = await GetStateAsync().ConfigureAwait(false);
                await _aiModeController.StartAsync(state).ConfigureAwait(false);
            }
        }

        public async Task EnsureAIModeIsOffAsync()
        {
            var compatibility = await Compatibility.IsCompatibleAsync().ConfigureAwait(false);
            if (compatibility.isCompatible)
            {
                var state = await GetStateAsync().ConfigureAwait(false);
                await _aiModeController.StopAsync(state).ConfigureAwait(false);
            }
        }

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
    }
}