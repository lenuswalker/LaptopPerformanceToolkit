using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Controllers;
using LenovoLegionToolkit.Lib.Listeners;
using LenovoLegionToolkit.Lib.System;
using LenovoLegionToolkit.Lib.Utils;

namespace LenovoLegionToolkit.Lib.Features;

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

    public override async Task<bool> IsSupportedAsync()
    {
        try
        {
            if (_supportMethodName is null)
                return true;

            var value = await WMI.CallAsync(Scope,
                Query,
                _supportMethodName,
                new(),
                pdc => Convert.ToInt32(pdc[_outParameterName].Value)).ConfigureAwait(false);
            return value > _supportOffset;
        }
        catch
        {
            uint result = Power.PowerGetEffectiveOverlayScheme(out Guid currentMode);
            if (result == 0)
                return true;
            else                
                return false;
        }
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
            uint result = Power.PowerGetEffectiveOverlayScheme(out Guid currentMode);
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
            Power.PowerSetActiveOverlayScheme(new Guid(defaultGenericPowerModes[state]));
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
}