using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.Utils;
using System;
using System.Diagnostics;
using System.IO;

namespace LenovoLegionToolkit.Lib.System
{
    public class KX
    {
        private ProcessStartInfo startInfo;
        private ProcessStartInfo msrcmdStartInfo;

        private string path;
        private string msrcmdPath;

        private string cpuType;
        private string mchbar;

        private bool isKX;

        // Package Power Limit (PACKAGE_RAPL_LIMIT_0_0_0_MCHBAR_PCU) — Offset 59A0h
        private const string pnt_limit = "59";
        private const string pnt_clock = "94";

        public KX()
        {
            path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Intel", "KX", "KX.exe");
            msrcmdPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Intel", "MSR", "msr-cmd.exe");

            if (!File.Exists(path))
            {
                if (Log.Instance.IsTraceEnabled)
                {
                    Log.Instance.Trace($"KX.exe is missing. We may not be able to change MMIO and MSR power limits.");
                    return;
                }
            }

            if (!File.Exists(msrcmdPath))
            {
                if (Log.Instance.IsTraceEnabled)
                {
                    Log.Instance.Trace($"msr-cmd.exe is missing. We may not be able to change MSR power limits.");
                    return;
                }
            }

            startInfo = new ProcessStartInfo(path)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            msrcmdStartInfo = new ProcessStartInfo(msrcmdPath)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
        }

        internal bool init()
        {
            if (startInfo == null)
                return false;

            startInfo.Arguments = "/RdPci32 0 0 0 0x48";
            using (var ProcessOutput = Process.Start(startInfo))
            {
                while (!ProcessOutput.StandardOutput.EndOfStream)
                {
                    string line = ProcessOutput.StandardOutput.ReadLine();

                    if (!line.Contains("Return"))
                        continue;

                    // parse result
                    line = StringExtensions.Between(line, "Return ");
                    long returned = long.Parse(line);
                    string output = "0x" + returned.ToString("X2").Substring(0, 4);

                    mchbar = output;

                    ProcessOutput.Close();
                    isKX = true;
                    return true;
                }
                ProcessOutput.Close();
            }

            isKX = false;

            determineCPU();
            if (mchbar != "")
                return true;

            return false;
        }

        void determineCPU()
        {
            try
            {
                if (cpuType != "Intel" && cpuType != "AMD")
                {
                    //Get the processor name to determine intel vs AMD
                    object processorNameRegistry = Microsoft.Win32.Registry.GetValue("HKEY_LOCAL_MACHINE\\hardware\\description\\system\\centralprocessor\\0", "ProcessorNameString", null);
                    string processorName = null;
                    if (processorNameRegistry != null)
                    {
                        //If not null, find intel or AMD string and clarify type. For Intel determine MCHBAR for rw.exe
                        processorName = processorNameRegistry.ToString();
                        if (processorName.IndexOf("Intel") >= 0) { cpuType = "Intel"; }
                    }
                }
                if (cpuType == "Intel" && mchbar == "")
                {
                    determineIntelMCHBAR();
                }
            }
            catch (Exception)
            { }
        }

        void determineIntelMCHBAR()
        {
            try
            {
                //Get the processor model to determine MCHBAR, INTEL ONLY
                object processorModelRegistry = Microsoft.Win32.Registry.GetValue("HKEY_LOCAL_MACHINE\\hardware\\description\\system\\centralprocessor\\0", "Identifier", null);
                string processorModel = null;
                if (processorModelRegistry != null)
                {
                    //If not null, convert to string and determine MCHBAR for rw.exe
                    processorModel = processorModelRegistry.ToString();
                    if (processorModel.IndexOf("Model 140") >= 0) { mchbar = "0xFEDC59"; } else { mchbar = "0xFED159"; };
                }
            }
            catch (Exception)
            { }
        }


        internal int get_short_limit(bool msr)
        {
            switch (msr)
            {
                default:
                case false:
                    if (isKX)
                        return get_limit("a4");
                    else
                        return 0;
                case true:
                    return get_msr_limit(0);
            }
        }

        internal int get_long_limit(bool msr)
        {
            switch (msr)
            {
                default:
                case false:
                    if (isKX)
                        return get_limit("a0");
                    else 
                        return 0;
                case true:
                    return get_msr_limit(1);
            }
        }

        internal int get_limit(string pointer)
        {
            startInfo.Arguments = $"/rdmem16 {mchbar}{pnt_limit}{pointer}";
            using (var ProcessOutput = Process.Start(startInfo))
            {
                try
                {
                    while (!ProcessOutput.StandardOutput.EndOfStream)
                    {
                        string line = ProcessOutput.StandardOutput.ReadLine();

                        if (!line.Contains("Return"))
                            continue;

                        // parse result
                        line = StringExtensions.Between(line, "Return ");
                        long returned = long.Parse(line);
                        var output = ((double)returned + short.MinValue) / 8.0d;

                        ProcessOutput.Close();
                        return (int)output;
                    }
                }
                catch (Exception) { }
                ProcessOutput.Close();
            }

            return -1; // failed
        }

