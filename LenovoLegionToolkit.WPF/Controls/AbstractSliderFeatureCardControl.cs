using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Features;
using LenovoLegionToolkit.WPF.Extensions;
using Wpf.Ui.Common;
using Wpf.Ui.Controls;

namespace LenovoLegionToolkit.WPF.Controls;

public abstract class AbstractSliderFeatureCardControl<T> : AbstractRefreshingControl where T : struct
{
    private readonly IFeature<T> _feature = IoCContainer.Resolve<IFeature<T>>();

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

    protected abstract T Value { get; }

    protected virtual int Maximum => 100;

    protected AbstractSliderFeatureCardControl()
    {
        InitializeComponent();
    }
    
    private void InitializeComponent()
    {
        _slider.ValueChanged += Slider_ValueChanged;
        _slider.Visibility = Visibility.Hidden;
        _slider.Maximum = Maximum;
        _slider.Margin = new(8, 0, 0, 0);

        _cardHeaderControl.Accessory = _slider;
        _cardControl.Header = _cardHeaderControl;
        _cardControl.Margin = new(0, 0, 0, 8);

        Content = _cardControl;
    }

    private async void Slider_ValueChanged(object sender, RoutedEventArgs e) => await OnStateChange(_slider, _feature);

    protected override async Task OnRefreshAsync()
    {
        if (!await _feature.IsSupportedAsync())
            throw new NotSupportedException();

        var value = await _feature.GetStateAsync();

        if (int.TryParse(value.ToString(), out int intValue))
            _slider.Value = intValue;
        else
            throw new NotSupportedException();
    }
    
    protected override void OnFinishedLoading()
    {
        _slider.Visibility = Visibility.Visible;

        MessagingCenter.Subscribe<T>(this, () => Dispatcher.InvokeTask(RefreshAsync));
    }

    protected virtual async Task OnStateChange(Slider slider, IFeature<T> feature)
    {
        if (IsRefreshing)
            return;

        var state = (int)slider.Value;
        if (state.Equals(await feature.GetStateAsync()))
            return;

        await feature.SetStateAsync((T)Convert.ChangeType(state, typeof(T)));
    }
}
