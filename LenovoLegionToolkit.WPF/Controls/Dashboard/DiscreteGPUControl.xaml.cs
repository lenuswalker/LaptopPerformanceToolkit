using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Primitives;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Controllers;
using LenovoLegionToolkit.Lib.Listeners;
using LenovoLegionToolkit.Lib.Services;
using LenovoLegionToolkit.Lib.Settings;
using LenovoLegionToolkit.WPF.Resources;

namespace LenovoLegionToolkit.WPF.Controls.Dashboard;

public partial class DiscreteGPUControl
{
    private readonly GPUController _gpuController = IoCContainer.Resolve<GPUController>();
    private readonly NativeWindowsMessageListener _nativeWindowsMessageListener = IoCContainer.Resolve<NativeWindowsMessageListener>();
    private readonly GPUKeepOffMonitorService _gpuKeepOffMonitorService = IoCContainer.Resolve<GPUKeepOffMonitorService>();
    private readonly ApplicationSettings _settings = IoCContainer.Resolve<ApplicationSettings>();
    private GPUState _lastState = GPUState.Unknown;
    private bool _actionInProgress;
    private bool _isLoaded;
    private Task? _pendingGpuRefresh;

    public DiscreteGPUControl()
    {
        InitializeComponent();

        Loaded += DiscreteGPUControl_Loaded;
        Unloaded += DiscreteGPUControl_Unloaded;
        IsVisibleChanged += DiscreteGPUControl_IsVisibleChanged;

        _keepOffOnBatteryMenuItem.IsChecked = _settings.Store.KeepGPUOffOnBattery;
        SetUnknownStatus(Resource.DiscreteGPUControl_Status_Checking);
        UpdatePowerSavingStatus(_gpuKeepOffMonitorService.Status);
    }

    private void DiscreteGPUControl_Loaded(object sender, RoutedEventArgs e)
    {
        _gpuController.Refreshed += GpuController_Refreshed;
        _nativeWindowsMessageListener.Changed += NativeWindowsMessageListener_Changed;
        _gpuKeepOffMonitorService.StatusChanged += GpuKeepOffMonitorService_StatusChanged;
        _isLoaded = true;
        UpdatePowerSavingStatus(_gpuKeepOffMonitorService.Status);
        if (IsVisible)
            _ = RefreshGPUAsync();
    }

    private void DiscreteGPUControl_Unloaded(object sender, RoutedEventArgs e)
    {
        _gpuController.Refreshed -= GpuController_Refreshed;
        _nativeWindowsMessageListener.Changed -= NativeWindowsMessageListener_Changed;
        _gpuKeepOffMonitorService.StatusChanged -= GpuKeepOffMonitorService_StatusChanged;
        _isLoaded = false;
    }

    protected override void OnFinishedLoading() { }

    protected override async Task OnRefreshAsync()
    {
        if (IsVisible)
            await RefreshGPUAsync();
    }

    private Task RefreshGPUAsync()
    {
        if (_pendingGpuRefresh is not { IsCompleted: false })
            _pendingGpuRefresh = RefreshGPUCoreAsync();
        return _pendingGpuRefresh;
    }

    private async Task RefreshGPUCoreAsync()
    {
        try
        {
            try { await _gpuController.RefreshNowAsync(); }
            catch { SetUnknownStatus(Resource.DiscreteGPUControl_Status_Unknown); }
            if (_isLoaded && IsVisible)
            {
                try { await _gpuController.StartAsync(); } catch { }
            }
        }
        finally
        {
            _pendingGpuRefresh = null;
        }
    }

    private async void NativeWindowsMessageListener_Changed(object? sender, NativeWindowsMessageListener.ChangedEventArgs e)
    {
        if (e.Message != NativeWindowsMessage.OnDisplayDeviceArrival)
            return;

        Visibility = Visibility.Visible;
        await RefreshAsync();
    }