        internal int get_msr_limit(int pointer)
        {
            if (isKX)
            {
                startInfo.Arguments = $"/rdmsr 0x610";
                using (var ProcessOutput = Process.Start(startInfo))
                {
                    try
                    {
                        while (!ProcessOutput.StandardOutput.EndOfStream)
                        {
                            string line = ProcessOutput.StandardOutput.ReadLine();

                            if (!line.Contains("Msr Data"))
                                continue;

                            // parse result
                            line = StringExtensions.Between(line, "Msr Data     : ");

                            var values = line.Split(" ");
                            var hex = values[pointer];
                            hex = values[pointer].Substring(hex.Length - 3);
                            var output = Convert.ToInt32(hex, 16) / 8;

                            ProcessOutput.Close();
                            return (int)output;
                        }
                    }
                    catch (Exception) { }
                    ProcessOutput.Close();
                }
            }
            else
            {
                msrcmdStartInfo.Arguments = $"read 0x610";
                using (var ProcessOutput = Process.Start(msrcmdStartInfo))
                {
                    try
                    {
                        while (!ProcessOutput.StandardOutput.EndOfStream)
                        {
                            string line = ProcessOutput.StandardOutput.ReadLine();

                            if (!line.Contains("0        0x00000610 "))
                                continue;

                            // parse result
                            line = StringExtensions.Between(line, "0        0x00000610 ");

                            var values = line.Split(" ");
                            var hex = values[pointer];
                            hex = values[pointer].Substring(hex.Length - 3);
                            var output = Convert.ToInt32(hex, 16) / 8;

                            ProcessOutput.Close();
                            return (int)output;
                        }
                    }
                    catch (Exception) { }
                    ProcessOutput.Close();
                }
            }
            return -1; // failed
        }

        internal int get_short_value()
        {
            return -1; // not supported
        }

        internal int get_long_value()
        {
            return -1; // not supported
        }

        internal int set_short_limit(int limit, bool msr)
        {
            switch (msr)
            {
                default:
                case false:
                    if (isKX)
                        return set_limit("a4", limit);
                    else
                        return -1;
                case true:
                    return set_msr_limit("438", limit);
            }
        }

        internal int set_long_limit(int limit, bool msr)
        {
            switch(msr)
            {
                default:
                case false:
                    if (isKX)
                        return set_limit("a0", limit);
                    else
                        return -1;
                case true:
                    return set_msr_limit("DD8", limit);
            }
        }

        internal int set_limit(string pointer1, int limit)
        {
            string hex = TDPToHex(limit);

            // register command
            startInfo.Arguments = $"/wrmem16 {mchbar}{pnt_limit}{pointer1} 0x8{hex.Substring(0, 1)}{hex.Substring(1)}";
            using (var ProcessOutput = Process.Start(startInfo))
            {
                ProcessOutput.StandardOutput.ReadToEnd();
                ProcessOutput.Close();
            }

            return 0; // implement error code support
        }

        internal int set_msr_limits(int PL1, int PL2)
        {
            string hexPL1 = TDPToHex(PL1);
            string hexPL2 = TDPToHex(PL2);

            if (isKX)
            {
                // register command
                startInfo.Arguments = $"/wrmsr 0x610 0x00438{hexPL2} 00DD8{hexPL1}";
                using (var ProcessOutput = Process.Start(startInfo))
                {
                    try
                    {
                        ProcessOutput.StandardOutput.ReadToEnd();
                        ProcessOutput.Close();
                        return 0;
                    }
                    catch (Exception)
                    { }
                    ProcessOutput.Close();
                }
            }
            else
            {
                // register command
                msrcmdStartInfo.Arguments = $"-s write 0x610 0x00DD8{hexPL2} 00DD8{hexPL1}";
                using (var ProcessOutput = Process.Start(msrcmdStartInfo))
                {
                    try
                    {
                        ProcessOutput.StandardOutput.ReadToEnd();
                        ProcessOutput.Close();
                        return 0;
                    }
                    catch (Exception)
                    { }
                    ProcessOutput.Close();
                }
            }
            return -1; // implement error code support
        }
        
        internal int set_msr_limit(string pointer, int limit)
        {
            string hex = TDPToHex(limit);

            // register command
  
            startInfo.Arguments = $"/wrmsr 0x610 0x00{pointer}{hex}";
            using (var ProcessOutput = Process.Start(startInfo))
            {
                try
                {
                    ProcessOutput.StandardOutput.ReadToEnd();
                    ProcessOutput.Close();
                    return 0;
                }
                catch (Exception)
                { }
                ProcessOutput.Close();
            }

            return -1; // implement error code support
        }

        private string TDPToHex(int decValue)
        {
            decValue *= 8;
            string output = decValue.ToString("X3");
            return output;
        }

        private string ClockToHex(int decValue)
        {
            decValue /= 50;
            string output = "0x" + decValue.ToString("X2");
            return output;
        }

        internal int set_gfx_clk(int clock)
        {
            string hex = ClockToHex(clock);

            string command = $"/wrmem8 {mchbar}{pnt_clock} {hex}";

            startInfo.Arguments = command;
            using (var ProcessOutput = Process.Start(startInfo))
            {
                ProcessOutput.StandardOutput.ReadToEnd();
                ProcessOutput.Close();
            }

            return 0; // implement error code support
        }

        internal int get_gfx_clk()
        {
            startInfo.Arguments = $"/rdmem8 {mchbar}{pnt_clock}";
            using (var ProcessOutput = Process.Start(startInfo))
            {
                try
                {
                    while (!ProcessOutput.StandardOutput.EndOfStream)
                    {
                        string line = ProcessOutput.StandardOutput.ReadLine();

                        if (!line.Contains("Return"))
                            continue;

                        // parse result
                        line = StringExtensions.Between(line, "Return ");
                        int returned = int.Parse(line);
                        var clock = returned * 50;

                        ProcessOutput.Close();
                        return clock;
                    }
                }
                catch (Exception) { }
                ProcessOutput.Close();
            }

            return -1; // failed
        }
    }
}
