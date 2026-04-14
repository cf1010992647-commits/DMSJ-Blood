using System;
using System.Windows;
using Blood_Alcohol.Services;

namespace Blood_Alcohol.ViewModels
{
    /// <summary>
    /// 调试页主视图模型，负责按运行模式控制可访问页签。
    /// </summary>
    /// By:ChengLei
    /// <remarks>
    /// 由 DebugView 作为 DataContext 创建，并监听 OperationModeService 模式变化。
    /// </remarks>
    public class DebugViewModel : BaseViewModel, IDisposable
    {
        private const int AutoModeFallbackTabIndex = 1;

        private bool _isManualMode = OperationModeService.IsManualMode;
        private int _selectedTabIndex = OperationModeService.IsManualMode ? 0 : AutoModeFallbackTabIndex;
        private bool _disposed;

        public bool IsManualMode
        {
            get => _isManualMode;
            private set
            {
                if (_isManualMode != value)
                {
                    _isManualMode = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ModeHintText));
                }
            }
        }

        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set
            {
                int normalized = NormalizeTabIndex(value);
                if (_selectedTabIndex != normalized)
                {
                    _selectedTabIndex = normalized;
                    OnPropertyChanged();
                }
            }
        }

        public string ModeHintText => IsManualMode
            ? "手动模式：可进入全部调试页面"
            : "自动模式：轴运动/故障/坐标/Weight-Z/参数配置已锁定";

        /// <summary>
        /// 初始化调试页视图模型并注册模式切换监听。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由页面初始化调用；后续通过 OnModeChanged 同步界面状态。
        /// </remarks>
        public DebugViewModel()
        {
            OperationModeService.ModeChanged += OnModeChanged;
        }

        /// <summary>
        /// 处理运行模式变化并刷新当前页签可访问性。
        /// </summary>
        /// By:ChengLei
        /// <param name="mode">最新运行模式（手动或自动）。</param>
        /// <remarks>
        /// 由 OperationModeService.ModeChanged 事件回调触发。
        /// </remarks>
        private void OnModeChanged(OperationMode mode)
        {
            RunOnUiThread(() =>
            {
                IsManualMode = mode == OperationMode.Manual;
                if (!IsManualMode && IsManualOnlyTab(SelectedTabIndex))
                {
                    SelectedTabIndex = AutoModeFallbackTabIndex;
                }
            });
        }

        /// <summary>
        /// 规范化目标页签索引，必要时回退到自动模式允许页。
        /// </summary>
        /// By:ChengLei
        /// <param name="index">请求切换到的页签索引。</param>
        /// <returns>返回最终可用的页签索引。</returns>
        /// <remarks>
        /// 由 SelectedTabIndex 的 setter 调用，避免自动模式进入受限页签。
        /// </remarks>
        public int NormalizeTabIndex(int index)
        {
            if (!IsManualMode && IsManualOnlyTab(index))
            {
                return AutoModeFallbackTabIndex;
            }

            return index;
        }

        /// <summary>
        /// 判断指定页签是否属于仅手动模式可访问页。
        /// </summary>
        /// By:ChengLei
        /// <param name="index">待判断的页签索引。</param>
        /// <returns>返回是否为手动专用页签。</returns>
        /// <remarks>
        /// 由 NormalizeTabIndex 与 OnModeChanged 调用。
        /// </remarks>
        private static bool IsManualOnlyTab(int index)
        {
            return index == 0 || index == 2 || index == 3 || index == 4 || index == 5;
        }

        /// <summary>
        /// 在UI线程执行指定操作。
        /// </summary>
        /// By:ChengLei
        /// <param name="action">需要执行的UI更新委托。</param>
        /// <remarks>
        /// 由 OnModeChanged 调用，确保属性更新在WPF线程执行。
        /// </remarks>
        private static void RunOnUiThread(Action action)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                action();
                return;
            }

            _ = dispatcher.BeginInvoke(action);
        }

        /// <summary>
        /// 释放视图模型资源并注销模式变更事件。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由页面销毁流程调用，避免事件未解绑导致内存泄漏。
        /// </remarks>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            OperationModeService.ModeChanged -= OnModeChanged;
        }
    }
}
