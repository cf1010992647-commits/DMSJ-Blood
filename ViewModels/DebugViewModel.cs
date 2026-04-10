using System;
using System.Windows;
using Blood_Alcohol.Services;

namespace Blood_Alcohol.ViewModels
{
    public class DebugViewModel : BaseViewModel, IDisposable
    {
        // DebugView tabs after moving Communication out:
        // 0: Axis(Manual)
        // 1: PointMonitor(Always)
        // 2: Fault(Manual)
        // 3: Coordinate(Manual)
        // 4: WeightToZ(Manual)
        // 5: ParameterConfig(Manual)
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

        public DebugViewModel()
        {
            OperationModeService.ModeChanged += OnModeChanged;
        }

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

        public int NormalizeTabIndex(int index)
        {
            if (!IsManualMode && IsManualOnlyTab(index))
            {
                return AutoModeFallbackTabIndex;
            }

            return index;
        }

        private static bool IsManualOnlyTab(int index)
        {
            return index == 0 || index == 2 || index == 3 || index == 4 || index == 5;
        }

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
