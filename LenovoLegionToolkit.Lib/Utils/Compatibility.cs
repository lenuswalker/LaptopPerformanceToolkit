﻿using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.System;

namespace LenovoLegionToolkit.Lib.Utils;

public static class Compatibility
{
    private static readonly string _allowedVendor = "LENOVO";

    private static readonly string[] _allowedModelsPrefix = {
        "17ACH",
        "17ARH",
        "17ITH",
        "17IMH",

        "16ACH",
        "16ARH",
        "16IAH",
        "16IAX",
        "16ITH",

        "15ACH",
        "15ARH",
        "15IAH",
        "15IHU",
        "15IMH",
        "15ITH",

        "R9000",
        "R7000",
        "Y9000",
        "Y7000",
            
        // Limited compatibility
        "17IR",
        "15IR",
        "15ICH"
    };

    private static MachineInformation? _machineInformation;

    public static Task<bool> CheckBasicCompatibilityAsync() => WMI.ExistsAsync("root\\WMI", $"SELECT * FROM LENOVO_GAMEZONE_DATA");

    public static async Task<(bool isCompatible, MachineInformation machineInformation)> IsCompatibleAsync()
    {
        var mi = await GetMachineInformationAsync().ConfigureAwait(false);

        if (!await CheckBasicCompatibilityAsync().ConfigureAwait(false))
            return (false, mi);

        if (!mi.Vendor.Equals(_allowedVendor, StringComparison.InvariantCultureIgnoreCase))
            return (false, mi);

        foreach (var allowedModel in _allowedModelsPrefix)
            if (mi.Model.Contains(allowedModel, StringComparison.InvariantCultureIgnoreCase))
                return (true, mi);

        return (false, mi);
    }

    public static async Task<MachineInformation> GetMachineInformationAsync()
    {
        if (!_machineInformation.HasValue)
        {
            var (vendor, machineType, model, serialNumber) = await GetModelDataAsync().ConfigureAwait(false);
            var biosVersion = await GetBIOSVersionAsync().ConfigureAwait(false);

            var machineInformation = new MachineInformation
            {
                Vendor = vendor,
                MachineType = machineType,
                Model = model,
                SerialNumber = serialNumber,
                BiosVersion = biosVersion,
                Properties = new()
                {
                    SupportsGodMode = GetSupportsGodMode(biosVersion),
                    SupportsExtendedHybridMode = await GetSupportsExtendedHybridModeAsync().ConfigureAwait(false),
                    SupportsIntelligentSubMode = await GetSupportsIntelligentSubModeAsync().ConfigureAwait(false),
                    HasPerformanceModeSwitchingBug = GetHasPerformanceModeSwitchingBug(biosVersion)
                }
            };

            if (Log.Instance.IsTraceEnabled)
            {
                Log.Instance.Trace($"Retrieved machine information:");
                Log.Instance.Trace($" * Vendor: '{machineInformation.Vendor}'");
                Log.Instance.Trace($" * Machine Type: '{machineInformation.MachineType}'");
                Log.Instance.Trace($" * Model: '{machineInformation.Model}'");
                Log.Instance.Trace($" * BIOS: '{machineInformation.BiosVersion}'");
                Log.Instance.Trace($" * SupportsGodMode: '{machineInformation.Properties.SupportsGodMode}'");
                Log.Instance.Trace($" * SupportsExtendedHybridMode: '{machineInformation.Properties.SupportsExtendedHybridMode}'");
                Log.Instance.Trace($" * SupportsIntelligentSubMode: '{machineInformation.Properties.SupportsIntelligentSubMode}'");
            }

            _machineInformation = machineInformation;
        }

        return _machineInformation.Value;
    }

    private static bool GetSupportsGodMode(string currentBiosVersionString)
    {
        (string, int)[] supportedBiosVersions =
        {
            ("GKCN", 49),
            ("G9CN", 30),
            ("H1CN", 49),
            ("HACN", 31),
            ("HHCN", 23),
            ("K1CN", 31),
            ("K9CN", 34),
            ("KFCN", 32),
            ("J2CN", 40),
            ("JUCN", 51),
            ("JYCN", 39)
        };

        var prefixRegex = new Regex("^[A-Z0-9]{4}");
        var versionRegex = new Regex("[0-9]{2}");

        var currentPrefix = prefixRegex.Match(currentBiosVersionString).Value;
        var currentVersionString = versionRegex.Match(currentBiosVersionString).Value;

        if (!int.TryParse(versionRegex.Match(currentVersionString).Value, out var currentVersion))
            return false;

        foreach (var (prefix, minimumVersion) in supportedBiosVersions)
        {
            if (currentPrefix.Equals(prefix, StringComparison.InvariantCultureIgnoreCase) && currentVersion >= minimumVersion)
                return true;
        }

        return false;
    }

    private static async Task<bool> GetSupportsExtendedHybridModeAsync()
    {
        try
        {
            var result = await WMI.CallAsync("root\\WMI",
                $"SELECT * FROM LENOVO_GAMEZONE_DATA",
                "IsSupportIGPUMode",
                new(),
                pdc => (uint)pdc["Data"].Value).ConfigureAwait(false);
            return result > 0;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> GetSupportsIntelligentSubModeAsync()
    {
        try
        {
            _ = await WMI.CallAsync("root\\WMI",
                $"SELECT * FROM LENOVO_GAMEZONE_DATA",
                "GetIntelligentSubMode",
                new(),
                pdc => (uint)pdc["Data"].Value).ConfigureAwait(false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<(string, string, string, string)> GetModelDataAsync()
    {
        var result = await WMI.ReadAsync("root\\CIMV2",
            $"SELECT * FROM Win32_ComputerSystemProduct",
            pdc =>
            {
                var machineType = (string)pdc["Name"].Value;
                var vendor = (string)pdc["Vendor"].Value;
                var model = (string)pdc["Version"].Value;
                var serialNumber = (string)pdc["IdentifyingNumber"].Value;
                return (vendor, machineType, model, serialNumber);
            }).ConfigureAwait(false);
        return result.First();
    }

    private static async Task<string> GetBIOSVersionAsync()
    {
        var result = await WMI.ReadAsync("root\\CIMV2",
            $"SELECT * FROM Win32_BIOS",
            pdc => (string)pdc["Name"].Value).ConfigureAwait(false);
        return result.First();
    }

    private static bool GetHasPerformanceModeSwitchingBug(string biosVersion)
    {
        (string, int?)[] affectedBiosList =
        {
            ("J2CN", null)
        };

        foreach (var (biosPrefix, maximumVersion) in affectedBiosList)
        {
            if (biosVersion.StartsWith(biosPrefix)
                && (maximumVersion == null || int.TryParse(biosVersion.Replace(biosPrefix, null).Replace("WW", null), out var rev) && rev <= maximumVersion))
                return true;
        }

        return false;
    }
}