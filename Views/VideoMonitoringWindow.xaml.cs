using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Blood_Alcohol.ViewModels;

namespace Blood_Alcohol.Views
{
    /// <summary>
    /// 作用
    /// 视频监控窗口
    /// </summary>
    public partial class VideoMonitoringWindow : Window
    {
        private readonly DispatcherTimer _playbackTimer;
        private VideoMonitoringViewModel? _viewModel;
        private bool _isDraggingProgress;

        /// <summary>
        /// 初始化视频监控窗口
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 当前窗口仅负责本地录像回放界面和播放器控件联动 不处理实时相机直连
        /// </remarks>
        public VideoMonitoringWindow()
        {
            InitializeComponent();
            _playbackTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(300)
            };
            _playbackTimer.Tick += PlaybackTimer_Tick;
            BindViewModel();
        }

        /// <summary>
        /// 绑定当前窗口的数据上下文并订阅回放源变更
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由构造函数调用 让窗口代码后置可以感知选中录像变化
        /// </remarks>
        private void BindViewModel()
        {
            if (DataContext is not VideoMonitoringViewModel viewModel)
            {
                return;
            }

            _viewModel = viewModel;
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            ApplySelectedRecording();
        }

        /// <summary>
        /// 响应视图模型属性变化并在选中录像变更时加载媒体
        /// </summary>
        /// By:ChengLei
        /// <param name="sender">事件发送对象。</param>
        /// <param name="e">属性变更事件参数。</param>
        /// <remarks>
        /// 当前只关心 SelectedRecordingUri 相关字段 其余界面绑定由 XAML 自行更新
        /// </remarks>
        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(VideoMonitoringViewModel.SelectedRecordingUri))
            {
                ApplySelectedRecording();
            }
        }

        /// <summary>
        /// 根据当前选中录像加载或清空播放器
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 选中有效录像时自动开始播放 选中为空或文件不存在时恢复占位提示
        /// </remarks>
        private void ApplySelectedRecording()
        {
            if (_viewModel == null)
            {
                return;
            }

            Uri? source = _viewModel.SelectedRecordingUri;
            if (source == null || !File.Exists(source.LocalPath))
            {
                _playbackTimer.Stop();
                PlaybackMediaElement.Stop();
                PlaybackMediaElement.Source = null;
                PlaceholderPanel.Visibility = Visibility.Visible;
                _viewModel.ResetPlaybackProgress();
                _viewModel.SetPlaybackState("待选择");
                return;
            }

            _playbackTimer.Stop();
            PlaybackMediaElement.Stop();
            PlaybackMediaElement.Source = source;
            PlaybackMediaElement.Position = TimeSpan.Zero;
            PlaceholderPanel.Visibility = Visibility.Collapsed;
            PlaybackMediaElement.Play();
            _playbackTimer.Start();
            _viewModel.SetPlaybackState("播放中");
        }

        /// <summary>
        /// 处理开始播放按钮点击
        /// </summary>
        /// By:ChengLei
        /// <param name="sender">事件发送对象。</param>
        /// <param name="e">路由事件参数。</param>
        /// <remarks>
        /// 已加载媒体时继续播放 未加载媒体时尝试按当前选中录像重新加载
        /// </remarks>
        private void StartPlaybackButton_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null || _viewModel.SelectedRecordingUri == null)
            {
                return;
            }

            if (PlaybackMediaElement.Source == null)
            {
                ApplySelectedRecording();
                return;
            }

            PlaybackMediaElement.Play();
            _playbackTimer.Start();
            _viewModel.SetPlaybackState("播放中");
        }

        /// <summary>
        /// 处理暂停按钮点击
        /// </summary>
        /// By:ChengLei
        /// <param name="sender">事件发送对象。</param>
        /// <param name="e">路由事件参数。</param>
        /// <remarks>
        /// 暂停当前媒体并保留当前位置 便于后续继续播放
        /// </remarks>
        private void PausePlaybackButton_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null || PlaybackMediaElement.Source == null)
            {
                return;
            }

            PlaybackMediaElement.Pause();
            _playbackTimer.Stop();
            _viewModel.SetPlaybackState("已暂停");
        }

        /// <summary>
        /// 处理快退按钮点击
        /// </summary>
        /// By:ChengLei
        /// <param name="sender">事件发送对象。</param>
        /// <param name="e">路由事件参数。</param>
        /// <remarks>
        /// 每次快退 10 秒 不会回退到 0 秒以下
        /// </remarks>
        private void SeekBackwardButton_Click(object sender, RoutedEventArgs e)
        {
            SeekBy(TimeSpan.FromSeconds(-10));
        }

        /// <summary>
        /// 处理快进按钮点击
        /// </summary>
        /// By:ChengLei
        /// <param name="sender">事件发送对象。</param>
        /// <param name="e">路由事件参数。</param>
        /// <remarks>
        /// 每次快进 10 秒 不会超过录像总时长
        /// </remarks>
        private void SeekForwardButton_Click(object sender, RoutedEventArgs e)
        {
            SeekBy(TimeSpan.FromSeconds(10));
        }

        /// <summary>
        /// 标记用户开始拖动进度条
        /// </summary>
        /// By:ChengLei
        /// <param name="sender">事件发送对象。</param>
        /// <param name="e">鼠标按键事件参数。</param>
        /// <remarks>
        /// 拖动期间暂停计时器对滑块值的自动覆盖
        /// </remarks>
        private void PlaybackProgressSlider_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _isDraggingProgress = true;
        }

        /// <summary>
        /// 处理进度条拖动结束并跳转到对应播放位置
        /// </summary>
        /// By:ChengLei
        /// <param name="sender">事件发送对象。</param>
        /// <param name="e">鼠标按键事件参数。</param>
        /// <remarks>
        /// 释放鼠标后立即按当前滑块秒数更新播放器位置
        /// </remarks>
        private void PlaybackProgressSlider_PreviewMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _isDraggingProgress = false;
            SeekTo(TimeSpan.FromSeconds(PlaybackProgressSlider.Value));
        }

        /// <summary>
        /// 处理媒体打开完成事件并刷新总时长信息
        /// </summary>
        /// By:ChengLei
        /// <param name="sender">事件发送对象。</param>
        /// <param name="e">路由事件参数。</param>
        /// <remarks>
        /// 成功打开录像后刷新总时长并隐藏占位提示
        /// </remarks>
        private void PlaybackMediaElement_MediaOpened(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null)
            {
                return;
            }

            TimeSpan total = PlaybackMediaElement.NaturalDuration.HasTimeSpan
                ? PlaybackMediaElement.NaturalDuration.TimeSpan
                : TimeSpan.Zero;
            PlaceholderPanel.Visibility = Visibility.Collapsed;
            _viewModel.UpdatePlaybackProgress(PlaybackMediaElement.Position, total);
            _viewModel.SetPlaybackState("播放中");
        }

        /// <summary>
        /// 处理媒体播放结束事件
        /// </summary>
        /// By:ChengLei
        /// <param name="sender">事件发送对象。</param>
        /// <param name="e">路由事件参数。</param>
        /// <remarks>
        /// 播放结束后保留在末尾并更新状态文本
        /// </remarks>
        private void PlaybackMediaElement_MediaEnded(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null)
            {
                return;
            }

            _playbackTimer.Stop();
            TimeSpan total = PlaybackMediaElement.NaturalDuration.HasTimeSpan
                ? PlaybackMediaElement.NaturalDuration.TimeSpan
                : TimeSpan.Zero;
            _viewModel.UpdatePlaybackProgress(total, total);
            _viewModel.SetPlaybackState("播放完成");
        }

        /// <summary>
        /// 处理媒体播放失败事件
        /// </summary>
        /// By:ChengLei
        /// <param name="sender">事件发送对象。</param>
        /// <param name="e">媒体失败事件参数。</param>
        /// <remarks>
        /// 播放失败时停止计时器并恢复占位提示 便于用户重新选择文件
        /// </remarks>
        private void PlaybackMediaElement_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            if (_viewModel == null)
            {
                return;
            }

            _playbackTimer.Stop();
            PlaceholderPanel.Visibility = Visibility.Visible;
            _viewModel.ResetPlaybackProgress();
            _viewModel.SetPlaybackState("播放失败");
        }

        /// <summary>
        /// 定时同步当前媒体播放进度
        /// </summary>
        /// By:ChengLei
        /// <param name="sender">事件发送对象。</param>
        /// <param name="e">事件参数。</param>
        /// <remarks>
        /// 播放过程中每 300ms 同步一次进度条和时间文本
        /// </remarks>
        private void PlaybackTimer_Tick(object? sender, EventArgs e)
        {
            if (_viewModel == null || _isDraggingProgress || PlaybackMediaElement.Source == null)
            {
                return;
            }

            TimeSpan total = PlaybackMediaElement.NaturalDuration.HasTimeSpan
                ? PlaybackMediaElement.NaturalDuration.TimeSpan
                : TimeSpan.Zero;
            _viewModel.UpdatePlaybackProgress(PlaybackMediaElement.Position, total);
        }

        /// <summary>
        /// 按指定偏移量移动当前播放位置
        /// </summary>
        /// By:ChengLei
        /// <param name="offset">正数表示快进 负数表示快退。</param>
        /// <remarks>
        /// 由快进快退按钮复用调用
        /// </remarks>
        private void SeekBy(TimeSpan offset)
        {
            if (PlaybackMediaElement.Source == null)
            {
                return;
            }

            SeekTo(PlaybackMediaElement.Position + offset);
        }

        /// <summary>
        /// 把播放器定位到指定时间
        /// </summary>
        /// By:ChengLei
        /// <param name="position">目标播放时间。</param>
        /// <remarks>
        /// 会自动裁剪到 0 和总时长之间 并同步界面进度
        /// </remarks>
        private void SeekTo(TimeSpan position)
        {
            if (_viewModel == null || PlaybackMediaElement.Source == null)
            {
                return;
            }

            TimeSpan total = PlaybackMediaElement.NaturalDuration.HasTimeSpan
                ? PlaybackMediaElement.NaturalDuration.TimeSpan
                : TimeSpan.Zero;
            if (position < TimeSpan.Zero)
            {
                position = TimeSpan.Zero;
            }

            if (total > TimeSpan.Zero && position > total)
            {
                position = total;
            }

            PlaybackMediaElement.Position = position;
            _viewModel.UpdatePlaybackProgress(position, total);
        }

        /// <summary>
        /// 处理窗口关闭并释放播放器相关资源
        /// </summary>
        /// By:ChengLei
        /// <param name="e">关闭事件参数。</param>
        /// <remarks>
        /// 关闭前取消计时器和属性订阅 避免窗口销毁后继续访问 UI 对象
        /// </remarks>
        protected override void OnClosed(EventArgs e)
        {
            _playbackTimer.Stop();
            _playbackTimer.Tick -= PlaybackTimer_Tick;
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
            }

            PlaybackMediaElement.Stop();
            base.OnClosed(e);
        }
    }
}
