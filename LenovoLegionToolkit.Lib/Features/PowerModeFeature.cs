using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Controllers;
using LenovoLegionToolkit.Lib.Controllers.GodMode;
using LenovoLegionToolkit.Lib.Listeners;
using LenovoLegionToolkit.Lib.System;
using LenovoLegionToolkit.Lib.Utils;

namespace LenovoLegionToolkit.Lib.Features;

public class PowerModeUnavailableWithoutACException : Exception
{
    public PowerModeState PowerMode { get; }

    public PowerModeUnavailableWithoutACException(PowerModeState powerMode)
    {
        PowerMode = powerMode;
    }
}

public class PowerModeFeature : AbstractLenovoGamezoneWmiFeature<PowerModeState>
{
    private readonly AIModeController _aiModeController;
    private readonly GodModeController _godModeController;
    private readonly PowerPlanController _powerPlanController;
    private readonly ThermalModeListener _thermalModeListener;
    private readonly PowerModeListener _powerModeListener;

    public bool AllowAllPowerModesOnBattery { get; set; }

    private static readonly Dictionary<PowerModeState, string> defaultGenericPowerModes = new()
    {
        { PowerModeState.Efficiency , "961cc777-2547-4f9d-8174-7d86181b8a7a" },
        { PowerModeState.Balance , "00000000-0000-0000-0000-000000000000" },
        { PowerModeState.Performance , "ded574b5-45a0-4f42-8737-46345c09c238" },
    };

    public PowerModeFeature(
        AIModeController aiModeController,
        GodModeController godModeController,
        PowerPlanController powerPlanController,
        ThermalModeListener thermalModeListener,
        PowerModeListener powerModeListener) : base("SmartFanMode", 1, "IsSupportSmartFan")
    {
        _aiModeController = aiModeController ?? throw new ArgumentNullException(nameof(aiModeController));
        _godModeController = godModeController ?? throw new ArgumentNullException(nameof(godModeController));
        _powerPlanController = powerPlanController ?? throw new ArgumentNullException(nameof(powerPlanController));
        _thermalModeListener = thermalModeListener ?? throw new ArgumentNullException(nameof(thermalModeListener));
        _powerModeListener = powerModeListener ?? throw new ArgumentNullException(nameof(powerModeListener));
    }

    public override async Task<bool> IsSupportedAsync()
    {
        try
        {
            if (_supportMethodName is null)
                return true;

            var value = await WMI.CallAsync(SCOPE,
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
        var compatibility = await Compatibility.IsCompatibleAsync().ConfigureAwait(false);
        var mi = await Compatibility.GetMachineInformationAsync().ConfigureAwait(false);

        if (compatibility.isCompatible)
            return mi.Properties.SupportsGodMode
                ? new[] { PowerModeState.Quiet, PowerModeState.Balance, PowerModeState.Performance, PowerModeState.GodMode }
                : new[] { PowerModeState.Quiet, PowerModeState.Balance, PowerModeState.Performance };
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

        var currentState = await GetStateAsync().ConfigureAwait(false);

        var mi = await Compatibility.GetMachineInformationAsync().ConfigureAwait(false);

        var compatibility = await Compatibility.IsCompatibleAsync().ConfigureAwait(false);
        if (compatibility.isCompatible)
        {
            if (state is PowerModeState.Performance or PowerModeState.GodMode
                && !AllowAllPowerModesOnBattery
                && await Power.IsPowerAdapterConnectedAsync().ConfigureAwait(false) is PowerAdapterStatus.Disconnected)
                throw new PowerModeUnavailableWithoutACException(state);

            await _aiModeController.StopAsync(currentState).ConfigureAwait(false);

            if (mi.Properties.HasQuietToPerformanceModeSwitchingBug && currentState == PowerModeState.Quiet && state == PowerModeState.Performance)
            {
                _thermalModeListener.SuppressNext();
                await base.SetStateAsync(PowerModeState.Balance).ConfigureAwait(false);
                await Task.Delay(TimeSpan.FromMilliseconds(500)).ConfigureAwait(false);
            }

            if (mi.Properties.HasGodModeToOtherModeSwitchingBug && currentState == PowerModeState.GodMode && state != PowerModeState.GodMode)
            {
                _thermalModeListener.SuppressNext();
                await base.SetStateAsync(PowerModeState.Quiet).ConfigureAwait(false);

                switch (state)
                {
                    case PowerModeState.Quiet:
                        await base.SetStateAsync(PowerModeState.Performance).ConfigureAwait(false);
                        break;
                    case PowerModeState.Balance:
                        await base.SetStateAsync(PowerModeState.Quiet).ConfigureAwait(false);
                        break;
                    case PowerModeState.Performance:
                        await base.SetStateAsync(PowerModeState.Balance).ConfigureAwait(false);
                        break;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(500)).ConfigureAwait(false);

            }

            _thermalModeListener.SuppressNext();
            await base.SetStateAsync(state).ConfigureAwait(false);
        }
        else
        {
            Power.PowerSetActiveOverlayScheme(new Guid(defaultGenericPowerModes[state]));
        }

        if (state == PowerModeState.GodMode)
            await _godModeController.ApplyStateAsync().ConfigureAwait(false);

        await _powerModeListener.NotifyAsync(state).ConfigureAwait(false);
    }

    public async Task EnsureCorrectPowerPlanIsSetAsync()
    {
        var compatibility = await Compatibility.IsCompatibleAsync().ConfigureAwait(false);
        if (compatibility.isCompatible) {
            var state = await GetStateAsync().ConfigureAwait(false);
            await _powerPlanController.ActivatePowerPlanAsync(state, true).ConfigureAwait(false);
        }
    }

    public async Task EnsureGodModeStateIsAppliedAsync()
    {
        var state = await GetStateAsync().ConfigureAwait(false);
        if (state != PowerModeState.GodMode)
            return;

        await _godModeController.ApplyStateAsync().ConfigureAwait(false);
    }

    public async Task EnsureAiModeIsSetAsync()
    {
        var compatibility = await Compatibility.IsCompatibleAsync().ConfigureAwait(false);
        if (compatibility.isCompatible)
        {
            var state = await GetStateAsync().ConfigureAwait(false);
            await _aiModeController.StartAsync(state).ConfigureAwait(false);
        }
    }

    public async Task EnsureAiModeIsOffAsync()
    {
        var compatibility = await Compatibility.IsCompatibleAsync().ConfigureAwait(false);
        if (compatibility.isCompatible)
        {
            var state = await GetStateAsync().ConfigureAwait(false);
            await _aiModeController.StopAsync(state).ConfigureAwait(false);
        }
    }
}