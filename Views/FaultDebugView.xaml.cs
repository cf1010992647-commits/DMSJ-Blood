using System.Windows.Controls;

namespace Blood_Alcohol.Views
{
    public partial class FaultDebugView : UserControl
    {
        public FaultDebugView()
        {
            InitializeComponent();
        }

        private void UserControl_Unloaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is System.IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}
