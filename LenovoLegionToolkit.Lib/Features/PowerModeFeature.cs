using System;
using System.Collections.Generic;
using System.DirectoryServices.ActiveDirectory;
using System.Linq;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Controllers;
using LenovoLegionToolkit.Lib.Controllers.GodMode;
using LenovoLegionToolkit.Lib.Listeners;
using LenovoLegionToolkit.Lib.System;
using LenovoLegionToolkit.Lib.System.Management;
using LenovoLegionToolkit.Lib.Utils;

namespace LenovoLegionToolkit.Lib.Features;

public class PowerModeUnavailableWithoutACException(PowerModeState powerMode) : Exception
{
    public PowerModeState PowerMode { get; } = powerMode;
}

public class PowerModeFeature(
    GodModeController godModeController,
    WindowsPowerModeController windowsPowerModeController,
    WindowsPowerPlanController windowsPowerPlanController,
    ThermalModeListener thermalModeListener,
    PowerModeListener powerModeListener)
    : AbstractWmiFeature<PowerModeState>(WMI.LenovoGameZoneData.GetSmartFanModeAsync, WMI.LenovoGameZoneData.SetSmartFanModeAsync, WMI.LenovoGameZoneData.IsSupportSmartFanAsync, 1)
{
    public bool AllowAllPowerModesOnBattery { get; set; }

    private static readonly Dictionary<PowerModeState, string> defaultPowerModes = new()
    {
        { PowerModeState.Quiet , "961cc777-2547-4f9d-8174-7d86181b8a7a" },
        { PowerModeState.Balance , "381b4222-f694-41f0-9685-ff5bb260df2e" },
        { PowerModeState.Performance , "ded574b5-45a0-4f42-8737-46345c09c238" },
    };

    public override async Task<bool> IsSupportedAsync()
    {
        uint result = Power.PowerGetEffectiveOverlayScheme(out Guid currentMode);
        if (result == 0)
            return true;
        else
            return false;
    }

    public override async Task<PowerModeState[]> GetAllStatesAsync()
    {
        var mi = await Compatibility.GetMachineInformationAsync().ConfigureAwait(false);
        return mi.Properties.SupportsGodMode
            ? [PowerModeState.Quiet, PowerModeState.Balance, PowerModeState.Performance, PowerModeState.GodMode]
            : [PowerModeState.Quiet, PowerModeState.Balance, PowerModeState.Performance];
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
                    state = PowerModeState.Quiet;
                    break;
                case "381b4222-f694-41f0-9685-ff5bb260df2e":
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
            throw new InvalidOperationException($"Unsupported power mode {state}");

        if (state is PowerModeState.Performance or PowerModeState.GodMode
            && !AllowAllPowerModesOnBattery
            && await Power.IsPowerAdapterConnectedAsync().ConfigureAwait(false) is PowerAdapterStatus.Disconnected)
            throw new PowerModeUnavailableWithoutACException(state);

        var currentState = await GetStateAsync().ConfigureAwait(false);

        var mi = await Compatibility.GetMachineInformationAsync().ConfigureAwait(false);

        var compatibility = await Compatibility.IsCompatibleAsync().ConfigureAwait(false);
        if (compatibility.isCompatible)
        {
            if (mi.Properties.HasQuietToPerformanceModeSwitchingBug && currentState == PowerModeState.Quiet && state == PowerModeState.Performance)
            {
                thermalModeListener.SuppressNext();
                await base.SetStateAsync(PowerModeState.Balance).ConfigureAwait(false);
                await Task.Delay(TimeSpan.FromMilliseconds(500)).ConfigureAwait(false);
            }

            if (mi.Properties.HasGodModeToOtherModeSwitchingBug && currentState == PowerModeState.GodMode && state != PowerModeState.GodMode)
            {
                thermalModeListener.SuppressNext();

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

            thermalModeListener.SuppressNext();
            await base.SetStateAsync(state).ConfigureAwait(false);
        }
        else
            Power.PowerSetActiveOverlayScheme(new Guid(defaultPowerModes[state]));

        await powerModeListener.NotifyAsync(state).ConfigureAwait(false);
    }

    public async Task EnsureCorrectWindowsPowerSettingsAreSetAsync()
    {
        var state = await GetStateAsync().ConfigureAwait(false);
        await windowsPowerModeController.SetPowerModeAsync(state).ConfigureAwait(false);
        await windowsPowerPlanController.SetPowerPlanAsync(state, true).ConfigureAwait(false);
    }

    public async Task EnsureGodModeStateIsAppliedAsync()
    {
        var state = await GetStateAsync().ConfigureAwait(false);
        if (state != PowerModeState.GodMode)
            return;

        await godModeController.ApplyStateAsync().ConfigureAwait(false);
    }
}
