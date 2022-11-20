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
