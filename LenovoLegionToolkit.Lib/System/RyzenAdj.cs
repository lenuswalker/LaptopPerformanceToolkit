using System;
using System.Runtime.InteropServices;

namespace LenovoLegionToolkit.Lib.System;

// P/Invoke surface for libryzenadj.dll, shipped next to the executable together
// with WinRing0x64.dll/.sys and inpoutx64.dll (the official RyzenAdj release layout).
internal static class RyzenAdj
{
    private const string LIBRARY_NAME = "libryzenadj.dll";

    [DllImport(LIBRARY_NAME)] public static extern IntPtr init_ryzenadj();
    [DllImport(LIBRARY_NAME)] public static extern void cleanup_ryzenadj(IntPtr ry);
    [DllImport(LIBRARY_NAME)] public static extern RyzenFamily get_cpu_family(IntPtr ry);
    [DllImport(LIBRARY_NAME)] public static extern int refresh_table(IntPtr ry);

    [DllImport(LIBRARY_NAME)] public static extern int set_stapm_limit(IntPtr ry, uint value);
    [DllImport(LIBRARY_NAME)] public static extern int set_fast_limit(IntPtr ry, uint value);
    [DllImport(LIBRARY_NAME)] public static extern int set_slow_limit(IntPtr ry, uint value);

    [DllImport(LIBRARY_NAME)] public static extern float get_stapm_limit(IntPtr ry);
    [DllImport(LIBRARY_NAME)] public static extern float get_fast_limit(IntPtr ry);
    [DllImport(LIBRARY_NAME)] public static extern float get_slow_limit(IntPtr ry);
}
