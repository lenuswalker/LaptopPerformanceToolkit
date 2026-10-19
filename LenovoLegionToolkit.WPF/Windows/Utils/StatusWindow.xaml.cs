using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Controllers;
using LenovoLegionToolkit.Lib.Controllers.GodMode;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.Features;
using LenovoLegionToolkit.Lib.System;
using LenovoLegionToolkit.Lib.Utils;
using LenovoLegionToolkit.WPF.Extensions;
using LenovoLegionToolkit.WPF.Resources;
using Windows.Win32.System.Power;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace LenovoLegionToolkit.WPF.Windows.Utils;

public partial class StatusWindow
{
    private static readonly ProcessorController _controller = IoCContainer.Resolve<ProcessorController>();

    private readonly struct StatusWindowData(
        PowerModeState? powerModeState,
        string? godModePresetName,
        BatteryInformation? batteryInformation,
        BatteryState? batteryState,
        bool hasUpdate,
        ProcessorTDPState? processorTDPState)
    {
        public PowerModeState? PowerModeState { get; } = powerModeState;
        public string? GodModePresetName { get; } = godModePresetName;
        public BatteryInformation? BatteryInformation { get; } = batteryInformation;
        public BatteryState? BatteryState { get; } = batteryState;
        public bool HasUpdate { get; } = hasUpdate;
        public ProcessorTDPState? ProcessorTDPState { get; } = processorTDPState;
    }

    private static StatusWindowData _lastData;
    private static Task<StatusWindowData>? _pendingData;
    private static Task<GPUStatus?>? _pendingGpuData;

    public static Task<StatusWindow> CreateAsync() => Task.FromResult(new StatusWindow(_lastData));

    private static async Task<StatusWindowData> GetStatusWindowDataAsync()
    {
        var powerModeFeature = IoCContainer.Resolve<PowerModeFeature>();
        var godModeController = IoCContainer.Resolve<GodModeController>();
        var batteryFeature = IoCContainer.Resolve<BatteryFeature>();
        var updateChecker = IoCContainer.Resolve<UpdateChecker>();

        PowerModeState? state = null;
        string? godModePresetName = null;
        BatteryInformation? batteryInformation = null;
        BatteryState? batteryState = null;
        var hasUpdate = false;
        ProcessorTDPState? processorTDPState = null;

        try
        {
            if (await powerModeFeature.IsSupportedAsync())
            {
                state = await powerModeFeature.GetStateAsync();

                if (state == PowerModeState.GodMode)
                    godModePresetName = await godModeController.GetActivePresetNameAsync();
            }
        }
        catch { /* Ignored */ }

        try
        {
            batteryInformation = Battery.GetBatteryInformation();
        }
        catch { /* Ignored */ }

        try
        {
            if (await batteryFeature.IsSupportedAsync())
                batteryState = await batteryFeature.GetStateAsync();

        }
        catch { /* Ignored */ }

        try
        {
            hasUpdate = await updateChecker.CheckAsync(false) is not null;
        }
        catch { /* Ignored */ }

        try
        {
            if (await _controller.IsSupportedAsync())
                processorTDPState = await _controller.GetProcessorTDPAsync();
        }
        catch { /* Ignored */ }

        return new(state, godModePresetName, batteryInformation, batteryState, hasUpdate, processorTDPState);
    }

