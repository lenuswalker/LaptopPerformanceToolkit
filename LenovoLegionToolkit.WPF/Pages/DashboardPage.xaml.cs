using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Features;
using LenovoLegionToolkit.Lib;
using System.Windows;
using System.Windows.Controls;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.WPF.Controls.Dashboard;
using LenovoLegionToolkit.WPF.Resources;
using LenovoLegionToolkit.WPF.Settings;
using LenovoLegionToolkit.WPF.Windows.Dashboard;
using Wpf.Ui.Common;
using Wpf.Ui.Controls;
using LenovoLegionToolkit.Lib.Controllers;

namespace LenovoLegionToolkit.WPF.Pages;

public partial class DashboardPage
{
    protected readonly GPUController _gpuController = IoCContainer.Resolve<GPUController>();
    protected readonly HDRFeature _hDRFeature = IoCContainer.Resolve<HDRFeature>();
    protected readonly HybridModeFeature _hybridModeFeature = IoCContainer.Resolve<HybridModeFeature>();
    protected readonly RefreshRateFeature _refreshRateFeature = IoCContainer.Resolve<RefreshRateFeature>();

    //public DashboardPage()
    //{
    //    InitializeComponent();
    private readonly DashboardSettings _dashboardSettings = IoCContainer.Resolve<DashboardSettings>();

    private readonly List<DashboardGroupControl> _dashboardGroupControls = new();

    private bool _refreshOnce;

    public DashboardPage() => InitializeComponent();

    private async void DashboardPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (_refreshOnce)
            return;

        _refreshOnce = true;

        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        _loader.IsLoading = true;

        var initializedTasks = new List<Task> { Task.Delay(TimeSpan.FromMilliseconds(500)) };

        ScrollHost.ScrollToTop();

        _dashboardGroupControls.Clear();
        _content.ColumnDefinitions.Clear();
        _content.RowDefinitions.Clear();
        _content.Children.Clear();

        var groups = _dashboardSettings.Store.Groups;

        _content.ColumnDefinitions.Add(new ColumnDefinition { Width = new(1, GridUnitType.Star) });
        _content.ColumnDefinitions.Add(new ColumnDefinition { Width = new(1, GridUnitType.Star) });

        foreach (var group in groups)
        {
            _content.RowDefinitions.Add(new RowDefinition { Height = new(1, GridUnitType.Auto) });

            var control = new DashboardGroupControl(group);
            _content.Children.Add(control);
            _dashboardGroupControls.Add(control);
            initializedTasks.Add(control.InitializedTask);
        }

        _content.RowDefinitions.Add(new RowDefinition { Height = new(1, GridUnitType.Auto) });

        var editDashboardHyperlink = new Hyperlink
        {
            Icon = SymbolRegular.Edit24,
            Content = Resource.DashboardPage_Customize,
            Margin = new(0, 16, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        editDashboardHyperlink.Click += (_, _) =>
        {
            var window = new EditDashboardWindow { Owner = Window.GetWindow(this) };
            window.Apply += async (_, _) => await RefreshAsync();
            window.ShowDialog();
        };

        Grid.SetRow(editDashboardHyperlink, groups.Length);
        Grid.SetColumn(editDashboardHyperlink, 0);
        Grid.SetColumnSpan(editDashboardHyperlink, 2);

        _content.Children.Add(editDashboardHyperlink);

        LayoutGroups(ActualWidth);

        await Task.WhenAll(initializedTasks);

    //    Init();
    //}

    //private async void Init()
    //{
    //    _graphicsStackPanel.Visibility = Visibility.Collapsed;
    //    _otherStackPanel.Visibility = Visibility.Collapsed;

    //    if (App.Current.IsCompatible)
    //        _otherStackPanel.Visibility = Visibility.Visible;

    //    if (_gpuController.IsSupported() ||
    //        await _hybridModeFeature.IsSupportedAsync() ||
    //        await _hDRFeature.IsSupportedAsync() ||
    //        await _refreshRateFeature.IsSupportedAsync())
    //        _graphicsStackPanel.Visibility = Visibility.Visible;

    //    SizeChanged += DashboardPage_SizeChanged;
        _loader.IsLoading = false;
    }

    private void DashboardPage_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!e.WidthChanged)
            return;

        LayoutGroups(e.NewSize.Width);
    }

    private void LayoutGroups(double width)
    {
        if (width > 1000)
            Expand();
        else
            Collapse();
    }

    private void Expand()
    {
        var lastColumn = _content.ColumnDefinitions.LastOrDefault();
        if (lastColumn is not null)
            lastColumn.Width = new(1, GridUnitType.Star);

        for (var index = 0; index < _dashboardGroupControls.Count; index++)
        {
            var control = _dashboardGroupControls[index];
            Grid.SetRow(control, index - (index % 2));
            Grid.SetColumn(control, index % 2);
        }
    }

    private void Collapse()
    {
        var lastColumn = _content.ColumnDefinitions.LastOrDefault();
        if (lastColumn is not null)
            lastColumn.Width = new(0, GridUnitType.Pixel);

        for (var index = 0; index < _dashboardGroupControls.Count; index++)
        {
            var control = _dashboardGroupControls[index];
            Grid.SetRow(control, index);
            Grid.SetColumn(control, 0);
        }
    }
}
