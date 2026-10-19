using System;
using System.Linq;
using System.Threading;
using NvAPIWrapper;
using NvAPIWrapper.GPU;
using NvAPIWrapper.Native;
using NvAPIWrapper.Native.Exceptions;
using NvAPIWrapper.Native.General;
using NvAPIWrapper.Native.GPU;

namespace LenovoLegionToolkit.Lib.System;

internal static class NVAPI
{
    private static readonly object SessionLock = new();
    private static int _sessionCount;

    public static IDisposable AcquireSession()
    {
        lock (SessionLock)
        {
            if (_sessionCount == 0)
                NVIDIA.Initialize();

            _sessionCount++;
            return new Session();
        }
    }

    private sealed class Session : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            lock (SessionLock)
            {
                _sessionCount--;
                if (_sessionCount == 0)
                {
                    try { NVIDIA.Unload(); } catch { }
                }
            }
        }
    }

    public static PhysicalGPU? GetGPU()
    {
        try
        {
            return PhysicalGPU.GetPhysicalGPUs().FirstOrDefault(gpu => gpu.SystemType == SystemType.Laptop);
        }
        catch (NVIDIAApiException)
        {
            return null;
        }
    }

    public static bool IsDisplayConnected(PhysicalGPU gpu)
    {
        try
        {
            return GPUApi.GetConnectedDisplayIds(gpu.Handle, ConnectedIdsFlag.None).Any(display => display.IsConnected);
        }
        catch (NVIDIAApiException ex) when (ex.Status == Status.NvidiaDeviceNotFound)
        {
            return false;
        }
    }

    public static string? GetGPUId(PhysicalGPU gpu)
    {
        try
        {
            return gpu.BusInformation.PCIIdentifiers.ToString();
        }
        catch (NVIDIAApiException)
        {
            return null;
        }
    }

}
