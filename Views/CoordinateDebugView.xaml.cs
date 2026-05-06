using System;
using System.Windows;
using System.Windows.Controls;
using Blood_Alcohol.ViewModels;

namespace Blood_Alcohol.Views
{
    public partial class CoordinateDebugView : UserControl
    {
        private Window? _hostWindow;

        /// <summary>
        /// 初始化坐标调试视图并注册页面生命周期事件。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由 WPF 创建视图时调用，后续通过 Loaded 和 Unloaded 控制实时坐标监控启停。
        /// </remarks>
        public CoordinateDebugView()
        {
            InitializeComponent();
            Loaded += CoordinateDebugView_Loaded;
            Unloaded += CoordinateDebugView_Unloaded;
        }

        /// <summary>
        /// 处理页面加载并激活实时坐标监控。
        /// </summary>
        /// By:ChengLei
        /// <param name="sender">事件发送对象。</param>
        /// <param name="e">路由事件参数。</param>
        /// <remarks>
        /// 由 WPF Loaded 触发，用于页面可见时启动XYZ坐标刷新。
        /// </remarks>
        private void CoordinateDebugView_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is IMonitoringLifecycle lifecycle)
            {
                lifecycle.ActivateMonitoring();
            }

            BindHostWindowClosed();
        }

        /// <summary>
        /// 处理页面卸载并停用实时坐标监控。
        /// </summary>
        /// By:ChengLei
        /// <param name="sender">事件发送对象。</param>
        /// <param name="e">路由事件参数。</param>
        /// <remarks>
        /// 由 WPF Unloaded 触发，仅停止监控，不释放视图模型。
        /// </remarks>
        private void CoordinateDebugView_Unloaded(object sender, RoutedEventArgs e)
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
        /// 由 Loaded 调用，确保窗口关闭时释放实时坐标轮询资源。
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
