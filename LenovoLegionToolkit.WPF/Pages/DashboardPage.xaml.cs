using LenovoLegionToolkit.Lib.Features;
using LenovoLegionToolkit.Lib;
using System.Windows;
using System.Windows.Controls;
using LenovoLegionToolkit.Lib.Controllers;

namespace LenovoLegionToolkit.WPF.Pages
{
    public partial class DashboardPage
    {
        protected readonly GPUController _gpuController = IoCContainer.Resolve<GPUController>();
        protected readonly HDRFeature _hDRFeature = IoCContainer.Resolve<HDRFeature>();
        protected readonly HybridModeFeature _hybridModeFeature = IoCContainer.Resolve<HybridModeFeature>();
        protected readonly RefreshRateFeature _refreshRateFeature = IoCContainer.Resolve<RefreshRateFeature>();

        public DashboardPage()
        {
            InitializeComponent();

            Init();
        }

        private async void Init()
        {
            _graphicsStackPanel.Visibility = Visibility.Collapsed;
            _otherStackPanel.Visibility = Visibility.Collapsed;

            if (App.Current.IsCompatible)
                _otherStackPanel.Visibility = Visibility.Visible;

            if (_gpuController.IsSupported() ||
                await _hybridModeFeature.IsSupportedAsync() ||
                await _hDRFeature.IsSupportedAsync() ||
                await _refreshRateFeature.IsSupportedAsync())
                _graphicsStackPanel.Visibility = Visibility.Visible;

            SizeChanged += DashboardPage_SizeChanged;
        }

        private void DashboardPage_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!e.WidthChanged)
                return;

            if (e.NewSize.Width > 1000)
                Expand();
            else
                Collapse();
        }

        private void Expand()
        {
            _column1.Width = new(1, GridUnitType.Star);
            _otherInnerColumn1.Width = new(1, GridUnitType.Star);

            Grid.SetRow(_powerStackPanel, 0);
            Grid.SetColumn(_powerStackPanel, 0);

            Grid.SetRow(_graphicsStackPanel, 0);
            Grid.SetColumn(_graphicsStackPanel, 1);

            Grid.SetRow(_otherStackPanel, 1);
            Grid.SetColumn(_otherStackPanel, 0);

            Grid.SetRow(_otherInnerLeftStackPanel, 0);
            Grid.SetColumn(_otherInnerLeftStackPanel, 0);

            Grid.SetRow(_otherInnerRightStackPanel, 0);
            Grid.SetColumn(_otherInnerRightStackPanel, 1);
        }

        private void Collapse()
        {
            _column1.Width = new(0, GridUnitType.Pixel);
            _otherInnerColumn1.Width = new(0, GridUnitType.Pixel);

            Grid.SetRow(_powerStackPanel, 0);
            Grid.SetColumn(_powerStackPanel, 0);

            Grid.SetRow(_graphicsStackPanel, 1);
            Grid.SetColumn(_graphicsStackPanel, 0);

            Grid.SetRow(_otherStackPanel, 2);
            Grid.SetColumn(_otherStackPanel, 0);

            Grid.SetRow(_otherInnerLeftStackPanel, 0);
            Grid.SetColumn(_otherInnerLeftStackPanel, 0);

            Grid.SetRow(_otherInnerRightStackPanel, 1);
            Grid.SetColumn(_otherInnerRightStackPanel, 0);
        }
    }
}
