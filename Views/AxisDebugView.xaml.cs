using System;
using System.Windows;
using System.Windows.Controls;
using Blood_Alcohol.ViewModels;

namespace Blood_Alcohol.Views
{
    public partial class AxisDebugView : UserControl
    {
        private Window? _hostWindow;

        /// <summary>
        /// 初始化轴调试视图并注册页面生命周期事件。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由 WPF 创建视图时调用，后续通过 Loaded 和 Unloaded 控制监控启停。
        /// </remarks>
        public AxisDebugView()
        {
            InitializeComponent();
            Loaded += AxisDebugView_Loaded;
            Unloaded += AxisDebugView_Unloaded;
        }

        /// <summary>
        /// 处理页面加载并激活监控生命周期。
        /// </summary>
        /// By:ChengLei
        /// <param name="sender">事件发送对象。</param>
        /// <param name="e">路由事件参数。</param>
        /// <remarks>
        /// 由 WPF Loaded 触发，用于页面可见时启动轴状态监控并绑定窗口关闭事件。
        /// </remarks>
        private void AxisDebugView_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is IMonitoringLifecycle lifecycle)
            {
                lifecycle.ActivateMonitoring();
            }

            BindHostWindowClosed();
        }

        /// <summary>
        /// 处理页面卸载并停用监控生命周期。
        /// </summary>
        /// By:ChengLei
        /// <param name="sender">事件发送对象。</param>
        /// <param name="e">路由事件参数。</param>
        /// <remarks>
        /// 由 WPF Unloaded 触发，仅停止监控，不释放视图模型。
        /// </remarks>
        private void AxisDebugView_Unloaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is IMonitoringLifecycle lifecycle)
            {
                lifecycle.DeactivateMonitoring();
            }
        }

        /// <summary>
        /// 打开轴点位配置窗口并在保存后刷新轴调试页。
        /// </summary>
        /// By:ChengLei
        /// <param name="sender">事件发送对象。</param>
        /// <param name="e">路由事件参数。</param>
        /// <remarks>
        /// 由轴调试页顶部“轴点位配置”按钮触发。
        /// </remarks>
        private void OpenAxisAddressConfig_Click(object sender, RoutedEventArgs e)
        {
            AxisAddressConfigWindow window = new()
            {
                Owner = Window.GetWindow(this)
            };

            window.ShowDialog();
            if (window.HasSaved)
            {
                ReloadAxisDebugViewModel();
            }
        }

        /// <summary>
        /// 重新创建轴调试视图模型以加载最新点位配置。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由轴点位配置窗口保存后调用，避免修改配置后必须重启软件。
        /// </remarks>
        private void ReloadAxisDebugViewModel()
        {
            if (DataContext is IDisposable disposable)
            {
                disposable.Dispose();
            }

            AxisDebugViewModel viewModel = new();
            DataContext = viewModel;
            viewModel.ActivateMonitoring();
        }

        /// <summary>
        /// 绑定宿主窗口关闭事件。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由 Loaded 调用，确保 Dispose 只在宿主窗口真正关闭时执行。
        /// </remarks>
        private void BindHostWindowClosed()
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

        /// <summary>
        /// 处理宿主窗口关闭并释放视图模型资源。
        /// </summary>
        /// By:ChengLei
        /// <param name="sender">事件发送对象。</param>
        /// <param name="e">事件参数。</param>
        /// <remarks>
        /// 由宿主窗口 Closed 触发，执行最终资源释放。
        /// </remarks>
        private void HostWindow_Closed(object? sender, EventArgs e)
        {
            if (_hostWindow != null)
            {
                _hostWindow.Closed -= HostWindow_Closed;
                _hostWindow = null;
            }

            if (DataContext is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}
