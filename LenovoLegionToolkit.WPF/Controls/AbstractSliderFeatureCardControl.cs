using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Features;
using LenovoLegionToolkit.Lib.Messaging;
using LenovoLegionToolkit.Lib.Messaging.Messages;
using LenovoLegionToolkit.Lib.Utils;
using LenovoLegionToolkit.WPF.Extensions;
using Wpf.Ui.Common;
using Wpf.Ui.Controls;

namespace LenovoLegionToolkit.WPF.Controls;

public abstract class AbstractSliderFeatureCardControl<T> : AbstractRefreshingControl where T : struct
{
    private readonly IFeature<T> _feature = IoCContainer.Resolve<IFeature<T>>();

    // Coalesces the burst of ValueChanged events fired while dragging into one state write.
    private readonly ThrottleLastDispatcher _throttleDispatcher = new(TimeSpan.FromMilliseconds(250), nameof(AbstractSliderFeatureCardControl<T>));

    private readonly CardControl _cardControl = new();

    private readonly CardHeaderControl _cardHeaderControl = new();

    private readonly Slider _slider = new();

    protected SymbolRegular Icon
    {
        get => _cardControl.Icon;
        set => _cardControl.Icon = value;
    }

    protected string Title
    {
        get => _cardHeaderControl.Title;
        set => _cardHeaderControl.Title = value;
    }

    protected string Subtitle
    {
        get => _cardHeaderControl.Subtitle;
        set => _cardHeaderControl.Subtitle = value;
    }

    protected virtual int Maximum => 100;

    protected AbstractSliderFeatureCardControl() => InitializeComponent();

    private void InitializeComponent()
    {
        _slider.ValueChanged += Slider_ValueChanged;
        _slider.Visibility = Visibility.Hidden;
        _slider.Maximum = Maximum;
        _slider.IsSnapToTickEnabled = true;
        _slider.TickFrequency = 1;
        _slider.SmallChange = 1;
        _slider.Margin = new(8, 0, 0, 0);

        _cardHeaderControl.Accessory = _slider;
        _cardControl.Header = _cardHeaderControl;
        _cardControl.Margin = new(0, 0, 0, 8);

        Content = _cardControl;
    }

    private async void Slider_ValueChanged(object sender, RoutedEventArgs e)
    {
        if (IsRefreshing)
            return;

        var value = (int)_slider.Value;
        await _throttleDispatcher.DispatchAsync(() => OnStateChangeAsync(value));
    }

    protected override async Task OnRefreshAsync()
    {
        if (!await _feature.IsSupportedAsync())
            throw new NotSupportedException();

        _slider.Value = Convert.ToInt32(await _feature.GetStateAsync());
    }

    protected override void OnFinishedLoading()
    {
        _slider.Visibility = Visibility.Visible;

        MessagingCenter.Subscribe<FeatureStateMessage<T>>(this, () => Dispatcher.InvokeTask(async () =>
        {
            if (!IsVisible)
                return;

            await RefreshAsync();
        }));
    }

    private async Task OnStateChangeAsync(int value)
    {
        try
        {
            await _feature.SetStateAsync((T)Convert.ChangeType(value, typeof(T)));
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Failed to set state. [feature={_feature.GetType().Name}, value={value}]", ex);
        }
    }
}
