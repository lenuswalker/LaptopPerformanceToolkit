using LenovoLegionToolkit.Lib.Features;
using LenovoLegionToolkit.Lib;
using System.Windows;
using System.Windows.Controls;
using LenovoLegionToolkit.Lib.Controllers;

namespace LenovoLegionToolkit.WPF.Pages
{
    public partial class DashboardPage
    {
        private const int WIDTH_BREAKPOINT = 1000;

        private readonly StackPanel[] _collapsedPanels;

        private readonly StackPanel[][] _expandedPanels;

        protected readonly DisplayBrightnessFeature _displayBrightnessFeature = IoCContainer.Resolve<DisplayBrightnessFeature>();
        protected readonly GPUController _gpuController = IoCContainer.Resolve<GPUController>();
        protected readonly HDRFeature _hDRFeature = IoCContainer.Resolve<HDRFeature>();
        protected readonly HybridModeFeature _hybridModeFeature = IoCContainer.Resolve<HybridModeFeature>();
        protected readonly OverDriveFeature _overDriveFeature = IoCContainer.Resolve<OverDriveFeature>();
        protected readonly RefreshRateFeature _refreshRateFeature = IoCContainer.Resolve<RefreshRateFeature>();

        public DashboardPage()
        {
            InitializeComponent();

            _collapsedPanels = new[]
            {
                _powerStackPanel,
                _graphicsStackPanel,
                _displayStackPanel,
                _otherStackPanel
            };

            _expandedPanels = new[]
            {
                new[]
                {
                    _powerStackPanel,
                    _graphicsStackPanel
                },
                new []
                {
                    _displayStackPanel,
                    _otherStackPanel
                }
            };

            Init();
        }

        private async void Init()
        {
            _graphicsStackPanel.Visibility = Visibility.Collapsed;
            _displayStackPanel.Visibility = Visibility.Collapsed;
            _otherStackPanel.Visibility = Visibility.Collapsed;

            if (App.Current.IsCompatible)
                _otherStackPanel.Visibility = Visibility.Visible;

            if (_gpuController.IsSupported() || 
                await _hybridModeFeature.IsSupportedAsync())
                _graphicsStackPanel.Visibility = Visibility.Visible;

            if (await _displayBrightnessFeature.IsSupportedAsync() || 
                await _refreshRateFeature.IsSupportedAsync() ||
                await _hDRFeature.IsSupportedAsync() || 
                await _overDriveFeature.IsSupportedAsync())
                _displayStackPanel.Visibility = Visibility.Visible;
        }

        private void DashboardPage_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!e.WidthChanged)
                return;

            if (e.NewSize.Width > WIDTH_BREAKPOINT)
                Expand();
            else
                Collapse();
        }

        private void Expand()
        {
            for (var row = 0; row < _expandedPanels.Length; row++)
                for (var column = 0; column < _expandedPanels[row].Length; column++)
                {
                    var panel = _expandedPanels[row][column];
                    Grid.SetRow(panel, row);
                    Grid.SetColumn(panel, column);
                }

            _column1.Width = new(1, GridUnitType.Star);
        }

        private void Collapse()
        {
            for (var row = 0; row < _collapsedPanels.Length; row++)
            {
                var panel = _collapsedPanels[row];
                Grid.SetRow(panel, row);
                Grid.SetColumn(panel, 0);
            }

            _column1.Width = new(0, GridUnitType.Pixel);
        }
    }
}
