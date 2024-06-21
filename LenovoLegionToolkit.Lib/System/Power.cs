using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.System.Management;
using LenovoLegionToolkit.Lib.Utils;
using Windows.Win32;

namespace LenovoLegionToolkit.Lib.System;

public static class Power
{
    public static async Task<PowerAdapterStatus> IsPowerAdapterConnectedAsync()
    {
        if (!PInvoke.GetSystemPowerStatus(out var sps))
            return PowerAdapterStatus.Connected;

        var adapterConnected = sps.ACLineStatus == 1;
        var acFitForOc = await IsAcFitForOc().ConfigureAwait(false) ?? true;
        var chargingNormally = await IsChargingNormally().ConfigureAwait(false) ?? true;

        return (adapterConnected, acFitForOc && chargingNormally) switch
        {
            (true, false) => PowerAdapterStatus.ConnectedLowWattage,
            (true, _) => PowerAdapterStatus.Connected,
            (false, _) => PowerAdapterStatus.Disconnected,
        };
    }

    public static bool IsBatterySaverEnabled()
    {
        if (!PInvoke.GetSystemPowerStatus(out var sps))
            return false;

        return sps.SystemStatusFlag == 1;
    }

    public static async Task RestartAsync()
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Restarting...");

        await CMD.RunAsync("shutdown", "/r /t 0").ConfigureAwait(false);
    }

    private static async Task<bool?> IsAcFitForOc()
    {
        try
        {
            var result = await WMI.LenovoGameZoneData.IsACFitForOCAsync().ConfigureAwait(false);

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Mode = {result}");

            return result == 1;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<bool?> IsChargingNormally()
    {
        try
        {
            var result = await WMI.LenovoGameZoneData.GetPowerChargeModeAsync().ConfigureAwait(false);

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Mode = {result}");

            return result == 1;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Retrieves the active overlay power scheme and returns a GUID that identifies the scheme.
    /// </summary>
    /// <param name="EffectiveOverlayPolicyGuid">A pointer to a GUID structure.</param>
    /// <returns>Returns zero if the call was successful, and a nonzero value if the call failed.</returns>
    [DllImportAttribute("powrprof.dll", EntryPoint = "PowerGetEffectiveOverlayScheme")]
    public static extern uint PowerGetEffectiveOverlayScheme(out Guid EffectiveOverlayPolicyGuid);

    /// <summary>
    /// Sets the active power overlay power scheme.
    /// </summary>
    /// <param name="OverlaySchemeGuid">The identifier of the overlay power scheme.</param>
    /// <returns>Returns zero if the call was successful, and a nonzero value if the call failed.</returns>
    [DllImportAttribute("powrprof.dll", EntryPoint = "PowerSetActiveOverlayScheme")]
    public static extern uint PowerSetActiveOverlayScheme(Guid OverlaySchemeGuid);
}