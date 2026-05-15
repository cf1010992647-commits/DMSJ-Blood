using System.Windows;
using System.Windows.Controls;
using Blood_Alcohol.ViewModels;

namespace Blood_Alcohol.Views
{
    public partial class HomeView : UserControl
    {
        private Window? _hostWindow;
        private VideoMonitoringWindow? _videoMonitoringWindow;

        /// <summary>
        /// 初始化首页视图并注册页面生命周期事件
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由首页创建时调用 用于绑定首次加载和可见性刷新逻辑
        /// </remarks>
        public HomeView()
        {
            InitializeComponent();
            Loaded += HomeView_Loaded;
            IsVisibleChanged += HomeView_IsVisibleChanged;
        }

        /// <summary>
        /// 处理首页首次加载事件并绑定宿主窗口关闭回调
        /// </summary>
        /// By:ChengLei
        /// <param name="sender">事件发送对象</param>
        /// <param name="e">路由事件参数</param>
        /// <remarks>
        /// 首次进入页面时调用 同时刷新首页条件区的参数展示
        /// </remarks>
        private void HomeView_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is HomeViewModel viewModel)
            {
                viewModel.RefreshConditionsFromConfig();
            }

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

        /// <summary>
        /// 处理首页可见性变化并在显示时刷新条件区参数
        /// </summary>
        /// By:ChengLei
        /// <param name="sender">事件发送对象</param>
        /// <param name="e">可见性变化事件参数</param>
        /// <remarks>
        /// 由 TabControl 切回首页时触发 用于重新读取最新参数配置
        /// </remarks>
        private void HomeView_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is bool isVisible &&
                isVisible &&
                DataContext is HomeViewModel viewModel)
            {
                viewModel.RefreshConditionsFromConfig();
            }
        }

        /// <summary>
        /// 处理宿主窗口关闭事件并释放首页视图模型资源
        /// </summary>
        /// By:ChengLei
        /// <param name="sender">事件发送对象</param>
        /// <param name="e">事件参数</param>
        /// <remarks>
        /// 由宿主窗口关闭时调用 防止后台监控任务和事件残留
        /// </remarks>
        private void HostWindow_Closed(object? sender, System.EventArgs e)
        {
            if (_videoMonitoringWindow != null)
            {
                _videoMonitoringWindow.Close();
                _videoMonitoringWindow = null;
            }

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

        /// <summary>
        /// 打开视频监控窗口并在已存在时激活窗口。
        /// </summary>
        /// By:ChengLei
        /// <param name="sender">事件发送对象。</param>
        /// <param name="e">路由事件参数。</param>
        /// <remarks>
        /// 由首页报警状态左侧的视频监控按钮调用，当前先打开空白监控页面占位。
        /// </remarks>
        private void OpenVideoMonitoringWindow_Click(object sender, RoutedEventArgs e)
        {
            if (_videoMonitoringWindow == null)
            {
                _videoMonitoringWindow = new VideoMonitoringWindow();
                if (_hostWindow != null)
                {
                    _videoMonitoringWindow.Owner = _hostWindow;
                }

                _videoMonitoringWindow.Closed += VideoMonitoringWindow_Closed;
                _videoMonitoringWindow.Show();
                return;
            }

            if (!_videoMonitoringWindow.IsVisible)
            {
                _videoMonitoringWindow.Show();
            }

            if (_videoMonitoringWindow.WindowState == WindowState.Minimized)
            {
                _videoMonitoringWindow.WindowState = WindowState.Normal;
            }

            _videoMonitoringWindow.Activate();
        }

        /// <summary>
        /// 处理视频监控窗口关闭事件并清理窗口引用。
        /// </summary>
        /// By:ChengLei
        /// <param name="sender">事件发送对象。</param>
        /// <param name="e">事件参数。</param>
        /// <remarks>
        /// 由视频监控窗口关闭时调用，避免首页重复持有失效窗口对象。
        /// </remarks>
        private void VideoMonitoringWindow_Closed(object? sender, System.EventArgs e)
        {
            if (_videoMonitoringWindow != null)
            {
                _videoMonitoringWindow.Closed -= VideoMonitoringWindow_Closed;
                _videoMonitoringWindow = null;
            }
        }
    }
}
