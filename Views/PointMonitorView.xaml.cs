using System.Windows.Controls;

namespace Blood_Alcohol.Views
{
    public partial class PointMonitorView : UserControl
    {
        public PointMonitorView()
        {
            InitializeComponent();
        }

        private void UserControl_Unloaded(object sender, System.Windows.RoutedEventArgs e)
        {
            // DMSJ规范：视图卸载时主动释放监控资源，防止后台轮询残留。
            if (DataContext is System.IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}
