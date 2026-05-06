using Blood_Alcohol.Helpers;
using Blood_Alcohol.Models;
using Blood_Alcohol.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Blood_Alcohol.ViewModels
{
    /// <summary>
    /// 原位工位控制页视图模型，负责从点位监控配置中提取 X 和 Y 的原位工位点并按 C 编号分组显示。
    /// </summary>
    /// By:ChengLei
    /// <remarks>
    /// 页面使用 PointMonitorConfig.json 中的 M 点位定义，底层通信会自动补齐 4096 偏移。
    /// </remarks>
    public sealed class PointPositionControlViewModel : BaseViewModel
    {
        private const string PointMonitorConfigFileName = "PointMonitorConfig.json";
        private static readonly TimeSpan PulseWidth = TimeSpan.FromMilliseconds(100);
        private static readonly Regex PositionPointPattern = new(@"^C(?<index>\d+)(?<axis>XM|YM|MY)(?<position>原位|工位)$", RegexOptions.Compiled);

        private readonly SemaphoreSlim _plcLock = CommunicationManager.PlcAccessLock;
        private readonly ConfigService<PointMonitorConfig> _configService = new(PointMonitorConfigFileName);
        private string _statusMessage = "原位工位控制已加载，使用 PointMonitorConfig.json 中的地址定义。";

        /// <summary>
        /// 初始化原位工位控制页视图模型并立即加载配置。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 视图创建 DataContext 时会调用该构造函数。
        /// </remarks>
        public PointPositionControlViewModel()
        {
            ReloadGroups();
        }

        /// <summary>
        /// 获取按 C 编号分组后的控制卡片集合。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 每张卡片同时包含 X 和 Y 两行，便于快速切换原位和工位。
        /// </remarks>
        public ObservableCollection<PointPositionControlGroupViewModel> Groups { get; } = new();

        /// <summary>
        /// 获取或设置页面底部的状态提示文本。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 状态文本会在配置加载、脉冲下发成功或失败时更新。
        /// </remarks>
        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                if (_statusMessage != value)
                {
                    _statusMessage = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 从点位监控配置中重建按 C 编号聚合的原位工位控制集合。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 同时兼容 C0XM、C0YM 和 C0MY 这几种历史命名，最终统一显示为 X 和 Y。
        /// </remarks>
        private void ReloadGroups()
        {
            try
            {
                PointMonitorConfig config = _configService.Load() ?? new PointMonitorConfig();
                IEnumerable<PlcPointConfigItem> sourcePoints = config.Points.Any()
                    ? config.Points
                    : config.LeftPoints.Concat(config.RightPoints);

                Dictionary<int, PointPositionGroupBuilder> builders = new();
                foreach (PlcPointConfigItem point in sourcePoints)
                {
                    if (string.IsNullOrWhiteSpace(point.Address) || string.IsNullOrWhiteSpace(point.Description))
                    {
                        continue;
                    }

                    Match match = PositionPointPattern.Match(point.Description.Trim());
                    if (!match.Success)
                    {
                        continue;
                    }

                    int index = int.Parse(match.Groups["index"].Value);
                    string axis = NormalizeAxis(match.Groups["axis"].Value);
                    string position = match.Groups["position"].Value;

                    if (!builders.TryGetValue(index, out PointPositionGroupBuilder? builder))
                    {
                        builder = new PointPositionGroupBuilder(index);
                        builders[index] = builder;
                    }

                    builder.Apply(axis, position, point.Address.Trim());
                }

                Groups.Clear();
                foreach (PointPositionGroupBuilder builder in builders.OrderBy(item => item.Key).Select(item => item.Value))
                {
                    PointAxisControlItemViewModel? xAxis = builder.BuildAxisItem("X", PulsePointAsync);
                    PointAxisControlItemViewModel? yAxis = builder.BuildAxisItem("Y", PulsePointAsync);
                    if (xAxis == null && yAxis == null)
                    {
                        continue;
                    }

                    Groups.Add(new PointPositionControlGroupViewModel($"C{builder.Index}", xAxis, yAxis));
                }

                int axisCount = Groups.Sum(item => (item.XAxis != null ? 1 : 0) + (item.YAxis != null ? 1 : 0));
                StatusMessage = $"{DateTime.Now:HH:mm:ss} 已加载 {Groups.Count} 组 C 编号，包含 {axisCount} 组原位工位控制点。";
            }
            catch (Exception ex)
            {
                StatusMessage = $"{DateTime.Now:HH:mm:ss} 原位工位控制加载失败：{ex.Message}";
            }
        }

        /// <summary>
        /// 将配置中的轴命名统一转换为页面使用的 X 或 Y。
        /// </summary>
        /// By:ChengLei
        /// <param name="axisToken">配置中解析出的轴标记。</param>
        /// <returns>返回统一后的轴文本。</returns>
        /// <remarks>
        /// 历史配置中可能出现 XM、YM 或 MY，页面统一按照 X 和 Y 展示。
        /// </remarks>
        private static string NormalizeAxis(string axisToken)
        {
            return axisToken.Contains('X', StringComparison.OrdinalIgnoreCase) ? "X" : "Y";
        }

        /// <summary>
        /// 向指定点位发送原位或工位的短脉冲控制命令。
        /// </summary>
        /// By:ChengLei
        /// <param name="signalName">用于状态提示的机构名称。</param>
        /// <param name="addressText">页面显示的地址文本。</param>
        /// <param name="positionText">当前触发的动作文本。</param>
        /// <returns>返回异步控制任务。</returns>
        /// <remarks>
        /// 该方法会先写入 1，再延时 100ms 回写 0，适配按钮触发式的原工位切换操作。
        /// </remarks>
        private async Task PulsePointAsync(string signalName, string addressText, string positionText)
        {
            if (!CommunicationManager.Is485Open)
            {
                StatusMessage = $"{DateTime.Now:HH:mm:ss} {signalName} {positionText}失败：PLC 未连接。";
                return;
            }

            ushort? coilAddress = PlcAddressMapper.TryParseCoilDisplayAddress(addressText);
            if (!coilAddress.HasValue)
            {
                StatusMessage = $"{DateTime.Now:HH:mm:ss} {signalName} {positionText}失败：地址 {addressText} 无效。";
                return;
            }

            await _plcLock.WaitAsync().ConfigureAwait(true);
            try
            {
                (bool Success, string Error) writeHigh = await CommunicationManager.Plc
                    .TryWriteSingleCoilAsync(coilAddress.Value, true)
                    .ConfigureAwait(true);
                if (!writeHigh.Success)
                {
                    throw new InvalidOperationException(writeHigh.Error);
                }

                await Task.Delay(PulseWidth).ConfigureAwait(true);

                (bool Success, string Error) writeLow = await CommunicationManager.Plc
                    .TryWriteSingleCoilAsync(coilAddress.Value, false)
                    .ConfigureAwait(true);
                if (!writeLow.Success)
                {
                    throw new InvalidOperationException(writeLow.Error);
                }

                StatusMessage = $"{DateTime.Now:HH:mm:ss} {signalName} {positionText}完成，{addressText} 脉冲已发送。";
            }
            catch (Exception ex)
            {
                StatusMessage = $"{DateTime.Now:HH:mm:ss} {signalName} {positionText}失败：{ex.Message}";
            }
            finally
            {
                _plcLock.Release();
            }
        }

        /// <summary>
        /// 原位工位分组构建器，用于聚合同一 C 编号下的 X 和 Y 点位。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 该类型仅在加载配置时临时使用，最终会转换为界面绑定对象。
        /// </remarks>
        private sealed class PointPositionGroupBuilder
        {
            private string _xHomeAddress = string.Empty;
            private string _xWorkAddress = string.Empty;
            private string _yHomeAddress = string.Empty;
            private string _yWorkAddress = string.Empty;

            /// <summary>
            /// 初始化指定 C 编号的原位工位构建器。
            /// </summary>
            /// By:ChengLei
            /// <param name="index">当前分组的 C 编号。</param>
            /// <remarks>
            /// 每个 C 编号在首次命中配置项时创建一个构建器实例。
            /// </remarks>
            public PointPositionGroupBuilder(int index)
            {
                Index = index;
            }

            /// <summary>
            /// 获取当前分组对应的 C 编号。
            /// </summary>
            /// By:ChengLei
            /// <remarks>
            /// 该值用于卡片标题显示和分组排序。
            /// </remarks>
            public int Index { get; }

            /// <summary>
            /// 将单个原位或工位点位应用到当前分组。
            /// </summary>
            /// By:ChengLei
            /// <param name="axis">统一后的轴文本，只允许 X 或 Y。</param>
            /// <param name="position">位置类型，只允许原位或工位。</param>
            /// <param name="address">配置中的显示地址。</param>
            /// <remarks>
            /// 该方法会根据轴和位置将地址分别填入对应的缓存字段。
            /// </remarks>
            public void Apply(string axis, string position, string address)
            {
                if (axis == "X")
                {
                    if (position == "原位")
                    {
                        _xHomeAddress = address;
                    }
                    else
                    {
                        _xWorkAddress = address;
                    }

                    return;
                }

                if (position == "原位")
                {
                    _yHomeAddress = address;
                }
                else
                {
                    _yWorkAddress = address;
                }
            }

            /// <summary>
            /// 根据指定轴生成界面绑定所需的轴控制项。
            /// </summary>
            /// By:ChengLei
            /// <param name="axis">目标轴文本，只允许 X 或 Y。</param>
            /// <param name="pulsePointAsync">用于发送脉冲命令的异步委托。</param>
            /// <returns>返回轴控制项；若该轴缺少原位或工位定义则返回空。</returns>
            /// <remarks>
            /// 页面固定只显示 X 和 Y 两种轴文本，不再直接暴露配置中的完整点名。
            /// </remarks>
            public PointAxisControlItemViewModel? BuildAxisItem(string axis, Func<string, string, string, Task> pulsePointAsync)
            {
                string homeAddress = axis == "X" ? _xHomeAddress : _yHomeAddress;
                string workAddress = axis == "X" ? _xWorkAddress : _yWorkAddress;
                if (string.IsNullOrWhiteSpace(homeAddress) || string.IsNullOrWhiteSpace(workAddress))
                {
                    return null;
                }

                string signalName = $"C{Index}{axis}";
                return new PointAxisControlItemViewModel(
                    axis,
                    signalName,
                    homeAddress,
                    workAddress,
                    () => pulsePointAsync(signalName, homeAddress, "原位"),
                    () => pulsePointAsync(signalName, workAddress, "工位"));
            }
        }
    }

    /// <summary>
    /// 单个 C 编号分组视图模型，承载 X 和 Y 两个轴的原位工位控制项。
    /// </summary>
    /// By:ChengLei
    /// <remarks>
    /// 页面中的每一张卡片都对应一个该类型实例。
    /// </remarks>
    public sealed class PointPositionControlGroupViewModel : BaseViewModel
    {
        /// <summary>
        /// 初始化单个 C 编号分组对象。
        /// </summary>
        /// By:ChengLei
        /// <param name="groupTitle">卡片标题文本。</param>
        /// <param name="xAxis">X 轴控制项。</param>
        /// <param name="yAxis">Y 轴控制项。</param>
        /// <remarks>
        /// 构造后供卡片布局直接绑定使用。
        /// </remarks>
        public PointPositionControlGroupViewModel(
            string groupTitle,
            PointAxisControlItemViewModel? xAxis,
            PointAxisControlItemViewModel? yAxis)
        {
            GroupTitle = groupTitle;
            XAxis = xAxis;
            YAxis = yAxis;
        }

        /// <summary>
        /// 获取卡片标题文本。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 标题格式为 C0、C1、C2 等。
        /// </remarks>
        public string GroupTitle { get; }

        /// <summary>
        /// 获取当前分组的 X 轴控制项。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 若配置中不存在完整的 X 轴原位工位点则为 null。
        /// </remarks>
        public PointAxisControlItemViewModel? XAxis { get; }

        /// <summary>
        /// 获取当前分组的 Y 轴控制项。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 若配置中不存在完整的 Y 轴原位工位点则为 null。
        /// </remarks>
        public PointAxisControlItemViewModel? YAxis { get; }
    }

    /// <summary>
    /// 单个轴向控制项视图模型，承载一个轴的原位和工位按钮。
    /// </summary>
    /// By:ChengLei
    /// <remarks>
    /// 页面固定显示为两行布局，每行分别表示 X 或 Y。
    /// </remarks>
    public sealed class PointAxisControlItemViewModel : BaseViewModel
    {
        /// <summary>
        /// 初始化单个轴向控制项。
        /// </summary>
        /// By:ChengLei
        /// <param name="axisText">页面显示的轴文本。</param>
        /// <param name="signalName">用于状态提示的信号名称。</param>
        /// <param name="homeAddress">原位显示地址。</param>
        /// <param name="workAddress">工位显示地址。</param>
        /// <param name="goHomeAsync">原位控制异步委托。</param>
        /// <param name="goWorkAsync">工位控制异步委托。</param>
        /// <remarks>
        /// 该对象只保留页面展示和命令触发所需的信息。
        /// </remarks>
        public PointAxisControlItemViewModel(
            string axisText,
            string signalName,
            string homeAddress,
            string workAddress,
            Func<Task> goHomeAsync,
            Func<Task> goWorkAsync)
        {
            AxisText = axisText;
            SignalName = signalName;
            HomeAddress = homeAddress;
            WorkAddress = workAddress;
            GoHomeCommand = new AsyncRelayCommand(goHomeAsync);
            GoWorkCommand = new AsyncRelayCommand(goWorkAsync);
        }

        /// <summary>
        /// 获取页面显示的轴文本。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 页面固定显示为 X 或 Y。
        /// </remarks>
        public string AxisText { get; }

        /// <summary>
        /// 获取用于状态提示的信号名称。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 该名称不会直接显示在卡片上，仅用于底部状态信息。
        /// </remarks>
        public string SignalName { get; }

        /// <summary>
        /// 获取原位显示地址。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 页面保留项目地址文本，底层通信时会自动补齐偏移。
        /// </remarks>
        public string HomeAddress { get; }

        /// <summary>
        /// 获取工位显示地址。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 页面保留项目地址文本，底层通信时会自动补齐偏移。
        /// </remarks>
        public string WorkAddress { get; }

        /// <summary>
        /// 获取原位控制命令。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 执行时会向原位点发送一次短脉冲。
        /// </remarks>
        public ICommand GoHomeCommand { get; }

        /// <summary>
        /// 获取工位控制命令。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 执行时会向工位点发送一次短脉冲。
        /// </remarks>
        public ICommand GoWorkCommand { get; }
    }
}
