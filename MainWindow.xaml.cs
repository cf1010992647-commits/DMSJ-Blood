using System.Windows;
using Blood_Alcohol.Services;

namespace Blood_Alcohol
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
            Closed += MainWindow_Closed;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            OperationModeService.ModeChanged += OnOperationModeChanged;
            ApplyOperationModeUi(OperationModeService.CurrentMode);
        }

        private void MainWindow_Closed(object? sender, System.EventArgs e)
        {
            OperationModeService.ModeChanged -= OnOperationModeChanged;
        }

        private void OnOperationModeChanged(OperationMode mode)
        {
            if (Dispatcher.CheckAccess())
            {
                ApplyOperationModeUi(mode);
                return;
            }

            _ = Dispatcher.BeginInvoke(() => ApplyOperationModeUi(mode));
        }

        private void ApplyOperationModeUi(OperationMode mode)
        {
            bool isManual = mode == OperationMode.Manual;

            SettingsTab.IsEnabled = isManual;
            SettingsTab.IsHitTestVisible = isManual;

            if (!isManual && ReferenceEquals(RootTabControl.SelectedItem, SettingsTab))
            {
                RootTabControl.SelectedItem = HomeTab;
            }
        }
    }
}
