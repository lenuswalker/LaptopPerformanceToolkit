namespace LenovoLegionToolkit.Lib.Services;

public enum GPUPowerSavingStatus
{
    Disabled,
    OnAC,
    Checking,
    ActiveApplications,
    DisplayConnected,
    WaitingForIdle,
    Restarting,
    WaitingForSleep,
    Asleep,
    RestartFailed,
    Unsupported,
    Unknown,
    Suspended
}
