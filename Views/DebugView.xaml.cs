using System;
using System.Windows;
using System.Windows.Controls;
using Blood_Alcohol.ViewModels;

namespace Blood_Alcohol.Views
{
    public partial class DebugView : UserControl
    {
        private Window? _hostWindow;

        /// <summary>
        /// 初始化调试页容器并注册生命周期事件。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由 WPF 创建视图时调用，负责宿主窗口关闭绑定和页签选择约束。
        /// </remarks>
        public DebugView()
        {
            InitializeComponent();
            Loaded += DebugView_Loaded;
        }

        /// <summary>
        /// 处理调试页加载并绑定宿主窗口关闭事件。
        /// </summary>
        /// By:ChengLei
        /// <param name="sender">事件发送对象。</param>
        /// <param name="e">路由事件参数。</param>
        /// <remarks>
        /// 由 WPF Loaded 触发，确保调试页视图模型只在窗口关闭时释放。
        /// </remarks>
        private void DebugView_Loaded(object sender, RoutedEventArgs e)
        {
            BindHostWindowClosed();
        }

        /// <summary>
        /// 绑定宿主窗口关闭事件。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由 Loaded 调用，避免普通 Unloaded 直接释放调试页视图模型。
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
        /// 处理宿主窗口关闭并释放调试页视图模型。
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

            if (DataContext is DebugViewModel vm)
            {
                vm.Dispose();
            }
        }

        /// <summary>
        /// 处理调试页签选择变化并约束当前模式下可访问页签。
        /// </summary>
        /// By:ChengLei
        /// <param name="sender">事件发送对象。</param>
        /// <param name="e">页签选择事件参数。</param>
        /// <remarks>
        /// 由 DebugTabControl.SelectionChanged 触发，避免自动模式下进入受限页签。
        /// </remarks>
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
