using System;
using System.IO;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.Utils;

namespace LenovoLegionToolkit.Lib.System;

// Thin wrapper around the KX utility (MMIO/MSR access) with msr-cmd.exe as
// fallback for MSR writes. All calls spawn a short-lived process on demand.
public class KX
{
    // Package Power Limit (PACKAGE_RAPL_LIMIT_0_0_0_MCHBAR_PCU) - offset 59A0h.
    private const string LIMIT_OFFSET = "59";
    private const string PL1_POINTER = "a0";
    private const string PL2_POINTER = "a4";

    private readonly string _kxPath = Path.Combine(AppContext.BaseDirectory, "Resources", "Intel", "KX", "KX.exe");
    private readonly string _msrCmdPath = Path.Combine(AppContext.BaseDirectory, "Resources", "Intel", "MSR", "msr-cmd.exe");

    private string? _mchbar;
    private bool _useKxForMsr;

    public async Task<bool> InitAsync()
    {
        if (!File.Exists(_kxPath))
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"KX.exe is missing. MMIO and MSR power limits are unavailable.");

            return false;
        }

        var value = await ReadReturnValueAsync($"/RdPci32 0 0 0 0x48").ConfigureAwait(false);
        if (value is not null)
        {
            // High bytes of the MCHBAR address, e.g. 0xFEDC.
            _mchbar = "0x" + value.Value.ToString("X8")[..4];
            _useKxForMsr = true;

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"MCHBAR read from PCI. [mchbar={_mchbar}]");

            return true;
        }

        _mchbar = DetermineMchbarFromRegistry();
        _useKxForMsr = false;

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"MCHBAR from registry fallback. [mchbar={_mchbar ?? "<null>"}]");

        return _mchbar is not null;
    }

    public async Task<int> GetTDPLimitAsync(PowerType type)
    {
        var pointer = PointerFor(type);
        if (pointer is null || _mchbar is null)
            return -1;

        var value = await ReadReturnValueAsync($"/rdmem16 {_mchbar}{LIMIT_OFFSET}{pointer}").ConfigureAwait(false);
        if (value is null)
            return -1;

        return (int)((value.Value + short.MinValue) / 8.0);
    }

    public async Task<int> SetTDPLimitAsync(PowerType type, int watts)
    {
        var pointer = PointerFor(type);
        if (pointer is null || _mchbar is null)
            return -1;

        var hex = TDPToHex(watts);
        await CMD.RunAsync(_kxPath, $"/wrmem16 {_mchbar}{LIMIT_OFFSET}{pointer} 0x8{hex}").ConfigureAwait(false);
        return 0;
    }

    public async Task<int> SetMSRLimitsAsync(int pl1, int pl2)
    {
        var hexPL1 = TDPToHex(pl1);
        var hexPL2 = TDPToHex(pl2);

        if (_useKxForMsr)
        {
            await CMD.RunAsync(_kxPath, $"/wrmsr 0x610 0x00438{hexPL2} 00DD8{hexPL1}").ConfigureAwait(false);
            return 0;
        }

        if (!File.Exists(_msrCmdPath))
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"msr-cmd.exe is missing. MSR power limits are unavailable.");

            return -1;
        }

        await CMD.RunAsync(_msrCmdPath, $"-s write 0x610 0x00438{hexPL2} 00DD8{hexPL1}").ConfigureAwait(false);
        return 0;
    }

    private async Task<long?> ReadReturnValueAsync(string arguments)
    {
        try
        {
            var (_, output) = await CMD.RunAsync(_kxPath, arguments).ConfigureAwait(false);

            foreach (var line in output.Split('\n'))
            {
                if (!line.Contains("Return "))
                    continue;

                if (long.TryParse(line.Between("Return ").Trim(), out var value))
                    return value;
            }
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"KX call failed. [arguments={arguments}]", ex);
        }

        return null;
    }

    private static string? PointerFor(PowerType type) => type switch
    {
        PowerType.Slow => PL1_POINTER,
        PowerType.Fast => PL2_POINTER,
        _ => null
    };

    private static string TDPToHex(int watts) => (watts * 8).ToString("X3");

    private static string? DetermineMchbarFromRegistry()
    {
        try
        {
            var processorName = Microsoft.Win32.Registry.GetValue(@"HKEY_LOCAL_MACHINE\HARDWARE\DESCRIPTION\System\CentralProcessor\0", "ProcessorNameString", null) as string;
            if (processorName is null || !processorName.Contains("Intel"))
                return null;

            var processorModel = Microsoft.Win32.Registry.GetValue(@"HKEY_LOCAL_MACHINE\HARDWARE\DESCRIPTION\System\CentralProcessor\0", "Identifier", null) as string;
            if (processorModel is null)
                return null;

            return processorModel.Contains("Model 140") ? "0xFEDC" : "0xFED1";
        }
        catch
        {
            return null;
        }
    }
}
