using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace Blood_Alcohol.ViewModels
{
    /// <summary>
    /// 视频监控窗口视图模型
    /// </summary>
    /// By:ChengLei
    /// <remarks>
    /// 承载监控配置展示 本地录像按日期筛选 和播放器状态字段 不直接处理播放器控件本身
    /// </remarks>
    public sealed class VideoMonitoringViewModel : BaseViewModel
    {
        private static readonly string[] SupportedRecordingExtensions = [".mp4", ".avi", ".mkv", ".wmv", ".mov", ".m4v"];

        private readonly List<VideoRecordingItem> _allRecordedFiles = [];

        private int _selectedTabIndex;
        private string _cameraIpAddress = "192.168.1.101";
        private string _cameraPort = "554";
        private string _cameraUserName = "admin";
        private string _cameraPassword = "请输入相机密码";
        private string _selectedProtocol = "RTSP";
        private string _selectedStreamType = "主码流";
        private string _cameraChannelNo = "1";
        private int _coverDays = 7;
        private string _selectedSegmentStrategy = "按时间分段";
        private bool _isAutoOverwriteEnabled = true;
        private bool _isTimestampOverlayEnabled = true;
        private string _selectedStorageType = "本地磁盘";
        private string _storagePath = @"D:\VideoMonitoring\Recordings";
        private double _diskUsagePercent = 28;
        private int _storageKeepDays = 30;
        private DateTime? _selectedPlaybackDate;
        private VideoRecordingItem? _selectedRecording;
        private string _playbackStateText = "待选择";
        private double _playbackProgressSeconds;
        private double _playbackDurationSeconds = 1;
        private string _playbackCurrentTimeText = "00:00:00";
        private string _playbackDurationText = "00:00:00";
        private string _playbackHintText = "点击刷新后按日期筛选本地录像文件";

        /// <summary>
        /// 初始化视频监控视图模型
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 启动时先填充界面下拉选项 并尝试从当前储存路径扫描已有录像
        /// </remarks>
        public VideoMonitoringViewModel()
        {
            ProtocolOptions = new ObservableCollection<string> { "RTSP", "SDK", "HTTP" };
            StreamOptions = new ObservableCollection<string> { "主码流", "子码流", "第三码流" };
            SegmentStrategyOptions = new ObservableCollection<string> { "按时间分段", "按大小分段" };
            StorageTypeOptions = new ObservableCollection<string> { "本地磁盘", "网络磁盘", "NAS" };
            RecordedFiles = new ObservableCollection<VideoRecordingItem>();
            RefreshRecordedFilesCommand = new RelayCommand(_ => ReloadRecordedFiles());
            ReloadRecordedFiles();
        }

        /// <summary>
        /// 获取刷新录像列表命令
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 回放区点击刷新时重新扫描当前储存路径
        /// </remarks>
        public ICommand RefreshRecordedFilesCommand { get; }

        /// <summary>
        /// 获取当前日期筛选后的录像列表
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 回放区录像列表绑定该集合
        /// </remarks>
        public ObservableCollection<VideoRecordingItem> RecordedFiles { get; }

        /// <summary>
        /// 获取监控相机显示名称
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 左侧标题区显示当前监控位名称
        /// </remarks>
        public string CameraDisplayName => "本地录像回放";

        /// <summary>
        /// 获取相机状态说明
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 预览区标题下方用于提示当前回放模式和数据来源
        /// </remarks>
        public string CameraStatusText => "支持按日期筛选本地录像并在当前窗口回放";

        /// <summary>
        /// 获取监控状态文本
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 左上角状态标签使用该字段
        /// </remarks>
        public string MonitorStatusText => _playbackStateText;

        /// <summary>
        /// 获取监控时间戳文本
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 预览区右上角显示当前所选录像时间
        /// </remarks>
        public string MonitorTimestamp => _selectedRecording?.RecordedAt.ToString("yyyy/MM/dd HH:mm:ss") ?? "未选择录像";

        /// <summary>
        /// 获取视频规格说明文本
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 预览区底部左侧显示录像文件基础信息
        /// </remarks>
        public string VideoSpecText => _selectedRecording?.FileSizeText ?? "本地录像回放";

        /// <summary>
        /// 获取码率显示文本
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 预览区底部左侧显示录像文件扩展名
        /// </remarks>
        public string StreamBitrateText => _selectedRecording?.FileExtensionText ?? "等待选择录像";

        /// <summary>
        /// 获取回放区提示文本
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 根据目录状态和筛选结果动态提示当前可执行动作
        /// </remarks>
        public string PlaybackHintText
        {
            get => _playbackHintText;
            private set => SetField(ref _playbackHintText, value);
        }

        /// <summary>
        /// 获取回放区摘要文本
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 用于说明当前日期筛选结果和录像数量
        /// </remarks>
        public string PlaybackSelectionSummary
        {
            get
            {
                if (_selectedPlaybackDate.HasValue)
                {
                    return $"{_selectedPlaybackDate:yyyy/MM/dd} 共筛出 {RecordedFiles.Count} 段录像";
                }

                return $"全部日期共 {_allRecordedFiles.Count} 段录像";
            }
        }

        /// <summary>
        /// 获取当前选中录像标题
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 回放控制区顶部显示当前待播放或正在播放的录像名称
        /// </remarks>
        public string SelectedRecordingTitle => _selectedRecording?.DisplayName ?? "未选择录像";

        /// <summary>
        /// 获取当前选中录像说明
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 回放控制区顶部显示当前录像时间和大小
        /// </remarks>
        public string SelectedRecordingSummaryText => _selectedRecording?.SummaryText ?? "请先选择日期并从左侧列表选择录像";

        /// <summary>
        /// 获取当前选中录像路径
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 回放控制区底部展示当前回放文件来源
        /// </remarks>
        public string SelectedRecordingPathText => _selectedRecording?.FullPath ?? StoragePath;

        /// <summary>
        /// 获取已扫描到的录像文件总数字符串
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 右侧目录信息区显示当前录像总量
        /// </remarks>
        public string TotalRecordedFileCountText => $"{_allRecordedFiles.Count} 个";

        /// <summary>
        /// 获取已扫描到的录像日期总数字符串
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 右侧目录信息区显示当前存在录像的日期数量
        /// </remarks>
        public string RecordedDateCountText => $"{_allRecordedFiles.Select(item => item.RecordedAt.Date).Distinct().Count()} 天";

        /// <summary>
        /// 获取最近一段录像说明文本
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 右侧目录信息区显示最新录像的时间和名称
        /// </remarks>
        public string LatestRecordedFileText => _allRecordedFiles.Count > 0
            ? $"{_allRecordedFiles[0].RecordedAt:yyyy/MM/dd HH:mm:ss} · {_allRecordedFiles[0].DisplayName}"
            : "暂无录像";

        /// <summary>
        /// 获取支持的录像格式说明文本
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 右侧目录信息区展示当前支持扫描的文件扩展名
        /// </remarks>
        public string SupportedFormatText => "MP4 / AVI / MKV / WMV / MOV / M4V";

        /// <summary>
        /// 获取当前选中录像源地址
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 播放窗口代码后置读取该字段并加载 MediaElement
        /// </remarks>
        public Uri? SelectedRecordingUri => string.IsNullOrWhiteSpace(_selectedRecording?.FullPath)
            ? null
            : new Uri(_selectedRecording.FullPath, UriKind.Absolute);

        /// <summary>
        /// 获取当前是否已选中录像
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 控制播放按钮和进度条是否可操作
        /// </remarks>
        public bool HasSelectedRecording => _selectedRecording != null;

        /// <summary>
        /// 获取占位提示面板显示状态
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 预览区未选择录像时显示占位提示
        /// </remarks>
        public Visibility PlaceholderVisibility => HasSelectedRecording ? Visibility.Collapsed : Visibility.Visible;

        /// <summary>
        /// 获取当前播放状态文本
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 预览区底部右侧显示当前播放器状态
        /// </remarks>
        public string PlaybackStateText => _playbackStateText;

        /// <summary>
        /// 获取或设置当前播放进度秒数
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 进度滑块双向绑定该字段
        /// </remarks>
        public double PlaybackProgressSeconds
        {
            get => _playbackProgressSeconds;
            set => SetField(ref _playbackProgressSeconds, value);
        }

        /// <summary>
        /// 获取或设置当前播放总时长秒数
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 进度滑块最大值绑定该字段
        /// </remarks>
        public double PlaybackDurationSeconds
        {
            get => _playbackDurationSeconds;
            set => SetField(ref _playbackDurationSeconds, value);
        }

        /// <summary>
        /// 获取当前播放位置文本
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 进度条左侧显示当前回放时间
        /// </remarks>
        public string PlaybackCurrentTimeText
        {
            get => _playbackCurrentTimeText;
            private set => SetField(ref _playbackCurrentTimeText, value);
        }

        /// <summary>
        /// 获取总时长文本
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 进度条右侧显示当前录像总时长
        /// </remarks>
        public string PlaybackDurationText
        {
            get => _playbackDurationText;
            private set => SetField(ref _playbackDurationText, value);
        }

        /// <summary>
        /// 获取连接协议选项集合
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 连接页协议下拉框绑定该集合
        /// </remarks>
        public ObservableCollection<string> ProtocolOptions { get; }

        /// <summary>
        /// 获取码流选项集合
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 连接页码流下拉框绑定该集合
        /// </remarks>
        public ObservableCollection<string> StreamOptions { get; }

        /// <summary>
        /// 获取分段策略选项集合
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 覆盖页分段策略下拉框绑定该集合
        /// </remarks>
        public ObservableCollection<string> SegmentStrategyOptions { get; }

        /// <summary>
        /// 获取储存类型选项集合
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 储存页储存类型下拉框绑定该集合
        /// </remarks>
        public ObservableCollection<string> StorageTypeOptions { get; }

        /// <summary>
        /// 获取或设置当前选中的页签索引
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 0 为连接 1 为覆盖 2 为储存
        /// </remarks>
        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set => SetField(ref _selectedTabIndex, value);
        }

        /// <summary>
        /// 获取或设置相机 IP 地址
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 当前仅作为界面输入占位字段
        /// </remarks>
        public string CameraIpAddress
        {
            get => _cameraIpAddress;
            set
            {
                if (SetField(ref _cameraIpAddress, value))
                {
                    OnPropertyChanged(nameof(StreamPreviewAddress));
                }
            }
        }

        /// <summary>
        /// 获取或设置相机端口
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 当前仅作为界面输入占位字段
        /// </remarks>
        public string CameraPort
        {
            get => _cameraPort;
            set
            {
                if (SetField(ref _cameraPort, value))
                {
                    OnPropertyChanged(nameof(StreamPreviewAddress));
                }
            }
        }

        /// <summary>
        /// 获取或设置相机用户名
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 当前仅作为界面输入占位字段
        /// </remarks>
        public string CameraUserName
        {
            get => _cameraUserName;
            set
            {
                if (SetField(ref _cameraUserName, value))
                {
                    OnPropertyChanged(nameof(StreamPreviewAddress));
                }
            }
        }

        /// <summary>
        /// 获取或设置相机密码
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 当前仅作为界面输入占位字段
        /// </remarks>
        public string CameraPassword
        {
            get => _cameraPassword;
            set
            {
                if (SetField(ref _cameraPassword, value))
                {
                    OnPropertyChanged(nameof(StreamPreviewAddress));
                }
            }
        }

        /// <summary>
        /// 获取或设置当前协议
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 连接页协议下拉框双向绑定该字段
        /// </remarks>
        public string SelectedProtocol
        {
            get => _selectedProtocol;
            set
            {
                if (SetField(ref _selectedProtocol, value))
                {
                    OnPropertyChanged(nameof(StreamPreviewAddress));
                }
            }
        }

        /// <summary>
        /// 获取或设置当前码流类型
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 连接页码流下拉框双向绑定该字段
        /// </remarks>
        public string SelectedStreamType
        {
            get => _selectedStreamType;
            set => SetField(ref _selectedStreamType, value);
        }

        /// <summary>
        /// 获取或设置当前通道号
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 连接页通道号输入框双向绑定该字段
        /// </remarks>
        public string CameraChannelNo
        {
            get => _cameraChannelNo;
            set
            {
                if (SetField(ref _cameraChannelNo, value))
                {
                    OnPropertyChanged(nameof(StreamPreviewAddress));
                }
            }
        }

        /// <summary>
        /// 获取流地址预览文本
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 用于连接页展示后续将要使用的地址格式
        /// </remarks>
        public string StreamPreviewAddress => $"{SelectedProtocol.ToLower()}://{CameraUserName}:******@{CameraIpAddress}:{CameraPort}/Streaming/Channels/{CameraChannelNo.PadLeft(3, '0')}";

        /// <summary>
        /// 获取或设置覆盖天数
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 覆盖页滑块绑定该字段
        /// </remarks>
        public int CoverDays
        {
            get => _coverDays;
            set
            {
                if (SetField(ref _coverDays, value))
                {
                    OnPropertyChanged(nameof(CoverDaysText));
                }
            }
        }

        /// <summary>
        /// 获取覆盖天数字符串
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 覆盖页右上角说明绑定该字段
        /// </remarks>
        public string CoverDaysText => $"{_coverDays} 天";

        /// <summary>
        /// 获取或设置当前分段策略
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 覆盖页分段策略下拉框绑定该字段
        /// </remarks>
        public string SelectedSegmentStrategy
        {
            get => _selectedSegmentStrategy;
            set => SetField(ref _selectedSegmentStrategy, value);
        }

        /// <summary>
        /// 获取或设置是否自动覆盖旧文件
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 覆盖页勾选框绑定该字段
        /// </remarks>
        public bool IsAutoOverwriteEnabled
        {
            get => _isAutoOverwriteEnabled;
            set => SetField(ref _isAutoOverwriteEnabled, value);
        }

        /// <summary>
        /// 获取或设置是否叠加时间戳
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 覆盖页勾选框绑定该字段
        /// </remarks>
        public bool IsTimestampOverlayEnabled
        {
            get => _isTimestampOverlayEnabled;
            set => SetField(ref _isTimestampOverlayEnabled, value);
        }

        /// <summary>
        /// 获取或设置当前储存类型
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 储存页下拉框绑定该字段
        /// </remarks>
        public string SelectedStorageType
        {
            get => _selectedStorageType;
            set => SetField(ref _selectedStorageType, value);
        }

        /// <summary>
        /// 获取或设置储存路径
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 储存页路径输入框绑定该字段 点击刷新录像后会按当前路径重新扫描
        /// </remarks>
        public string StoragePath
        {
            get => _storagePath;
            set
            {
                if (SetField(ref _storagePath, value))
                {
                    OnPropertyChanged(nameof(SelectedRecordingPathText));
                }
            }
        }

        /// <summary>
        /// 获取或设置磁盘使用百分比
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 储存页进度条绑定该字段
        /// </remarks>
        public double DiskUsagePercent
        {
            get => _diskUsagePercent;
            set => SetField(ref _diskUsagePercent, value);
        }

        /// <summary>
        /// 获取磁盘使用文本
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 当前为静态占位展示文本
        /// </remarks>
        public string DiskUsageText => "142 GB / 500 GB";

        /// <summary>
        /// 获取或设置储存保留天数
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 储存页滑块绑定该字段
        /// </remarks>
        public int StorageKeepDays
        {
            get => _storageKeepDays;
            set
            {
                if (SetField(ref _storageKeepDays, value))
                {
                    OnPropertyChanged(nameof(StorageKeepDaysText));
                }
            }
        }

        /// <summary>
        /// 获取储存保留天数字符串
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 储存页右上角说明绑定该字段
        /// </remarks>
        public string StorageKeepDaysText => $"{_storageKeepDays} 天";

        /// <summary>
        /// 获取或设置当前回放日期
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 改变日期时会按所选日期刷新录像列表
        /// </remarks>
        public DateTime? SelectedPlaybackDate
        {
            get => _selectedPlaybackDate;
            set
            {
                if (SetField(ref _selectedPlaybackDate, value))
                {
                    ApplyPlaybackDateFilter();
                }
            }
        }

        /// <summary>
        /// 获取或设置当前选中的录像
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 选中录像后由窗口代码后置加载到播放器中
        /// </remarks>
        public VideoRecordingItem? SelectedRecording
        {
            get => _selectedRecording;
            set
            {
                if (SetField(ref _selectedRecording, value))
                {
                    OnPropertyChanged(nameof(HasSelectedRecording));
                    OnPropertyChanged(nameof(PlaceholderVisibility));
                    OnPropertyChanged(nameof(SelectedRecordingTitle));
                    OnPropertyChanged(nameof(SelectedRecordingSummaryText));
                    OnPropertyChanged(nameof(SelectedRecordingPathText));
                    OnPropertyChanged(nameof(SelectedRecordingUri));
                    OnPropertyChanged(nameof(MonitorTimestamp));
                    OnPropertyChanged(nameof(VideoSpecText));
                    OnPropertyChanged(nameof(StreamBitrateText));
                    ResetPlaybackProgress();
                    SetPlaybackState(value == null ? "待选择" : "待播放");
                }
            }
        }

        /// <summary>
        /// 重新扫描当前储存路径中的录像文件
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 回放区点击刷新后调用 仅扫描支持的常见视频扩展名
        /// </remarks>
        public void ReloadRecordedFiles()
        {
            _allRecordedFiles.Clear();
            RecordedFiles.Clear();
            SelectedRecording = null;

            if (!Directory.Exists(StoragePath))
            {
                PlaybackHintText = "当前储存目录不存在 请先确认录像目录或修改后点击刷新";
                NotifyRecordingStatisticsChanged();
                OnPropertyChanged(nameof(PlaybackSelectionSummary));
                return;
            }

            List<VideoRecordingItem> files = Directory.EnumerateFiles(StoragePath, "*.*", SearchOption.AllDirectories)
                .Where(IsSupportedRecordingFile)
                .Select(BuildRecordingItem)
                .OrderByDescending(item => item.RecordedAt)
                .ToList();

            _allRecordedFiles.AddRange(files);

            if (_allRecordedFiles.Count == 0)
            {
                PlaybackHintText = "当前目录还没有检测到可播放的录像文件";
                SelectedPlaybackDate = null;
                NotifyRecordingStatisticsChanged();
                OnPropertyChanged(nameof(PlaybackSelectionSummary));
                return;
            }

            NotifyRecordingStatisticsChanged();

            DateTime latestDate = _allRecordedFiles[0].RecordedAt.Date;
            if (_selectedPlaybackDate.HasValue && _allRecordedFiles.Any(item => item.RecordedAt.Date == _selectedPlaybackDate.Value.Date))
            {
                ApplyPlaybackDateFilter();
            }
            else
            {
                SelectedPlaybackDate = latestDate;
            }
        }

        /// <summary>
        /// 根据播放器当前状态刷新状态文本
        /// </summary>
        /// By:ChengLei
        /// <param name="stateText">新的状态文本。</param>
        /// <remarks>
        /// 由窗口代码后置在播放 开始 暂停 失败 等节点调用
        /// </remarks>
        public void SetPlaybackState(string stateText)
        {
            if (SetField(ref _playbackStateText, stateText, nameof(PlaybackStateText)))
            {
                OnPropertyChanged(nameof(MonitorStatusText));
            }
        }

        /// <summary>
        /// 根据播放器当前位置刷新进度和时间文本
        /// </summary>
        /// By:ChengLei
        /// <param name="current">当前播放时间。</param>
        /// <param name="total">总时长。</param>
        /// <remarks>
        /// 由窗口代码后置的计时器和媒体打开事件调用
        /// </remarks>
        public void UpdatePlaybackProgress(TimeSpan current, TimeSpan total)
        {
            PlaybackProgressSeconds = Math.Max(0, current.TotalSeconds);
            PlaybackDurationSeconds = Math.Max(1, total.TotalSeconds);
            PlaybackCurrentTimeText = FormatTimeText(current);
            PlaybackDurationText = FormatTimeText(total);
        }

        /// <summary>
        /// 重置播放器进度显示
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由切换录像或清空选择时调用
        /// </remarks>
        public void ResetPlaybackProgress()
        {
            PlaybackProgressSeconds = 0;
            PlaybackDurationSeconds = 1;
            PlaybackCurrentTimeText = "00:00:00";
            PlaybackDurationText = "00:00:00";
        }

        /// <summary>
        /// 按当前日期筛选可回放录像
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由 SelectedPlaybackDate setter 和刷新录像逻辑复用调用
        /// </remarks>
        private void ApplyPlaybackDateFilter()
        {
            RecordedFiles.Clear();

            IEnumerable<VideoRecordingItem> filteredFiles = _selectedPlaybackDate.HasValue
                ? _allRecordedFiles.Where(item => item.RecordedAt.Date == _selectedPlaybackDate.Value.Date)
                : _allRecordedFiles;

            foreach (VideoRecordingItem item in filteredFiles)
            {
                RecordedFiles.Add(item);
            }

            if (RecordedFiles.Count > 0)
            {
                PlaybackHintText = $"已筛出 {RecordedFiles.Count} 段录像 可选择后直接回放";
                SelectedRecording = RecordedFiles[0];
            }
            else
            {
                PlaybackHintText = _selectedPlaybackDate.HasValue
                    ? "所选日期没有录像文件 请切换日期或点击刷新"
                    : "当前没有可回放录像文件";
                SelectedRecording = null;
            }

            OnPropertyChanged(nameof(PlaybackSelectionSummary));
        }

        /// <summary>
        /// 通知右侧目录统计信息刷新
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由录像目录重扫后调用 统一更新文件数 日期数 和最近录像展示
        /// </remarks>
        private void NotifyRecordingStatisticsChanged()
        {
            OnPropertyChanged(nameof(TotalRecordedFileCountText));
            OnPropertyChanged(nameof(RecordedDateCountText));
            OnPropertyChanged(nameof(LatestRecordedFileText));
            OnPropertyChanged(nameof(SupportedFormatText));
        }

        /// <summary>
        /// 判断文件是否属于支持的录像扩展名
        /// </summary>
        /// By:ChengLei
        /// <param name="filePath">待判断文件完整路径。</param>
        /// <returns>支持回放返回 true，否则返回 false。</returns>
        /// <remarks>
        /// 仅识别常见本地视频格式 避免把图片或日志文件混进录像列表
        /// </remarks>
        private static bool IsSupportedRecordingFile(string filePath)
        {
            string extension = Path.GetExtension(filePath);
            return SupportedRecordingExtensions.Any(item => string.Equals(item, extension, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 根据文件路径构造录像列表项
        /// </summary>
        /// By:ChengLei
        /// <param name="filePath">录像文件完整路径。</param>
        /// <returns>返回界面使用的录像列表项。</returns>
        /// <remarks>
        /// 当前按文件最后写入时间作为录像时间来源
        /// </remarks>
        private static VideoRecordingItem BuildRecordingItem(string filePath)
        {
            FileInfo fileInfo = new FileInfo(filePath);
            DateTime recordedAt = fileInfo.LastWriteTime;
            return new VideoRecordingItem(
                fileInfo.FullName,
                fileInfo.Name,
                recordedAt,
                FormatFileSize(fileInfo.Length),
                Path.GetExtension(fileInfo.Name).TrimStart('.').ToUpperInvariant());
        }

        /// <summary>
        /// 把字节大小转换为便于界面展示的文本
        /// </summary>
        /// By:ChengLei
        /// <param name="length">文件字节数。</param>
        /// <returns>返回格式化后的大小文本。</returns>
        /// <remarks>
        /// 当前最多显示到 GB 单位
        /// </remarks>
        private static string FormatFileSize(long length)
        {
            double size = length;
            string[] units = ["B", "KB", "MB", "GB"];
            int index = 0;
            while (size >= 1024 && index < units.Length - 1)
            {
                size /= 1024;
                index++;
            }

            return $"{size:0.#} {units[index]}";
        }

        /// <summary>
        /// 把时长转换为播放器时间文本
        /// </summary>
        /// By:ChengLei
        /// <param name="time">待格式化时长。</param>
        /// <returns>返回 HH:mm:ss 格式文本。</returns>
        /// <remarks>
        /// 当前回放条左侧和右侧统一使用该格式
        /// </remarks>
        private static string FormatTimeText(TimeSpan time)
        {
            if (time < TimeSpan.Zero)
            {
                time = TimeSpan.Zero;
            }

            return $"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}";
        }

        /// <summary>
        /// 设置字段并通知界面更新
        /// </summary>
        /// By:ChengLei
        /// <typeparam name="T">字段类型</typeparam>
        /// <param name="field">目标字段引用</param>
        /// <param name="value">要写入的新值</param>
        /// <param name="propertyName">属性名称</param>
        /// <returns>是否发生了实际变更</returns>
        /// <remarks>
        /// 用于减少属性 setter 的重复通知代码
        /// </remarks>
        private bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }

    /// <summary>
    /// 视频回放列表项
    /// </summary>
    /// By:ChengLei
    /// <remarks>
    /// 用于承载本地录像文件的基础展示字段和回放源路径
    /// </remarks>
    public sealed class VideoRecordingItem
    {
        /// <summary>
        /// 初始化视频回放列表项
        /// </summary>
        /// By:ChengLei
        /// <param name="fullPath">录像完整路径。</param>
        /// <param name="fileName">录像文件名。</param>
        /// <param name="recordedAt">录像时间。</param>
        /// <param name="fileSizeText">录像大小文本。</param>
        /// <param name="fileExtensionText">录像扩展名文本。</param>
        /// <remarks>
        /// 由 VideoMonitoringViewModel 扫描本地录像目录时创建
        /// </remarks>
        public VideoRecordingItem(string fullPath, string fileName, DateTime recordedAt, string fileSizeText, string fileExtensionText)
        {
            FullPath = fullPath;
            FileName = fileName;
            RecordedAt = recordedAt;
            FileSizeText = fileSizeText;
            FileExtensionText = fileExtensionText;
        }

        /// <summary>
        /// 录像完整路径
        /// </summary>
        /// By:ChengLei
        public string FullPath { get; }

        /// <summary>
        /// 录像文件名
        /// </summary>
        /// By:ChengLei
        public string FileName { get; }

        /// <summary>
        /// 录像时间
        /// </summary>
        /// By:ChengLei
        public DateTime RecordedAt { get; }

        /// <summary>
        /// 录像大小文本
        /// </summary>
        /// By:ChengLei
        public string FileSizeText { get; }

        /// <summary>
        /// 录像扩展名文本
        /// </summary>
        /// By:ChengLei
        public string FileExtensionText { get; }

        /// <summary>
        /// 录像显示标题
        /// </summary>
        /// By:ChengLei
        public string DisplayName => Path.GetFileNameWithoutExtension(FileName);

        /// <summary>
        /// 录像摘要文本
        /// </summary>
        /// By:ChengLei
        public string SummaryText => $"{RecordedAt:yyyy/MM/dd HH:mm:ss} · {FileSizeText} · {FileExtensionText}";
    }
}
