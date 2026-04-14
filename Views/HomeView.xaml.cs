using System.Windows;
using System.Windows.Controls;
using Blood_Alcohol.ViewModels;

namespace Blood_Alcohol.Views
{
    public partial class HomeView : UserControl
    {
        private Window? _hostWindow;

        public HomeView()
        {
            InitializeComponent();
            Loaded += HomeView_Loaded;
        }

        private void HomeView_Loaded(object sender, RoutedEventArgs e)
        {
            if (_hostWindow != null)
            {
                return;
            }

            _hostWindow = Window.GetWindow(this);
            if (_hostWindow != null)
            {
                _hostWindow.Closed += HostWindow_Closed;
            }
        }

        private void HostWindow_Closed(object? sender, System.EventArgs e)
        {
            if (_hostWindow != null)
            {
                _hostWindow.Closed -= HostWindow_Closed;
                _hostWindow = null;
            }

            if (DataContext is HomeViewModel vm)
            {
                vm.Dispose();
            }
        }
    }
}
