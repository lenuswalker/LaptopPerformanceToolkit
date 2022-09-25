using System.Threading.Tasks;
using System.Windows;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Controllers;
using LenovoLegionToolkit.Lib.Settings;
using LenovoLegionToolkit.WPF.Controls.Settings;

namespace LenovoLegionToolkit.WPF.Windows.Settings
{
    public partial class ProcessorTDPModesWindow
    {
        private readonly ProcessorController _controller = IoCContainer.Resolve<ProcessorController>();
        private readonly ProcessorSettings _settings = IoCContainer.Resolve<ProcessorSettings>();

        public ProcessorTDPModesWindow()
        {
            InitializeComponent();

            ResizeMode = ResizeMode.CanMinimize;

            _titleBar.UseSnapLayout = false;
            _titleBar.CanMaximize = false;

            Loaded += ProcessorTDPModesWindow_Loaded;
            IsVisibleChanged += ProcessorTDPModesWindow_IsVisibleChanged;
        }

        private async void ProcessorTDPModesWindow_Loaded(object sender, RoutedEventArgs e) => await RefreshAsync();

        private async void ProcessorTDPModesWindow_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsLoaded && IsVisible)
                await RefreshAsync();
        }

        private async Task RefreshAsync()
        {
            _loader.IsLoading = true;

            var loadingTask = Task.Delay(500);

            var settings = await _controller.GetSettingsAsync();

            _stackPanel.Children.Clear();
            foreach (var setting in settings)
                _stackPanel.Children.Add(new ProcessorTDPModeControl(setting));

            await loadingTask;

            _loader.IsLoading = false;
        }
    }
}
