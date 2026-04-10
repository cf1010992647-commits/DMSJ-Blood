using System.Windows;
using System.Windows.Controls;
using Blood_Alcohol.ViewModels;

namespace Blood_Alcohol.Views
{
    public partial class AxisDebugView : UserControl
    {
        public AxisDebugView()
        {
            InitializeComponent();
            Unloaded += AxisDebugView_Unloaded;
        }

        private void AxisDebugView_Unloaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is AxisDebugViewModel vm)
            {
                vm.Dispose();
            }
        }
    }
}
