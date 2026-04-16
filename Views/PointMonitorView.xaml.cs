using System;
using System.Windows;
using System.Windows.Controls;
using Blood_Alcohol.ViewModels;

namespace Blood_Alcohol.Views
{
    public partial class PointMonitorView : UserControl
    {
        private Window? _hostWindow;

        /// <summary>
        /// 初始化点位监控视图并注册页面生命周期事件。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由 WPF 创建视图时调用，后续通过 Loaded 和 Unloaded 控制监控启停。
        /// </remarks>
        public PointMonitorView()
        {
            InitializeComponent();
            Loaded += PointMonitorView_Loaded;
        }

        /// <summary>
        /// 处理页面加载并激活监控生命周期。
        /// </summary>
        /// By:ChengLei
        /// <param name="sender">事件发送对象。</param>
        /// <param name="e">路由事件参数。</param>
        /// <remarks>
        /// 由 WPF Loaded 触发，用于页面可见时启动监控并绑定宿主窗口关闭事件。
        /// </remarks>
        private void PointMonitorView_Loaded(object sender, RoutedEventArgs e)
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
        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is IMonitoringLifecycle lifecycle)
            {
                lifecycle.DeactivateMonitoring();
            }
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