    private static async Task<GPUStatus?> GetGPUStatusAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try { return await IoCContainer.Resolve<GPUController>().RefreshNowAsync(timeout.Token); }
        catch { return null; }
    }

    private StatusWindow(StatusWindowData data)
    {
        InitializeComponent();

        Loaded += StatusWindow_Loaded;

        WindowStartupLocation = WindowStartupLocation.Manual;
        WindowBackdropType = Wpf.Ui.Controls.WindowBackdropType.None;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;

        Focusable = false;
        Topmost = true;
        ExtendsContentIntoTitleBar = true;
        WindowStyle = WindowStyle.None;
        ShowInTaskbar = false;
        ShowActivated = false;

#if DEBUG
        _title.Text += " [DEBUG]";
#else
        var version = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version;
        if (version == new Version(0, 0, 1, 0) || version?.Build == 99)
            _title.Text += " [BETA]";
#endif

        if (Log.Instance.IsTraceEnabled)
            _title.Text += " [LOGGING ENABLED]";

        RefreshData(data);
    }

    protected override void OnExtendsContentIntoTitleBarChanged(bool oldValue, bool newValue)
    {
        base.OnExtendsContentIntoTitleBarChanged(oldValue, newValue);
        WindowStyle = WindowStyle.None;
    }

    private void RefreshData(StatusWindowData data)
    {
        RefreshPowerMode(data.PowerModeState, data.GodModePresetName);
        RefreshDiscreteGpu(null);
        RefreshBattery(data.BatteryInformation, data.BatteryState);
        RefreshUpdate(data.HasUpdate);
        RefreshProcessorTDP(data.ProcessorTDPState);
    }

    private async void StatusWindow_Loaded(object sender, RoutedEventArgs e)
    {
        MoveBottomRightEdgeOfWindowToMousePosition();
        var pendingGpuData = _pendingGpuData ??= Task.Run(GetGPUStatusAsync);
        _ = ApplyGPUStatusWhenReadyAsync(pendingGpuData);
        var pendingData = _pendingData ??= Task.Run(GetStatusWindowDataAsync);
        try
        {
            _lastData = await pendingData;
            if (!IsVisible)
                return;

            RefreshData(_lastData);
            UpdateLayout();
            MoveBottomRightEdgeOfWindowToMousePosition();
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Failed to refresh tooltip.", ex);
        }
        finally
        {
            if (ReferenceEquals(_pendingData, pendingData))
                _pendingData = null;
        }
    }

    private async Task ApplyGPUStatusWhenReadyAsync(Task<GPUStatus?> pendingGpuData)
    {
        try
        {
            var gpuStatus = await pendingGpuData.WaitAsync(TimeSpan.FromSeconds(10));
            if (!IsVisible)
                return;

            RefreshDiscreteGpu(gpuStatus);
            UpdateLayout();
            MoveBottomRightEdgeOfWindowToMousePosition();
        }
        catch (TimeoutException)
        {
            if (IsVisible)
                RefreshDiscreteGpu(null);
        }
        catch (Exception ex)
        {
            if (IsVisible)
                RefreshDiscreteGpu(null);
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Failed to refresh GPU preview.", ex);
        }
        finally
        {
            if (pendingGpuData.IsCompleted && ReferenceEquals(_pendingGpuData, pendingGpuData))
                _pendingGpuData = null;
        }
    }

    private void MoveBottomRightEdgeOfWindowToMousePosition()
    {
        var transform = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice;
        if (!transform.HasValue)
        {
            Left = 0;
            Top = 0;
            return;
        }

        const double offset = 8;

        var mousePoint = Control.MousePosition;
        var screenRectangle = Screen.FromPoint(mousePoint).WorkingArea;

        var mouse = transform.Value.Transform(new Point(mousePoint.X, mousePoint.Y));
        var screen = transform.Value.Transform(new Point(screenRectangle.Right, screenRectangle.Bottom));

        if (mouse.X + offset + ActualWidth > screen.X)
            Left = mouse.X - ActualWidth - offset;
        else
            Left = mouse.X + offset;

        if (mouse.Y + offset + ActualHeight > screen.Y)
            Top = mouse.Y - ActualHeight - offset;
        else
            Top = mouse.Y + offset;
    }

    private void RefreshPowerMode(PowerModeState? powerModeState, string? godModePresetName)
    {
        _powerModeValueLabel.Content = powerModeState?.GetDisplayName() ?? "-";
        _powerModeValueIndicator.Fill = powerModeState?.GetSolidColorBrush() ?? new(Colors.Transparent);

        if (powerModeState == PowerModeState.GodMode)
        {
            _powerModePresetValueLabel.Content = godModePresetName ?? "-";

            _powerModePresetLabel.Visibility = Visibility.Visible;
            _powerModePresetValueLabel.Visibility = Visibility.Visible;
        }
        else
        {
            _powerModePresetLabel.Visibility = Visibility.Collapsed;
            _powerModePresetValueLabel.Visibility = Visibility.Collapsed;
        }
    }

    private void RefreshDiscreteGpu(GPUStatus? status)
    {
        var current = status ?? new GPUStatus(GPUState.Unknown, null, []);
        _gpuStatusLabel.Content = current.State switch
        {
            GPUState.Active => Resource.Active,
            GPUState.Inactive => Resource.DiscreteGPUControl_Status_IdlePoweredOn,
            GPUState.MonitorConnected => Resource.DiscreteGPUControl_Status_DisplayConnectedSimple,
            GPUState.PoweredOff => Resource.DiscreteGPUControl_Status_Asleep,
            GPUState.NvidiaGpuNotFound => Resource.DiscreteGPUControl_Status_NotDetected,
            _ => Resource.DiscreteGPUControl_Status_Unknown
        };
        _gpuStatusDot.Fill = new SolidColorBrush(current.State switch
        {
            GPUState.Active => Color.FromRgb(0xE5, 0x39, 0x35),
            GPUState.Inactive or GPUState.MonitorConnected => Color.FromRgb(0xF2, 0xA5, 0x41),
            GPUState.PoweredOff => Color.FromRgb(0x4C, 0xAF, 0x50),
            _ => Color.FromRgb(0x80, 0x80, 0x80)
        });
        var hasPerformanceState = current.State is GPUState.Active or GPUState.MonitorConnected;
        _gpuPowerStateValueLabel.Content = hasPerformanceState ? current.PerformanceState ?? "-" : null;
        _gpuPowerStateValue.Visibility = hasPerformanceState ? Visibility.Visible : Visibility.Collapsed;
        _gpuPowerStateValueLabel.Visibility = hasPerformanceState ? Visibility.Visible : Visibility.Collapsed;
        _gpuGrid.Visibility = Visibility.Visible;
    }

    private void RefreshBattery(BatteryInformation? batteryInformation, BatteryState? batteryState)
    {
        if (!batteryState.HasValue)
            batteryState = BatteryState.Normal;

        if (!batteryInformation.HasValue || !batteryState.HasValue)
        {
            _batteryIcon.Symbol = SymbolRegular.Battery024;
            _batteryValueLabel.Content = "-";
            _batteryModeValueLabel.Content = "-";
            _batteryDischargeValueLabel.Content = "-";
            _batteryMinDischargeValueLabel.Content = "-";
            _batteryMaxDischargeValueLabel.Content = "-";
            return;
        }

        var symbol = (int)Math.Round(batteryInformation.Value.BatteryPercentage / 10.0) switch
        {
            10 => SymbolRegular.Battery1024,
            9 => SymbolRegular.Battery924,
            8 => SymbolRegular.Battery824,
            7 => SymbolRegular.Battery724,
            6 => SymbolRegular.Battery624,
            5 => SymbolRegular.Battery524,
            4 => SymbolRegular.Battery424,
            3 => SymbolRegular.Battery324,
            2 => SymbolRegular.Battery224,
            1 => SymbolRegular.Battery124,
            _ => SymbolRegular.Battery024,
        };

        if (batteryInformation.Value.IsCharging)
            symbol = batteryState == BatteryState.Conservation ? SymbolRegular.BatterySaver24 : SymbolRegular.BatteryCharge24;

        if (batteryInformation.Value.IsLowBattery)
            _batteryValueLabel.SetResourceReference(ForegroundProperty, "SystemFillColorCautionBrush");
        else
            _batteryValueLabel.ClearValue(ForegroundProperty);

        _batteryIcon.Symbol = symbol;
        _batteryValueLabel.Content = $"{batteryInformation.Value.BatteryPercentage}%";
        _batteryModeValueLabel.Content = batteryState.GetDisplayName();
        _batteryDischargeValueLabel.Content = $"{batteryInformation.Value.DischargeRate / 1000.0:+0.00;-0.00;0.00} W";
        _batteryMinDischargeValueLabel.Content = $"{batteryInformation.Value.MinDischargeRate / 1000.0:+0.00;-0.00;0.00} W";
        _batteryMaxDischargeValueLabel.Content = $"{batteryInformation.Value.MaxDischargeRate / 1000.0:+0.00;-0.00;0.00} W";
    }

    private void RefreshProcessorTDP(ProcessorTDPState? processorTDPState)
    {
        if (!processorTDPState.HasValue)
        {
            _processorTDPFastLabel.Content = "-";
            _processorTDPSlowLabel.Content = "-";
            _processorTDPFastLabel.Visibility = Visibility.Collapsed;
            _processorTDPSlowLabel.Visibility = Visibility.Collapsed;
            _processorTDPFastValueLabel.Content = "-";
            _processorTDPSlowValueLabel.Content = "-";
            _processorTDPFastValueLabel.Visibility = Visibility.Collapsed;
            _processorTDPSlowValueLabel.Visibility = Visibility.Collapsed;
            _processorTDPGrid.Visibility = Visibility.Collapsed;
            return;
        }

        _processorTDPFastLabel.Content = _controller.FastLimitDisplayName;
        _processorTDPSlowLabel.Content = _controller.SlowLimitDisplayName;
        _processorTDPFastLabel.Visibility = Visibility.Visible;
        _processorTDPSlowLabel.Visibility = Visibility.Visible;

        _processorTDPFastValueLabel.Content = $"{processorTDPState.Value.Fast} W";
        _processorTDPSlowValueLabel.Content = $"{processorTDPState.Value.Slow} W";
        _processorTDPFastValueLabel.Visibility = Visibility.Visible;
        _processorTDPSlowValueLabel.Visibility = Visibility.Visible;
        _processorTDPGrid.Visibility = Visibility.Visible;
    }

    private void RefreshUpdate(bool hasUpdate) => _updateIndicator.Visibility = hasUpdate ? Visibility.Visible : Visibility.Collapsed;
}