    private async void DiscreteGPUControl_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_isLoaded)
        {
            if (IsVisible)
                _ = RefreshGPUAsync();
            else
                await _gpuController.StopAsync();
        }
    }

    private void GpuController_Refreshed(object? sender, GPUStatus e) => Dispatcher.BeginInvoke(() =>
    {
        _lastState = e.State;
        var tooltipStringBuilder = new StringBuilder(Resource.DiscreteGPUControl_PerformanceState);
        tooltipStringBuilder.AppendLine().Append(" · ").Append(e.PerformanceState ?? Resource.DiscreteGPUControl_PerformanceState_Unknown);

        if (e.State is GPUState.Unknown or GPUState.NvidiaGpuNotFound)
        {
            _discreteGPUStatusActiveIndicator.Visibility = Visibility.Collapsed;
            _discreteGPUStatusInactiveIndicator.Visibility = Visibility.Collapsed;
            _discreteGPUStatusPoweredOffIndicator.Visibility = Visibility.Collapsed;
            _discreteGPUStatusUnknownIndicator.Visibility = Visibility.Visible;
            _discreteGPUStatusDescription.Text = e.State is GPUState.NvidiaGpuNotFound ? Resource.DiscreteGPUControl_Status_NotDetected : Resource.DiscreteGPUControl_Status_Unknown;
            _gpuInfoButton.ToolTip = null;
            _gpuInfoButton.IsEnabled = false;
            _gpuProcessesStatus.Text = string.Empty;
            _deactivateGPUButton.IsEnabled = true;
            _killAppsMenuItem.IsEnabled = false;
            _restartGPUMenuItem.IsEnabled = false;
            _content.Visibility = Visibility.Visible;
            UpdatePowerSavingStatus(_gpuKeepOffMonitorService.Status);
            return;
        }

        if (e.State is GPUState.MonitorConnected)
            tooltipStringBuilder.AppendLine().AppendLine().Append(Resource.DiscreteGPUControl_MonitorConnected);

        if (e.State is GPUState.Active or GPUState.MonitorConnected)
        {
            var processesStringBuilder = new StringBuilder();

            if (e.ProcessCount > 0)
            {
                processesStringBuilder.Append(Resource.DiscreteGPUControl_Processes);
                foreach (var processName in e.Processes.Select(p => { try { return p.ProcessName; } catch { return null; } }).Where(name => name is not null).OrderBy(name => name))
                {
                    processesStringBuilder.AppendLine().Append(" · ").Append(processName);
                }
            }
            else
            {
                processesStringBuilder.Append(Resource.DiscreteGPUControl_NoProcesses);
            }

            _discreteGPUStatusActiveIndicator.Fill = e.State is GPUState.MonitorConnected ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF2, 0xA5, 0x41)) : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE5, 0x39, 0x35));
            _discreteGPUStatusActiveIndicator.Visibility = Visibility.Visible;
            _discreteGPUStatusInactiveIndicator.Visibility = Visibility.Collapsed;
            _discreteGPUStatusPoweredOffIndicator.Visibility = Visibility.Collapsed;
            _discreteGPUStatusUnknownIndicator.Visibility = Visibility.Collapsed;
            _discreteGPUStatusDescription.Text = e.State is GPUState.MonitorConnected ? Resource.DiscreteGPUControl_Status_DisplayConnectedSimple : Resource.Active;
            _gpuInfoButton.ToolTip = tooltipStringBuilder.AppendLine().AppendLine().Append(processesStringBuilder).ToString();
            _gpuInfoButton.IsEnabled = true;
            var processNames = e.Processes.Select(p => { try { return p.ProcessName; } catch { return null; } }).Where(name => name is not null).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(name => name).ToArray();
            _gpuProcessesStatus.Text = processNames.Length > 0 ? $"{processNames.Length} · {string.Join(", ", processNames)}" : Resource.DiscreteGPUControl_NoProcesses;
        }
        else if (e.State is GPUState.PoweredOff)
        {
            _discreteGPUStatusActiveIndicator.Visibility = Visibility.Collapsed;
            _discreteGPUStatusInactiveIndicator.Visibility = Visibility.Collapsed;
            _discreteGPUStatusPoweredOffIndicator.Visibility = Visibility.Visible;
            _discreteGPUStatusUnknownIndicator.Visibility = Visibility.Collapsed;
            _discreteGPUStatusDescription.Text = Resource.DiscreteGPUControl_Status_Asleep;
            _gpuInfoButton.ToolTip = tooltipStringBuilder.ToString();
            _gpuInfoButton.IsEnabled = true;
            _gpuProcessesStatus.Text = string.Empty;
        }
        else
        {
            _discreteGPUStatusActiveIndicator.Visibility = Visibility.Collapsed;
            _discreteGPUStatusInactiveIndicator.Visibility = Visibility.Visible;
            _discreteGPUStatusPoweredOffIndicator.Visibility = Visibility.Collapsed;
            _discreteGPUStatusUnknownIndicator.Visibility = Visibility.Collapsed;
            _discreteGPUStatusDescription.Text = Resource.DiscreteGPUControl_Status_IdlePoweredOn;
            _gpuInfoButton.ToolTip = tooltipStringBuilder.ToString();
            _gpuInfoButton.IsEnabled = true;
            _gpuProcessesStatus.Text = string.Empty;
        }

        _killAppsMenuItem.IsEnabled = !_actionInProgress && e.State is GPUState.Active;
        _restartGPUMenuItem.IsEnabled = !_actionInProgress && e.State is GPUState.Active or GPUState.Inactive;
        _deactivateGPUButton.IsEnabled = true;
        _deactivateGPUButtonText.SetResourceReference(ForegroundProperty, "TextOnAccentFillColorPrimaryBrush");
        _deactivateGPUButtonIcon.SetResourceReference(ForegroundProperty, "TextOnAccentFillColorPrimaryBrush");

        _content.Visibility = Visibility.Visible;
        UpdatePowerSavingStatus(_gpuKeepOffMonitorService.Status);
    });

    private void GpuKeepOffMonitorService_StatusChanged(object? sender, GPUPowerSavingStatus e) => Dispatcher.BeginInvoke(() => UpdatePowerSavingStatus(e));

    private void UpdatePowerSavingStatus(GPUPowerSavingStatus status)
    {
        _powerSavingStatus.Visibility = Visibility.Visible;
        if (!_settings.Store.KeepGPUOffOnBattery)
        {
            _powerSavingStatus.Text = Resource.DiscreteGPUControl_BatteryMonitoringOff;
            return;
        }
        var statusText = status switch
        {
            GPUPowerSavingStatus.Disabled => Resource.DiscreteGPUControl_Status_Disabled,
            GPUPowerSavingStatus.OnAC => Resource.DiscreteGPUControl_Status_OnAC,
            GPUPowerSavingStatus.Checking => Resource.DiscreteGPUControl_Status_Checking,
            GPUPowerSavingStatus.ActiveApplications => Resource.DiscreteGPUControl_Status_ActiveApplications,
            GPUPowerSavingStatus.DisplayConnected => Resource.DiscreteGPUControl_Status_DisplayConnected,
            GPUPowerSavingStatus.WaitingForIdle => Resource.DiscreteGPUControl_Status_WaitingForIdle,
            GPUPowerSavingStatus.Restarting => Resource.DiscreteGPUControl_Status_Restarting,
            GPUPowerSavingStatus.WaitingForSleep => Resource.DiscreteGPUControl_Status_WaitingForSleep,
            GPUPowerSavingStatus.Asleep when _lastState != GPUState.PoweredOff => Resource.DiscreteGPUControl_Status_Checking,
            GPUPowerSavingStatus.Asleep => Resource.DiscreteGPUControl_Status_Asleep,
            GPUPowerSavingStatus.RestartFailed => Resource.DiscreteGPUControl_Status_RestartFailed,
            GPUPowerSavingStatus.Unsupported => Resource.DiscreteGPUControl_Status_Unsupported,
            GPUPowerSavingStatus.Suspended => Resource.DiscreteGPUControl_Status_Suspended,
            _ => Resource.DiscreteGPUControl_Status_Unknown
        };
        _powerSavingStatus.Text = string.Format(Resource.DiscreteGPUControl_PowerSavingStatus, statusText);
    }

    private void SetUnknownStatus(string text)
    {
        _lastState = GPUState.Unknown;
        _discreteGPUStatusActiveIndicator.Visibility = Visibility.Collapsed;
        _discreteGPUStatusInactiveIndicator.Visibility = Visibility.Collapsed;
        _discreteGPUStatusPoweredOffIndicator.Visibility = Visibility.Collapsed;
        _discreteGPUStatusUnknownIndicator.Visibility = Visibility.Visible;
        _discreteGPUStatusDescription.Text = text;
        _content.Visibility = Visibility.Visible;
        _deactivateGPUButton.IsEnabled = true;
        _killAppsMenuItem.IsEnabled = false;
        _restartGPUMenuItem.IsEnabled = false;
    }

    private void DeactivateGPUButton_Click(object sender, RoutedEventArgs e)
    {
        if (_deactivateGPUButton.ContextMenu is null)
            return;

        _deactivateGPUButton.ContextMenu.PlacementTarget = _deactivateGPUButton;
        _deactivateGPUButton.ContextMenu.Placement = PlacementMode.Bottom;
        _deactivateGPUButton.ContextMenu.IsOpen = true;
    }

    private async void KillAppsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (ShowMessage(Resource.DiscreteGPUControl_KillApps_Confirm, Resource.DiscreteGPUControl_KillApps_ConfirmTitle, MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        await RunGpuActionAsync(() => _gpuController.KillGPUProcessesAsync());
    }

    private async void RestartGPUMenuItem_Click(object sender, RoutedEventArgs e)
    {
        SetActionInProgress(true);
        try
        {
            var result = await _gpuController.RestartGPUAsync();
            if (result is GPURestartResult.Skipped)
                ShowMessage(Resource.DiscreteGPUControl_RestartSkipped, Resource.DiscreteGPUControl_Title, MessageBoxButton.OK, MessageBoxImage.Information);
            else if (result is GPURestartResult.Failed or GPURestartResult.TimedOut)
                ShowMessage(string.Format(Resource.DiscreteGPUControl_ActionFailed, result), Resource.DiscreteGPUControl_Title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch (Exception ex)
        {
            ShowMessage(string.Format(Resource.DiscreteGPUControl_ActionFailed, ex.Message), Resource.DiscreteGPUControl_Title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetActionInProgress(false);
            await RefreshAfterActionAsync();
        }
    }

    private async Task RunGpuActionAsync(Func<Task> action)
    {
        SetActionInProgress(true);
        try { await action(); }
        catch (Exception ex) { ShowMessage(string.Format(Resource.DiscreteGPUControl_ActionFailed, ex.Message), Resource.DiscreteGPUControl_Title, MessageBoxButton.OK, MessageBoxImage.Error); }
        finally
        {
            SetActionInProgress(false);
            await RefreshAfterActionAsync();
        }
    }

    private async Task RefreshAfterActionAsync()
    {
        try { await _gpuController.RefreshNowAsync(); }
        catch (Exception ex) { ShowMessage(string.Format(Resource.DiscreteGPUControl_ActionFailed, ex.Message), Resource.DiscreteGPUControl_Title, MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void SetActionInProgress(bool inProgress)
    {
        _actionInProgress = inProgress;
        _killAppsMenuItem.IsEnabled = !inProgress && _lastState is GPUState.Active;
        _restartGPUMenuItem.IsEnabled = !inProgress && _lastState is GPUState.Active or GPUState.Inactive;
    }

    private MessageBoxResult ShowMessage(string message, string title, MessageBoxButton buttons, MessageBoxImage image)
    {
        var owner = Window.GetWindow(this);
        return owner is null ? MessageBox.Show(message, title, buttons, image) : MessageBox.Show(owner, message, title, buttons, image);
    }

    private async void KeepOffOnBatteryMenuItem_Click(object sender, RoutedEventArgs e)
    {
        _settings.Store.KeepGPUOffOnBattery = _keepOffOnBatteryMenuItem.IsChecked;
        _settings.SynchronizeStore();

        UpdatePowerSavingStatus(_gpuKeepOffMonitorService.Status);
        try { await _gpuKeepOffMonitorService.StartStopIfNeededAsync(); }
        catch (Exception ex) { ShowMessage(string.Format(Resource.DiscreteGPUControl_ActionFailed, ex.Message), Resource.DiscreteGPUControl_Title, MessageBoxButton.OK, MessageBoxImage.Error); }
    }
}
