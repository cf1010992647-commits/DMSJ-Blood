using System.Windows;
using System.Windows.Controls;
using Blood_Alcohol.ViewModels;

namespace Blood_Alcohol.Views
{
    public partial class DebugView : UserControl
    {
        public DebugView()
        {
            InitializeComponent();
            Unloaded += DebugView_Unloaded;
        }

        private void DebugView_Unloaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is DebugViewModel vm)
            {
                vm.Dispose();
            }
        }

        private void DebugTabControl_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is not TabControl tabControl || DataContext is not DebugViewModel vm)
            {
                return;
            }

            int currentIndex = tabControl.SelectedIndex;
            int normalized = vm.NormalizeTabIndex(currentIndex);
            if (normalized != currentIndex)
            {
                tabControl.SelectedIndex = normalized;
            }
        }
    }
}
