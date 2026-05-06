using Blood_Alcohol.Communication.Serial;
using Blood_Alcohol.Models;
using Blood_Alcohol.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;

namespace Blood_Alcohol.ViewModels
{
    /// <summary>
    /// 坐标调试页视图模型，负责多组XY/Z坐标配置的加载与保存。
    /// </summary>
    /// By:ChengLei
    /// <remarks>
    /// 由 CoordinateDebugView 创建为 DataContext，内部组合多个坐标配置子模块。
    /// </remarks>
    public class CoordinateDebugViewModel : BaseViewModel, IMonitoringLifecycle, IDisposable
    {
        private const string CoordinateConfigFileName = "CoordinateDebugConfig.json";
        private const string AxisAddressConfigFileName = "AxisDebugAddressConfig.json";
        private static readonly TimeSpan PositionPollInterval = TimeSpan.FromMilliseconds(250);
        private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(2);
        private static readonly string[] OtherPositionNameSuffixes =
        {
            "待机位",
            "摇匀_采血管位置",
            "摇匀_顶空位置",
            "顶空盖子暂放台_盖子1位置",
            "顶空盖子暂放台_盖子2位置",
            "扫码_采血管位置",
            "顶空合盖_顶空瓶位置",
            "采血管开合盖_采血管位置",
            "天平_顶空瓶1位置",
            "天平_顶空瓶2位置",
            "天平_采血管位置",
            "枪头_丢弃位置",
            "顶空进样器_放料位1",
            "顶空进样器_放料位2"
        };

        private readonly ConfigService<CoordinateDebugConfig> _configService;
        private readonly ConfigService<AxisDebugAddressConfig> _axisAddressConfigService;
        private readonly AxisDebugAddressConfig _axisAddressConfig;
        private string _statusMessage = "坐标调试已加载。";
        private string _realTimeX = "--";
        private string _realTimeY = "--";
        private string _realTimeZ = "--";
        private string _realTimeXRaw = "PLC原值";
        private string _realTimeYRaw = "PLC原值";
        private string _realTimeZRaw = "PLC原值";
        private string _realTimeStatus = "等待实时坐标刷新...";
        private int? _realTimeXRawValue;
        private int? _realTimeYRawValue;
        private int? _realTimeZRawValue;
        private CancellationTokenSource? _positionPollCts;
        private Task? _positionPollTask;
        private bool _isMonitoring;
        private bool _disposed;

        public CoordinateProfileViewModel BloodTubeProfile { get; }
        public CoordinateProfileViewModel HeadspaceVialProfile { get; }
        public CoordinateProfileViewModel OtherPositionProfile { get; }
        public CoordinateProfileViewModel PipetteTipProfile { get; }
        public ZCoordinateProfileViewModel ZAxisProfile { get; }

        public ICommand SaveConfigCommand { get; }
        public ICommand ReloadConfigCommand { get; }

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
        /// 获取实时X轴坐标显示文本。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由页面顶部实时坐标区域绑定，用于人工标定时与点位表对比。
        /// </remarks>
        public string RealTimeX
        {
            get => _realTimeX;
            private set
            {
                if (_realTimeX != value)
                {
                    _realTimeX = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 获取实时Y轴坐标显示文本。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由页面顶部实时坐标区域绑定，用于人工标定时与点位表对比。
        /// </remarks>
        public string RealTimeY
        {
            get => _realTimeY;
            private set
            {
                if (_realTimeY != value)
                {
                    _realTimeY = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 获取实时Z轴坐标显示文本。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由页面顶部实时坐标区域绑定，用于人工标定时与点位表对比。
        /// </remarks>
        public string RealTimeZ
        {
            get => _realTimeZ;
            private set
            {
                if (_realTimeZ != value)
                {
                    _realTimeZ = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 获取实时X轴取值来源显示文本。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由页面顶部实时坐标区域绑定，提示当前显示值直接来自PLC。
        /// </remarks>
        public string RealTimeXRaw
        {
            get => _realTimeXRaw;
            private set
            {
                if (_realTimeXRaw != value)
                {
                    _realTimeXRaw = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 获取实时Y轴取值来源显示文本。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由页面顶部实时坐标区域绑定，提示当前显示值直接来自PLC。
        /// </remarks>
        public string RealTimeYRaw
        {
            get => _realTimeYRaw;
            private set
            {
                if (_realTimeYRaw != value)
                {
                    _realTimeYRaw = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 获取实时Z轴取值来源显示文本。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由页面顶部实时坐标区域绑定，提示当前显示值直接来自PLC。
        /// </remarks>
        public string RealTimeZRaw
        {
            get => _realTimeZRaw;
            private set
            {
                if (_realTimeZRaw != value)
                {
                    _realTimeZRaw = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 获取实时坐标轮询状态文本。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由页面顶部实时坐标区域绑定，用于显示PLC连接和刷新异常。
        /// </remarks>
        public string RealTimeStatus
        {
            get => _realTimeStatus;
            private set
            {
                if (_realTimeStatus != value)
                {
                    _realTimeStatus = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 初始化坐标调试视图模型并创建各坐标配置模块。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由页面初始化调用；构造完成后会立即执行 LoadConfig 加载本地配置。
        /// </remarks>
        public CoordinateDebugViewModel()
        {
            _configService = new ConfigService<CoordinateDebugConfig>(CoordinateConfigFileName);
            _axisAddressConfigService = new ConfigService<AxisDebugAddressConfig>(AxisAddressConfigFileName);
            _axisAddressConfig = LoadAxisAddressConfig();

            BloodTubeProfile = new CoordinateProfileViewModel(
                "采血管XY",
                CommunicationManager.Plc,
                CommunicationManager.PlcAccessLock,
                msg => StatusMessage = $"{DateTime.Now:HH:mm:ss} {msg}",
                index => $"M1XP{index}采血管料盘NO{index}",
                CreateLivePositionSnapshot);

            HeadspaceVialProfile = new CoordinateProfileViewModel(
                "顶空瓶XY",
                CommunicationManager.Plc,
                CommunicationManager.PlcAccessLock,
                msg => StatusMessage = $"{DateTime.Now:HH:mm:ss} {msg}",
                index => $"M1XP{100 + index}顶空瓶料盘NO{index}",
                CreateLivePositionSnapshot);

            OtherPositionProfile = new CoordinateProfileViewModel(
                "其他工位XY",
                CommunicationManager.Plc,
                CommunicationManager.PlcAccessLock,
                msg => StatusMessage = $"{DateTime.Now:HH:mm:ss} {msg}",
                BuildOtherPositionDescription,
                CreateLivePositionSnapshot);

            PipetteTipProfile = new CoordinateProfileViewModel(
                "枪头XY",
                CommunicationManager.Plc,
                CommunicationManager.PlcAccessLock,
                msg => StatusMessage = $"{DateTime.Now:HH:mm:ss} {msg}",
                BuildPipetteTipDescription,
                CreateLivePositionSnapshot);

            ZAxisProfile = new ZCoordinateProfileViewModel(
                "Z轴坐标",
                CommunicationManager.Plc,
                CommunicationManager.PlcAccessLock,
                msg => StatusMessage = $"{DateTime.Now:HH:mm:ss} {msg}",
                CreateLivePositionSnapshot);

            SaveConfigCommand = new RelayCommand(_ => SaveConfig());
            ReloadConfigCommand = new RelayCommand(_ => LoadConfig());

            LoadConfig();
        }

        /// <summary>
        /// 加载轴地址映射配置并在异常时回退默认配置。
        /// </summary>
        /// By:ChengLei
        /// <returns>返回用于读取实时XYZ坐标的轴地址配置。</returns>
        /// <remarks>
        /// 由构造函数调用，与轴调试页共用同一份地址配置文件。
        /// </remarks>
        private AxisDebugAddressConfig LoadAxisAddressConfig()
        {
            try
            {
                return _axisAddressConfigService.Load() ?? new AxisDebugAddressConfig();
            }
            catch
            {
                return new AxisDebugAddressConfig();
            }
        }

        /// <summary>
        /// 创建当前实时坐标快照。
        /// </summary>
        /// By:ChengLei
        /// <returns>返回包含XYZ原始坐标的快照对象。</returns>
        /// <remarks>
        /// 由各坐标页签的一键采集命令调用，避免人工抄写实时值。
        /// </remarks>
        private LiveAxisPositionSnapshot CreateLivePositionSnapshot()
        {
            return new LiveAxisPositionSnapshot(_realTimeXRawValue, _realTimeYRawValue, _realTimeZRawValue);
        }

        /// <summary>
        /// 激活实时XYZ坐标轮询。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由 CoordinateDebugView 页面加载时调用，重复调用不会创建多个轮询任务。
        /// </remarks>
        public void ActivateMonitoring()
        {
            if (_disposed || _isMonitoring)
            {
                return;
            }

            _positionPollCts = new CancellationTokenSource();
            _positionPollTask = Task.Run(() => PollLivePositionsAsync(_positionPollCts.Token));
            _isMonitoring = true;
        }

        /// <summary>
        /// 停用实时XYZ坐标轮询。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由 CoordinateDebugView 页面卸载时调用，仅停止后台轮询。
        /// </remarks>
        public void DeactivateMonitoring()
        {
            _ = BeginStopPositionPollingAsync();
        }

        /// <summary>
        /// 释放实时坐标轮询资源。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由宿主窗口关闭时调用，确保后台任务停止。
        /// </remarks>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            BeginStopPositionPollingAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// 后台轮询实时XYZ坐标并刷新界面显示。
        /// </summary>
        /// By:ChengLei
        /// <param name="token">取消令牌，用于终止轮询任务。</param>
        /// <returns>返回实时坐标轮询任务。</returns>
        /// <remarks>
        /// 由 ActivateMonitoring 启动，读取轴调试地址配置中的当前坐标寄存器。
        /// </remarks>
        private async Task PollLivePositionsAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (!CommunicationManager.Is485Open)
                    {
                        RunOnUiThread(() => RealTimeStatus = $"{DateTime.Now:HH:mm:ss} PLC未连接，实时坐标等待中...");
                        await Task.Delay(PositionPollInterval, token);
                        continue;
                    }

                    int xRaw = await ReadAxisPositionRawAsync(_axisAddressConfig.Axis1, token);
                    int yRaw = await ReadAxisPositionRawAsync(_axisAddressConfig.Axis2, token);
                    int zRaw = await ReadAxisPositionRawAsync(_axisAddressConfig.Axis3, token);

                    RunOnUiThread(() =>
                    {
                        _realTimeXRawValue = xRaw;
                        _realTimeYRawValue = yRaw;
                        _realTimeZRawValue = zRaw;
                        RealTimeX = FormatPlcCoordinate(xRaw);
                        RealTimeY = FormatPlcCoordinate(yRaw);
                        RealTimeZ = FormatPlcCoordinate(zRaw);
                        RealTimeXRaw = "PLC原值";
                        RealTimeYRaw = "PLC原值";
                        RealTimeZRaw = "PLC原值";
                        RealTimeStatus = $"{DateTime.Now:HH:mm:ss} 实时坐标已刷新";
                    });
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    RunOnUiThread(() => RealTimeStatus = $"{DateTime.Now:HH:mm:ss} 实时坐标读取失败: {ex.Message}");
                }

                await Task.Delay(PositionPollInterval, token);
            }
        }

        /// <summary>
        /// 读取单轴当前位置原始值。
        /// </summary>
        /// By:ChengLei
        /// <param name="profile">轴地址映射配置。</param>
        /// <param name="token">取消令牌，用于终止读取。</param>
        /// <returns>返回PLC中高低位组合后的32位原始坐标。</returns>
        /// <remarks>
        /// 由实时坐标轮询任务调用，读取当前坐标低16位和高16位寄存器。
        /// </remarks>
        private static async Task<int> ReadAxisPositionRawAsync(AxisAddressProfile profile, CancellationToken token)
        {
            if (profile == null)
            {
                return 0;
            }

            ushort[] registers = await ReadRegistersWithLockAsync(profile.CurrentPositionLowRegister, 2, token);
            if (registers.Length < 2)
            {
                throw new InvalidOperationException($"读取D{profile.CurrentPositionLowRegister}实时坐标失败，返回长度不足2。");
            }

            return ComposeInt32(registers[0], registers[1]);
        }

        /// <summary>
        /// 在PLC访问锁保护下读取寄存器。
        /// </summary>
        /// By:ChengLei
        /// <param name="address">起始D寄存器地址。</param>
        /// <param name="length">读取寄存器数量。</param>
        /// <param name="token">取消令牌，用于终止读取。</param>
        /// <returns>返回读取到的寄存器数组。</returns>
        /// <remarks>
        /// 由实时坐标轮询任务调用，避免与其他PLC操作并发冲突。
        /// </remarks>
        private static async Task<ushort[]> ReadRegistersWithLockAsync(ushort address, ushort length, CancellationToken token)
        {
            await CommunicationManager.PlcAccessLock.WaitAsync(token);
            try
            {
                var read = await CommunicationManager.Plc.TryReadHoldingRegistersAsync(address, length);
                if (!read.Success)
                {
                    throw new InvalidOperationException(read.Error);
                }

                return read.Values;
            }
            finally
            {
                CommunicationManager.PlcAccessLock.Release();
            }
        }

        /// <summary>
        /// 将实时坐标原始值格式化为PLC坐标显示。
        /// </summary>
        /// By:ChengLei
        /// <param name="raw">PLC原始坐标值。</param>
        /// <returns>返回未换算的PLC坐标文本。</returns>
        /// <remarks>
        /// 由实时坐标区域显示调用，不做比例换算。
        /// </remarks>
        private static string FormatPlcCoordinate(int raw)
        {
            return raw.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 将高低位寄存器合成为Int32。
        /// </summary>
        /// By:ChengLei
        /// <param name="lowWord">低16位寄存器值。</param>
        /// <param name="highWord">高16位寄存器值。</param>
        /// <returns>返回组合后的32位整数。</returns>
        /// <remarks>
        /// 由实时坐标轮询任务组合当前位置时调用。
        /// </remarks>
        private static int ComposeInt32(ushort lowWord, ushort highWord)
        {
            uint raw = ((uint)highWord << 16) | lowWord;
            return unchecked((int)raw);
        }

        /// <summary>
        /// 异步停止实时坐标轮询并等待后台任务退出。
        /// </summary>
        /// By:ChengLei
        /// <returns>返回停止轮询任务。</returns>
        /// <remarks>
        /// 由 DeactivateMonitoring 和 Dispose 调用。
        /// </remarks>
        private Task BeginStopPositionPollingAsync()
        {
            CancellationTokenSource? cts = _positionPollCts;
            Task? pollTask = _positionPollTask;
            _positionPollTask = null;
            _positionPollCts = null;
            _isMonitoring = false;

            if (cts == null && pollTask == null)
            {
                return Task.CompletedTask;
            }

            cts?.Cancel();
            return FinishStopPositionPollingAsync(cts, pollTask);
        }

        /// <summary>
        /// 异步等待实时坐标轮询任务退出并释放取消资源。
        /// </summary>
        /// By:ChengLei
        /// <param name="cts">需要释放的取消令牌源。</param>
        /// <param name="pollTask">需要等待结束的后台轮询任务。</param>
        /// <returns>返回异步收尾任务。</returns>
        /// <remarks>
        /// 由 BeginStopPositionPollingAsync 调用，取消轮询后最多等待限定时间。
        /// </remarks>
        private async Task FinishStopPositionPollingAsync(CancellationTokenSource? cts, Task? pollTask)
        {
            if (pollTask == null)
            {
                cts?.Dispose();
                return;
            }

            if (!pollTask.IsCompleted)
            {
                try
                {
                    await pollTask.WaitAsync(StopTimeout).ConfigureAwait(false);
                }
                catch (TimeoutException)
                {
                    RunOnUiThread(() => RealTimeStatus = $"{DateTime.Now:HH:mm:ss} 实时坐标监控停止超时。");
                }
                catch (OperationCanceledException)
                {
                }
            }

            cts?.Dispose();
        }

        /// <summary>
        /// 在UI线程执行指定操作。
        /// </summary>
        /// By:ChengLei
        /// <param name="action">需要执行的UI更新操作。</param>
        /// <remarks>
        /// 由实时坐标轮询任务刷新绑定属性时调用。
        /// </remarks>
        private static void RunOnUiThread(Action action)
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                action();
                return;
            }

            _ = dispatcher.BeginInvoke(action, DispatcherPriority.Send);
        }

        /// <summary>
        /// 从配置文件读取坐标参数并下发到各子模块。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由构造函数和“重新加载配置”按钮调用，用于恢复已保存的坐标调试参数。
        /// </remarks>
        private void LoadConfig()
        {
            try
            {
                CoordinateDebugConfig config = _configService.Load() ?? new CoordinateDebugConfig();

                BloodTubeProfile.ApplySettings(config.BloodTube);
                HeadspaceVialProfile.ApplySettings(config.HeadspaceVial);
                OtherPositionProfile.ApplySettings(config.OtherPosition);
                PipetteTipProfile.ApplySettings(config.PipetteTip);
                ZAxisProfile.ApplySettings(config.ZAxis);

                StatusMessage = $"{DateTime.Now:HH:mm:ss} 坐标配置已加载。";
            }
            catch (Exception ex)
            {
                StatusMessage = $"{DateTime.Now:HH:mm:ss} 坐标配置加载失败: {ex.Message}";
            }
        }

        /// <summary>
        /// 汇总当前坐标参数并保存到配置文件。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由“保存配置”按钮调用，包含采血管、顶空瓶、其他工位、枪头和Z轴配置。
        /// </remarks>
        private void SaveConfig()
        {
            try
            {
                CoordinateDebugConfig config = new()
                {
                    BloodTube = BloodTubeProfile.ExportSettings(),
                    HeadspaceVial = HeadspaceVialProfile.ExportSettings(),
                    OtherPosition = OtherPositionProfile.ExportSettings(),
                    PipetteTip = PipetteTipProfile.ExportSettings(),
                    ZAxis = ZAxisProfile.ExportSettings()
                };

                _configService.Save(config);
                StatusMessage = $"{DateTime.Now:HH:mm:ss} 坐标配置已保存。";
            }
            catch (Exception ex)
            {
                StatusMessage = $"{DateTime.Now:HH:mm:ss} 坐标配置保存失败: {ex.Message}";
            }
        }

        /// <summary>
        /// 生成“其他工位”点位描述文本。
        /// </summary>
        /// By:ChengLei
        /// <param name="index">点位序号（从1开始）。</param>
        /// <returns>返回用于界面显示的点位描述。</returns>
        /// <remarks>
        /// 由 OtherPositionProfile 的描述工厂委托调用。
        /// </remarks>
        private static string BuildOtherPositionDescription(int index)
        {
            int xp = 300 + index - 1;
            string suffix = index <= OtherPositionNameSuffixes.Length
                ? OtherPositionNameSuffixes[index - 1]
                : "预留";

            return $"M1XP{xp}{suffix}";
        }

        /// <summary>
        /// 生成“枪头位”点位描述文本。
        /// </summary>
        /// By:ChengLei
        /// <param name="index">点位序号（从1开始）。</param>
        /// <returns>返回用于界面显示的枪头点位描述。</returns>
        /// <remarks>
        /// 由 PipetteTipProfile 的描述工厂委托调用。
        /// </remarks>
        private static string BuildPipetteTipDescription(int index)
        {
            int xp = 400 + index - 1;
            if (index == 1)
            {
                return $"M1XP{xp}占空";
            }

            return $"M1XP{xp}枪头NO{index - 1}";
        }
    }

    /// <summary>
    /// 实时轴坐标快照。
    /// </summary>
    /// By:ChengLei
    /// <remarks>
    /// 由坐标调试页实时轮询创建，供各点位页签一键采集当前PLC坐标。
    /// </remarks>
    public sealed class LiveAxisPositionSnapshot
    {
        /// <summary>
        /// 获取空实时坐标快照。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由未提供实时坐标委托的子模块作为默认值使用。
        /// </remarks>
        public static LiveAxisPositionSnapshot Empty { get; } = new(null, null, null);

        /// <summary>
        /// 初始化实时轴坐标快照。
        /// </summary>
        /// By:ChengLei
        /// <param name="x">实时X轴PLC坐标。</param>
        /// <param name="y">实时Y轴PLC坐标。</param>
        /// <param name="z">实时Z轴PLC坐标。</param>
        /// <remarks>
        /// 由 CoordinateDebugViewModel 根据最近一次轮询结果创建。
        /// </remarks>
        public LiveAxisPositionSnapshot(int? x, int? y, int? z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        /// <summary>
        /// 实时X轴PLC坐标。
        /// </summary>
        public int? X { get; }

        /// <summary>
        /// 实时Y轴PLC坐标。
        /// </summary>
        public int? Y { get; }

        /// <summary>
        /// 实时Z轴PLC坐标。
        /// </summary>
        public int? Z { get; }
    }

    /// <summary>
    /// 坐标点表写入地址保护
    /// </summary>
    internal static class CoordinateDebugAddressGuard
    {
        private const int ReservedAxisDebugAddressStart = 1000;
        private const int ReservedAxisDebugAddressEnd = 1399;

        /// <summary>
        /// 校验坐标点表双寄存器地址可以安全访问
        /// </summary>
        /// By:ChengLei
        /// <param name="lowAddress">32位坐标低16位寄存器地址。</param>
        /// <param name="fieldName">当前校验的字段名称。</param>
        /// <remarks>
        /// 坐标值写入会同时占用低位和高位两个D寄存器，因此需要按地址区间判断是否覆盖轴调试寄存器。
        /// </remarks>
        public static void EnsureCoordinatePairAddress(int lowAddress, string fieldName)
        {
            if (lowAddress < 0 || lowAddress > ushort.MaxValue - 1)
            {
                throw new InvalidOperationException($"{fieldName}越界，双寄存器写入要求低位地址范围为 0-65534: {lowAddress}");
            }

            int highAddress = lowAddress + 1;
            if (lowAddress <= ReservedAxisDebugAddressEnd && highAddress >= ReservedAxisDebugAddressStart)
            {
                throw new InvalidOperationException(
                    $"{fieldName} D{lowAddress}/D{highAddress} 属于轴调试保留寄存器区 D{ReservedAxisDebugAddressStart}-D{ReservedAxisDebugAddressEnd}，请将坐标点表地址改到 D5100 以后再写入。");
            }
        }
    }

    public class CoordinateProfileViewModel : BaseViewModel
    {
        private static readonly TimeSpan PlcWriteInterval = TimeSpan.FromMilliseconds(100);

        private readonly Lx5vPlc _plc;
        private readonly SemaphoreSlim _plcLock;
        private readonly Action<string> _statusCallback;
        private readonly Func<int, string> _descriptionFactory;
        private readonly Func<LiveAxisPositionSnapshot> _liveSnapshotFactory;

        private string _name;
        private int _rows;
        private int _columns;
        private int _xStartAddress;
        private int _yStartAddress;
        private int _registerStridePerPoint;
        private int _currentXAddress;
        private int _currentYAddress;
        private double _baseX;
        private double _baseY;
        private double _stepX;
        private double _stepY;
        private double _scale;
        private bool _isBusy;
        private string _profileStatusMessage = "未执行操作。";
        private CoordinatePointItemViewModel? _selectedPoint;

        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged();
                }
            }
        }

        public int Rows
        {
            get => _rows;
            set
            {
                if (_rows != value)
                {
                    _rows = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(TotalPoints));
                }
            }
        }

        public int Columns
        {
            get => _columns;
            set
            {
                if (_columns != value)
                {
                    _columns = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(TotalPoints));
                }
            }
        }

        public int XStartAddress
        {
            get => _xStartAddress;
            set
            {
                if (_xStartAddress != value)
                {
                    _xStartAddress = value;
                    OnPropertyChanged();
                }
            }
        }

        public int YStartAddress
        {
            get => _yStartAddress;
            set
            {
                if (_yStartAddress != value)
                {
                    _yStartAddress = value;
                    OnPropertyChanged();
                }
            }
        }

        public int RegisterStridePerPoint
        {
            get => _registerStridePerPoint;
            set
            {
                if (_registerStridePerPoint != value)
                {
                    _registerStridePerPoint = value;
                    OnPropertyChanged();
                }
            }
        }

        public int CurrentXAddress
        {
            get => _currentXAddress;
            set
            {
                if (_currentXAddress != value)
                {
                    _currentXAddress = value;
                    OnPropertyChanged();
                }
            }
        }

        public int CurrentYAddress
        {
            get => _currentYAddress;
            set
            {
                if (_currentYAddress != value)
                {
                    _currentYAddress = value;
                    OnPropertyChanged();
                }
            }
        }

        public double BaseX
        {
            get => _baseX;
            set
            {
                if (Math.Abs(_baseX - value) > 0.000001d)
                {
                    _baseX = value;
                    OnPropertyChanged();
                }
            }
        }

        public double BaseY
        {
            get => _baseY;
            set
            {
                if (Math.Abs(_baseY - value) > 0.000001d)
                {
                    _baseY = value;
                    OnPropertyChanged();
                }
            }
        }

        public double StepX
        {
            get => _stepX;
            set
            {
                if (Math.Abs(_stepX - value) > 0.000001d)
                {
                    _stepX = value;
                    OnPropertyChanged();
                }
            }
        }

        public double StepY
        {
            get => _stepY;
            set
            {
                if (Math.Abs(_stepY - value) > 0.000001d)
                {
                    _stepY = value;
                    OnPropertyChanged();
                }
            }
        }

        public double Scale
        {
            get => _scale;
            set
            {
                if (Math.Abs(_scale - value) > 0.000001d)
                {
                    _scale = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (_isBusy != value)
                {
                    _isBusy = value;
                    OnPropertyChanged();
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public string ProfileStatusMessage
        {
            get => _profileStatusMessage;
            set
            {
                if (_profileStatusMessage != value)
                {
                    _profileStatusMessage = value;
                    OnPropertyChanged();
                }
            }
        }

        public int TotalPoints => Points.Count;

        public ObservableCollection<CoordinatePointItemViewModel> Points { get; } = new();

        public CoordinatePointItemViewModel? SelectedPoint
        {
            get => _selectedPoint;
            set
            {
                if (!ReferenceEquals(_selectedPoint, value))
                {
                    _selectedPoint = value;
                    OnPropertyChanged();
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public ICommand BuildGridCommand { get; }
        public ICommand GenerateFromBaseCommand { get; }
        public ICommand ReadCurrentToBaseCommand { get; }
        public ICommand ReadPointsFromPlcCommand { get; }
        public ICommand WriteAllPointsToPlcCommand { get; }
        public ICommand WriteSelectedPointToPlcCommand { get; }
        public ICommand SetBaseFromSelectedPointCommand { get; }
        public ICommand CaptureFirstCornerCommand { get; }
        public ICommand CaptureLastCornerAndGenerateCommand { get; }
        public ICommand GenerateFromTwoCornersCommand { get; }
        public ICommand CaptureLiveToSelectedPointCommand { get; }
        public ICommand CaptureLiveAndWriteSelectedPointCommand { get; }

        public CoordinateProfileViewModel(
            string name,
            Lx5vPlc plc,
            SemaphoreSlim plcLock,
            Action<string> statusCallback,
            Func<int, string>? descriptionFactory = null,
            Func<LiveAxisPositionSnapshot>? liveSnapshotFactory = null)
        {
            _name = name;
            _plc = plc;
            _plcLock = plcLock;
            _statusCallback = statusCallback;
            _descriptionFactory = descriptionFactory ?? (index => $"P{index:000}");
            _liveSnapshotFactory = liveSnapshotFactory ?? (() => LiveAxisPositionSnapshot.Empty);

            Rows = 5;
            Columns = 10;
            XStartAddress = 5100;
            YStartAddress = 5200;
            RegisterStridePerPoint = 2;
            CurrentXAddress = 5100;
            CurrentYAddress = 5200;
            BaseX = 0;
            BaseY = 0;
            StepX = 0;
            StepY = 0;
            Scale = 100;

            BuildGridCommand = new RelayCommand(_ => BuildGrid(), _ => !IsBusy);
            GenerateFromBaseCommand = new RelayCommand(_ => GenerateFromBase(), _ => !IsBusy);
            ReadCurrentToBaseCommand = new RelayCommand(_ => _ = ReadCurrentToBaseAsync(), _ => !IsBusy);
            ReadPointsFromPlcCommand = new RelayCommand(_ => _ = ReadPointsFromPlcAsync(), _ => !IsBusy);
            WriteAllPointsToPlcCommand = new RelayCommand(_ => _ = WriteAllPointsToPlcAsync(), _ => !IsBusy && Points.Count > 0);
            WriteSelectedPointToPlcCommand = new RelayCommand(_ => _ = WriteSelectedPointToPlcAsync(), _ => !IsBusy && SelectedPoint != null);
            SetBaseFromSelectedPointCommand = new RelayCommand(_ => SetBaseFromSelectedPoint(), _ => !IsBusy && SelectedPoint != null);
            CaptureFirstCornerCommand = new RelayCommand(_ => CaptureFirstCorner(), _ => !IsBusy && Points.Count > 0);
            CaptureLastCornerAndGenerateCommand = new RelayCommand(_ => CaptureLastCornerAndGenerate(), _ => !IsBusy && Points.Count > 0);
            GenerateFromTwoCornersCommand = new RelayCommand(_ => GenerateFromTwoCorners(), _ => !IsBusy && Points.Count > 1);
            CaptureLiveToSelectedPointCommand = new RelayCommand(_ => CaptureLiveToSelectedPoint(), _ => !IsBusy && SelectedPoint != null);
            CaptureLiveAndWriteSelectedPointCommand = new RelayCommand(_ => _ = CaptureLiveAndWriteSelectedPointAsync(), _ => !IsBusy && SelectedPoint != null);

            BuildGrid();
        }

        public void ApplySettings(CoordinateProfileSettings settings)
        {
            CoordinateProfileSettings safeSettings = settings ?? new CoordinateProfileSettings();
            bool hasNewAddressSettings = safeSettings.XStartAddress > 0 || safeSettings.YStartAddress > 0;

            Rows = Math.Max(1, safeSettings.Rows);
            Columns = Math.Max(1, safeSettings.Columns);
            RegisterStridePerPoint = hasNewAddressSettings
                ? Math.Max(2, safeSettings.RegisterStridePerPoint)
                : 2;

            XStartAddress = hasNewAddressSettings
                ? Math.Max(0, safeSettings.XStartAddress)
                : XStartAddress;
            YStartAddress = hasNewAddressSettings
                ? Math.Max(0, safeSettings.YStartAddress)
                : YStartAddress;

            CurrentXAddress = hasNewAddressSettings
                ? Math.Max(0, safeSettings.CurrentXAddress)
                : CurrentXAddress;
            CurrentYAddress = hasNewAddressSettings
                ? Math.Max(0, safeSettings.CurrentYAddress)
                : CurrentYAddress;
            BaseX = safeSettings.BaseX;
            BaseY = safeSettings.BaseY;
            StepX = safeSettings.StepX;
            StepY = safeSettings.StepY;
            Scale = safeSettings.Scale <= 0 ? 100 : safeSettings.Scale;

            if (safeSettings.Points is { Count: > 0 })
            {
                LoadPointsFromSettings(safeSettings.Points);
                AlignPointAddressesFromProfileSettings();
                ProfileStatusMessage = $"{DateTime.Now:HH:mm:ss} 已从配置加载 {Points.Count} 个独立点位。";
            }
            else
            {
                BuildGrid();
            }
        }

        public CoordinateProfileSettings ExportSettings()
        {
            return new CoordinateProfileSettings
            {
                Rows = Rows,
                Columns = Columns,
                XStartAddress = XStartAddress,
                YStartAddress = YStartAddress,
                RegisterStridePerPoint = RegisterStridePerPoint,
                CurrentXAddress = CurrentXAddress,
                CurrentYAddress = CurrentYAddress,
                BaseX = BaseX,
                BaseY = BaseY,
                StepX = StepX,
                StepY = StepY,
                Scale = Scale,
                Points = Points.Select(point => new CoordinatePointSetting
                {
                    Index = point.Index,
                    Row = point.Row,
                    Column = point.Column,
                    Description = point.Description,
                    XAddress = point.XAddress,
                    YAddress = point.YAddress,
                    X = point.X,
                    Y = point.Y
                }).ToList(),

                // DMSJ：兼容历史字段，避免旧版本读取时出现默认值误判。
                TableStartAddress = XStartAddress
            };
        }

        private async Task ExecuteBusyAsync(Func<Task> action, string successMessage)
        {
            if (IsBusy)
            {
                return;
            }

            IsBusy = true;
            try
            {
                ValidateProfileSettings();
                await action();
                ProfileStatusMessage = $"{DateTime.Now:HH:mm:ss} {successMessage}";
                _statusCallback($"{Name}: {successMessage}");
            }
            catch (Exception ex)
            {
                ProfileStatusMessage = $"{DateTime.Now:HH:mm:ss} 操作失败: {ex.Message}";
                _statusCallback($"{Name}: 操作失败 - {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ValidateProfileSettings()
        {
            if (!CommunicationManager.Is485Open)
            {
                throw new InvalidOperationException("RS485未连接，请先连接通讯。");
            }

            if (Rows <= 0 || Columns <= 0)
            {
                throw new InvalidOperationException("行列数必须大于0。");
            }

            if (RegisterStridePerPoint < 2)
            {
                throw new InvalidOperationException("每点寄存器步长至少为2（每轴占低位/高位两个寄存器）。");
            }

            if (Scale <= 0)
            {
                throw new InvalidOperationException("比例系数必须大于0。");
            }

            if (XStartAddress < 0 || YStartAddress < 0)
            {
                throw new InvalidOperationException("起始地址不能为负数。");
            }
        }

        private static ushort EnsureUShortAddress(int value, string fieldName)
        {
            if (value < 0 || value > ushort.MaxValue)
            {
                throw new InvalidOperationException($"{fieldName}超出PLC地址范围(0-65535): {value}");
            }

            return (ushort)value;
        }

        private void BuildGrid()
        {
            int rows = Math.Max(1, Rows);
            int columns = Math.Max(1, Columns);
            int stride = Math.Max(2, RegisterStridePerPoint);

            Points.Clear();

            int index = 1;
            for (int row = 1; row <= rows; row++)
            {
                for (int col = 1; col <= columns; col++)
                {
                    int offsetIndex = (row - 1) * columns + (col - 1);

                    int xAddress = XStartAddress + offsetIndex * stride;
                    int yAddress = YStartAddress + offsetIndex * stride;

                    Points.Add(new CoordinatePointItemViewModel
                    {
                        Index = index,
                        Row = row,
                        Column = col,
                        Description = _descriptionFactory(index),
                        XAddress = xAddress,
                        YAddress = yAddress
                    });

                    index++;
                }
            }

            OnPropertyChanged(nameof(TotalPoints));
            GenerateFromBase();
            ProfileStatusMessage = $"{DateTime.Now:HH:mm:ss} 已重建点位列表，共 {Points.Count} 点。";
        }

        private void LoadPointsFromSettings(IReadOnlyList<CoordinatePointSetting> pointSettings)
        {
            Points.Clear();

            int fallbackIndex = 1;
            foreach (CoordinatePointSetting item in pointSettings.OrderBy(p => p.Index <= 0 ? int.MaxValue : p.Index))
            {
                int index = item.Index > 0 ? item.Index : fallbackIndex;
                int row = item.Row > 0 ? item.Row : 1;
                int column = item.Column > 0 ? item.Column : index;

                Points.Add(new CoordinatePointItemViewModel
                {
                    Index = index,
                    Row = row,
                    Column = column,
                    Description = string.IsNullOrWhiteSpace(item.Description) ? _descriptionFactory(index) : item.Description,
                    XAddress = item.XAddress,
                    YAddress = item.YAddress,
                    X = item.X,
                    Y = item.Y,
                    IsGenerated = false
                });

                fallbackIndex++;
            }

            if (Points.Count > 0)
            {
                Rows = Math.Max(1, Points.Max(point => point.Row));
                Columns = Math.Max(1, Points.Max(point => point.Column));
            }

            OnPropertyChanged(nameof(TotalPoints));
        }

        /// <summary>
        /// 按当前起始地址和步长重新对齐XY点表地址
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由配置加载后调用，避免历史配置中点位地址仍停留在旧地址而覆盖错误PLC区域。
        /// </remarks>
        private void AlignPointAddressesFromProfileSettings()
        {
            int columns = Math.Max(1, Columns);
            int stride = Math.Max(2, RegisterStridePerPoint);

            foreach (CoordinatePointItemViewModel point in Points)
            {
                int row = Math.Max(1, point.Row);
                int column = Math.Max(1, point.Column);
                int offsetIndex = (row - 1) * columns + (column - 1);
                point.XAddress = XStartAddress + offsetIndex * stride;
                point.YAddress = YStartAddress + offsetIndex * stride;
            }
        }

        private void GenerateFromBase()
        {
            if (Points.Count == 0)
            {
                return;
            }

            foreach (CoordinatePointItemViewModel point in Points)
            {
                point.X = BaseX + (point.Column - 1) * StepX;
                point.Y = BaseY + (point.Row - 1) * StepY;
                point.IsGenerated = true;
            }

            ProfileStatusMessage = $"{DateTime.Now:HH:mm:ss} 已按起点/步距推导 {Points.Count} 个点位。";
        }

        private async Task ReadCurrentToBaseAsync()
        {
            await ExecuteBusyAsync(async () =>
            {
                ushort xAddr = EnsureUShortAddress(CurrentXAddress, "当前X低位地址");
                ushort yAddr = EnsureUShortAddress(CurrentYAddress, "当前Y低位地址");

                BaseX = FromInt32(await ReadInt32AtAddressAsync(xAddr));
                BaseY = FromInt32(await ReadInt32AtAddressAsync(yAddr));

                GenerateFromBase();
            }, "已读取当前坐标并更新起点。");
        }

        private async Task ReadPointsFromPlcAsync()
        {
            await ExecuteBusyAsync(async () =>
            {
                foreach (CoordinatePointItemViewModel point in Points)
                {
                    EnsureAxisAddressRange(point.XAddress, "X轴地址");
                    EnsureAxisAddressRange(point.YAddress, "Y轴地址");

                    ushort xAddr = EnsureUShortAddress(point.XAddress, "X低位地址");
                    ushort yAddr = EnsureUShortAddress(point.YAddress, "Y低位地址");

                    point.X = FromInt32(await ReadInt32AtAddressAsync(xAddr));
                    point.Y = FromInt32(await ReadInt32AtAddressAsync(yAddr));
                    point.IsGenerated = false;
                }
            }, $"已从PLC读取 {Points.Count} 个点位。");
        }

        private async Task WriteAllPointsToPlcAsync()
        {
            await ExecuteBusyAsync(async () =>
            {
                ValidateAllPointAddresses();

                foreach (CoordinatePointItemViewModel point in Points)
                {
                    await WritePointAsync(point);
                }
            }, $"已写入PLC {Points.Count} 个点位。");
        }

        private async Task WriteSelectedPointToPlcAsync()
        {
            if (SelectedPoint == null)
            {
                return;
            }

            await ExecuteBusyAsync(
                async () => await WritePointAsync(SelectedPoint),
                $"已写入选中点 P{SelectedPoint.Index:000}。");
        }

        private void SetBaseFromSelectedPoint()
        {
            if (SelectedPoint == null)
            {
                return;
            }

            BaseX = SelectedPoint.X;
            BaseY = SelectedPoint.Y;

            ProfileStatusMessage = $"{DateTime.Now:HH:mm:ss} 已将 P{SelectedPoint.Index:000} 设为起点。";
            _statusCallback($"{Name}: 已将选中点设为起点。");
        }

        /// <summary>
        /// 采集实时坐标作为料盘第一点
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由两点标定流程的“采集第一点P1”按钮调用，第一点作为整张点表的基准点。
        /// </remarks>
        private void CaptureFirstCorner()
        {
            CoordinatePointItemViewModel? firstPoint = GetFirstPoint();
            if (firstPoint == null || !TryGetLiveXY(out int x, out int y))
            {
                return;
            }

            firstPoint.X = x;
            firstPoint.Y = y;
            firstPoint.IsGenerated = false;
            BaseX = x;
            BaseY = y;
            SelectedPoint = firstPoint;

            ProfileStatusMessage = $"{DateTime.Now:HH:mm:ss} 已采集第一点 P{firstPoint.Index:000}，请移动到 X2Y2 后再采集推导。";
            _statusCallback($"{Name}: 已采集第一点 P{firstPoint.Index:000}。");
        }

        /// <summary>
        /// 采集实时坐标作为料盘X2Y2点并推导全部点
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由两点标定流程的“采集X2Y2并推导”按钮调用，根据第一点和相邻对角点自动计算行列步距。
        /// </remarks>
        private void CaptureLastCornerAndGenerate()
        {
            CoordinatePointItemViewModel? secondPoint = GetSecondDiagonalPoint();
            if (secondPoint == null)
            {
                ProfileStatusMessage = $"{DateTime.Now:HH:mm:ss} 两点标定需要至少2行2列，找不到 X2Y2 点。";
                _statusCallback($"{Name}: 两点标定失败，找不到 X2Y2 点。");
                return;
            }

            if (!TryGetLiveXY(out int x, out int y))
            {
                return;
            }

            secondPoint.X = x;
            secondPoint.Y = y;
            secondPoint.IsGenerated = false;
            SelectedPoint = secondPoint;

            GenerateFromTwoCorners();
        }

        /// <summary>
        /// 根据第一点和X2Y2点推导整张XY点表
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由“两点重新推导”按钮和X2Y2采集后调用，适用于采血管和顶空瓶这种规则矩阵料盘。
        /// </remarks>
        private void GenerateFromTwoCorners()
        {
            CoordinatePointItemViewModel? firstPoint = GetFirstPoint();
            CoordinatePointItemViewModel? secondPoint = GetSecondDiagonalPoint();
            if (firstPoint == null || secondPoint == null)
            {
                ProfileStatusMessage = $"{DateTime.Now:HH:mm:ss} 两点标定需要至少2行2列，找不到 X1Y1 或 X2Y2。";
                _statusCallback($"{Name}: 两点标定失败，找不到 X1Y1 或 X2Y2。");
                return;
            }

            int columnSpan = secondPoint.Column - firstPoint.Column;
            int rowSpan = secondPoint.Row - firstPoint.Row;
            if (columnSpan == 0 || rowSpan == 0)
            {
                ProfileStatusMessage = $"{DateTime.Now:HH:mm:ss} 两点标定需要至少2行2列。";
                _statusCallback($"{Name}: 两点标定失败，行列数不足。");
                return;
            }

            BaseX = firstPoint.X;
            BaseY = firstPoint.Y;
            StepX = (secondPoint.X - firstPoint.X) / columnSpan;
            StepY = (secondPoint.Y - firstPoint.Y) / rowSpan;

            foreach (CoordinatePointItemViewModel point in Points)
            {
                point.X = firstPoint.X + (point.Column - firstPoint.Column) * StepX;
                point.Y = firstPoint.Y + (point.Row - firstPoint.Row) * StepY;
                point.IsGenerated = true;
            }

            firstPoint.IsGenerated = false;
            secondPoint.IsGenerated = false;
            ProfileStatusMessage = $"{DateTime.Now:HH:mm:ss} 已按 P{firstPoint.Index:000} 和 X2Y2(P{secondPoint.Index:000}) 推导 {Points.Count} 个点位。";
            _statusCallback($"{Name}: 已按两点推导全部XY点位。");
        }

        /// <summary>
        /// 获取两点标定使用的第一点
        /// </summary>
        /// By:ChengLei
        /// <returns>返回行列最小的点位，点表为空时返回空。</returns>
        /// <remarks>
        /// 由两点标定流程调用，通常对应料盘左上角P1。
        /// </remarks>
        private CoordinatePointItemViewModel? GetFirstPoint()
        {
            return Points
                .OrderBy(point => point.Row)
                .ThenBy(point => point.Column)
                .FirstOrDefault();
        }

        /// <summary>
        /// 获取两点标定使用的X2Y2点
        /// </summary>
        /// By:ChengLei
        /// <returns>返回第一点右下方相邻对角点，找不到时返回空。</returns>
        /// <remarks>
        /// 由两点标定流程调用，通常对应10乘10料盘中的第2行第2列。
        /// </remarks>
        private CoordinatePointItemViewModel? GetSecondDiagonalPoint()
        {
            CoordinatePointItemViewModel? firstPoint = GetFirstPoint();
            if (firstPoint == null)
            {
                return null;
            }

            return Points.FirstOrDefault(point =>
                point.Row == firstPoint.Row + 1 &&
                point.Column == firstPoint.Column + 1);
        }

        /// <summary>
        /// 读取最近一次实时X/Y坐标
        /// </summary>
        /// By:ChengLei
        /// <param name="x">输出实时X轴PLC坐标。</param>
        /// <param name="y">输出实时Y轴PLC坐标。</param>
        /// <returns>返回实时坐标是否已经刷新。</returns>
        /// <remarks>
        /// 由两点标定和选中点采集调用，坐标值直接使用PLC原值。
        /// </remarks>
        private bool TryGetLiveXY(out int x, out int y)
        {
            LiveAxisPositionSnapshot snapshot = _liveSnapshotFactory();
            if (!snapshot.X.HasValue || !snapshot.Y.HasValue)
            {
                x = 0;
                y = 0;
                ProfileStatusMessage = $"{DateTime.Now:HH:mm:ss} 实时X/Y坐标未刷新，无法采集。";
                _statusCallback($"{Name}: 实时X/Y坐标未刷新，无法采集。");
                return false;
            }

            x = snapshot.X.Value;
            y = snapshot.Y.Value;
            return true;
        }

        /// <summary>
        /// 将实时X/Y坐标采集到当前选中点。
        /// </summary>
        /// By:ChengLei
        /// <returns>返回是否成功采集实时坐标。</returns>
        /// <remarks>
        /// 由“采集当前到选中点”和“采集并写入选中点”命令调用，直接使用PLC读取值不做比例换算。
        /// </remarks>
        private bool CaptureLiveToSelectedPoint()
        {
            if (SelectedPoint == null)
            {
                return false;
            }

            if (!TryGetLiveXY(out int x, out int y))
            {
                return false;
            }

            SelectedPoint.X = x;
            SelectedPoint.Y = y;
            SelectedPoint.IsGenerated = false;
            ProfileStatusMessage = $"{DateTime.Now:HH:mm:ss} 已采集实时X/Y到 P{SelectedPoint.Index:000}。";
            _statusCallback($"{Name}: 已采集实时X/Y到选中点。");
            return true;
        }

        /// <summary>
        /// 将实时X/Y坐标采集到选中点并立即写入PLC点表。
        /// </summary>
        /// By:ChengLei
        /// <returns>返回采集和写入的异步任务。</returns>
        /// <remarks>
        /// 由“采集并写入选中点”按钮调用，用于减少标定步骤。
        /// </remarks>
        private async Task CaptureLiveAndWriteSelectedPointAsync()
        {
            if (SelectedPoint == null || !CaptureLiveToSelectedPoint())
            {
                return;
            }

            await ExecuteBusyAsync(
                async () => await WritePointAsync(SelectedPoint),
                $"已采集并写入选中点 P{SelectedPoint.Index:000}。");
        }

        private async Task WritePointAsync(CoordinatePointItemViewModel point)
        {
            EnsureAxisAddressRange(point.XAddress, "X轴地址");
            EnsureAxisAddressRange(point.YAddress, "Y轴地址");

            ushort xAddr = EnsureUShortAddress(point.XAddress, "X低位地址");
            ushort yAddr = EnsureUShortAddress(point.YAddress, "Y低位地址");

            int xRaw = ToInt32(point.X);
            int yRaw = ToInt32(point.Y);

            await WriteInt32AtAddressAsync(xAddr, xRaw);
            await WriteInt32AtAddressAsync(yAddr, yRaw);
        }

        /// <summary>
        /// 写入前检查整张XY点表的寄存器地址
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由“写入全部点”调用，确保发现危险地址时不会出现部分点位已经写入PLC的情况。
        /// </remarks>
        private void ValidateAllPointAddresses()
        {
            var writtenAddresses = new HashSet<int>();

            foreach (CoordinatePointItemViewModel point in Points)
            {
                EnsureAxisAddressRange(point.XAddress, $"P{point.Index:000} X轴地址");
                EnsureAxisAddressRange(point.YAddress, $"P{point.Index:000} Y轴地址");
                EnsureUniqueWrittenAddress(writtenAddresses, point.XAddress, $"P{point.Index:000} X低位");
                EnsureUniqueWrittenAddress(writtenAddresses, point.XAddress + 1, $"P{point.Index:000} X高位");
                EnsureUniqueWrittenAddress(writtenAddresses, point.YAddress, $"P{point.Index:000} Y低位");
                EnsureUniqueWrittenAddress(writtenAddresses, point.YAddress + 1, $"P{point.Index:000} Y高位");
            }
        }

        /// <summary>
        /// 检查本次批量写入中寄存器地址不会被重复写入
        /// </summary>
        /// By:ChengLei
        /// <param name="writtenAddresses">已经登记的写入地址集合。</param>
        /// <param name="address">当前准备写入的D寄存器地址。</param>
        /// <param name="owner">当前地址对应的点位说明。</param>
        /// <remarks>
        /// 由批量写入预检调用，避免同一张XY点表内X/Y地址范围重叠导致后写值覆盖先写值。
        /// </remarks>
        private static void EnsureUniqueWrittenAddress(HashSet<int> writtenAddresses, int address, string owner)
        {
            if (!writtenAddresses.Add(address))
            {
                throw new InvalidOperationException($"坐标点表地址重复写入 D{address}，冲突位置: {owner}，请调整X/Y起始地址或寄存器步长。");
            }
        }

        private static void EnsureAxisAddressRange(int lowAddress, string fieldName)
        {
            CoordinateDebugAddressGuard.EnsureCoordinatePairAddress(lowAddress, fieldName);
        }

        private double FromInt32(int raw)
        {
            return raw;
        }

        private int ToInt32(double value)
        {
            double scaled = Math.Round(value, MidpointRounding.AwayFromZero);
            if (scaled < int.MinValue || scaled > int.MaxValue)
            {
                string text = value.ToString("F3", CultureInfo.InvariantCulture);
                throw new InvalidOperationException($"坐标值超出32位寄存器范围: {text}");
            }

            return (int)scaled;
        }

        private static int ComposeInt32(ushort lowWord, ushort highWord)
        {
            uint raw = ((uint)highWord << 16) | lowWord;
            return unchecked((int)raw);
        }

        private static void SplitInt32(int value, out ushort lowWord, out ushort highWord)
        {
            unchecked
            {
                uint raw = (uint)value;
                lowWord = (ushort)(raw & 0xFFFF);
                highWord = (ushort)((raw >> 16) & 0xFFFF);
            }
        }

        private async Task<int> ReadInt32AtAddressAsync(ushort lowAddress)
        {
            ushort[] regs = await ReadHoldingRegistersAsync(lowAddress, 2);
            if (regs.Length < 2)
            {
                throw new InvalidOperationException($"读取地址 {lowAddress} 失败，返回长度不足2。");
            }

            return ComposeInt32(regs[0], regs[1]);
        }

        private async Task WriteInt32AtAddressAsync(ushort lowAddress, int value)
        {
            SplitInt32(value, out ushort lowWord, out ushort highWord);

            await _plcLock.WaitAsync();
            try
            {
                var writeLow = await _plc.TryWriteSingleRegisterAsync(lowAddress, lowWord);
                if (!writeLow.Success)
                {
                    throw new InvalidOperationException(writeLow.Error);
                }

                await Task.Delay(PlcWriteInterval);

                var writeHigh = await _plc.TryWriteSingleRegisterAsync((ushort)(lowAddress + 1), highWord);
                if (!writeHigh.Success)
                {
                    throw new InvalidOperationException(writeHigh.Error);
                }

                await Task.Delay(PlcWriteInterval);
            }
            finally
            {
                _plcLock.Release();
            }
        }

        private async Task<ushort[]> ReadHoldingRegistersAsync(ushort address, ushort length)
        {
            await _plcLock.WaitAsync();
            try
            {
                var read = await _plc.TryReadHoldingRegistersAsync(address, length);
                if (!read.Success)
                {
                    throw new InvalidOperationException(read.Error);
                }

                return read.Values;
            }
            finally
            {
                _plcLock.Release();
            }
        }
    }

    public class CoordinatePointItemViewModel : BaseViewModel
    {
        private int _index;
        private int _row;
        private int _column;
        private string _description = string.Empty;
        private int _xAddress;
        private int _yAddress;
        private double _x;
        private double _y;
        private bool _isGenerated;

        public int Index
        {
            get => _index;
            set
            {
                if (_index != value)
                {
                    _index = value;
                    OnPropertyChanged();
                }
            }
        }

        public int Row
        {
            get => _row;
            set
            {
                if (_row != value)
                {
                    _row = value;
                    OnPropertyChanged();
                }
            }
        }

        public int Column
        {
            get => _column;
            set
            {
                if (_column != value)
                {
                    _column = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Description
        {
            get => _description;
            set
            {
                if (_description != value)
                {
                    _description = value;
                    OnPropertyChanged();
                }
            }
        }

        public int XAddress
        {
            get => _xAddress;
            set
            {
                if (_xAddress != value)
                {
                    _xAddress = value;
                    OnPropertyChanged();
                }
            }
        }

        public int YAddress
        {
            get => _yAddress;
            set
            {
                if (_yAddress != value)
                {
                    _yAddress = value;
                    OnPropertyChanged();
                }
            }
        }

        public double X
        {
            get => _x;
            set
            {
                if (Math.Abs(_x - value) > 0.000001d)
                {
                    _x = value;
                    OnPropertyChanged();
                }
            }
        }

        public double Y
        {
            get => _y;
            set
            {
                if (Math.Abs(_y - value) > 0.000001d)
                {
                    _y = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsGenerated
        {
            get => _isGenerated;
            set
            {
                if (_isGenerated != value)
                {
                    _isGenerated = value;
                    OnPropertyChanged();
                }
            }
        }
    }

    public class ZCoordinateProfileViewModel : BaseViewModel
    {
        private static readonly TimeSpan PlcWriteInterval = TimeSpan.FromMilliseconds(100);
        private static readonly string[] DefaultZDescriptions =
        {
            "待机位置",
            "占空",
            "摇匀_采血管取放位置",
            "摇匀_顶空瓶位置",
            "采血管料盘_采血管取放位置",
            "顶空瓶料盘_盖子取放位置",
            "顶空瓶料盘_顶空瓶取放位置",
            "顶空瓶盖子暂放台_盖子取放位置",
            "扫码_采血管取放位置",
            "顶空瓶合盖_顶空瓶位置",
            "采血管开合盖_采血管位置",
            "天平_顶空瓶取放位置",
            "天平_采血管取放位置",
            "枪头_取料位置",
            "枪头_丢弃位置",
            "天平_顶空打液位置",
            "顶空进样器_取放料位置",
            "顶空瓶合盖_顶空盖子放置位置"
        };

        private readonly Lx5vPlc _plc;
        private readonly SemaphoreSlim _plcLock;
        private readonly Action<string> _statusCallback;
        private readonly Func<LiveAxisPositionSnapshot> _liveSnapshotFactory;

        private string _name;
        private int _pointCount;
        private int _zStartAddress;
        private int _registerStridePerPoint;
        private int _currentZAddress;
        private double _baseZ;
        private double _stepZ;
        private double _scale;
        private bool _isBusy;
        private string _profileStatusMessage = "未执行操作。";
        private ZCoordinatePointItemViewModel? _selectedPoint;

        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged();
                }
            }
        }

        public int PointCount
        {
            get => _pointCount;
            set
            {
                if (_pointCount != value)
                {
                    _pointCount = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(TotalPoints));
                }
            }
        }

        public int ZStartAddress
        {
            get => _zStartAddress;
            set
            {
                if (_zStartAddress != value)
                {
                    _zStartAddress = value;
                    OnPropertyChanged();
                }
            }
        }

        public int RegisterStridePerPoint
        {
            get => _registerStridePerPoint;
            set
            {
                if (_registerStridePerPoint != value)
                {
                    _registerStridePerPoint = value;
                    OnPropertyChanged();
                }
            }
        }

        public int CurrentZAddress
        {
            get => _currentZAddress;
            set
            {
                if (_currentZAddress != value)
                {
                    _currentZAddress = value;
                    OnPropertyChanged();
                }
            }
        }

        public double BaseZ
        {
            get => _baseZ;
            set
            {
                if (Math.Abs(_baseZ - value) > 0.000001d)
                {
                    _baseZ = value;
                    OnPropertyChanged();
                }
            }
        }

        public double StepZ
        {
            get => _stepZ;
            set
            {
                if (Math.Abs(_stepZ - value) > 0.000001d)
                {
                    _stepZ = value;
                    OnPropertyChanged();
                }
            }
        }

        public double Scale
        {
            get => _scale;
            set
            {
                if (Math.Abs(_scale - value) > 0.000001d)
                {
                    _scale = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (_isBusy != value)
                {
                    _isBusy = value;
                    OnPropertyChanged();
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public string ProfileStatusMessage
        {
            get => _profileStatusMessage;
            set
            {
                if (_profileStatusMessage != value)
                {
                    _profileStatusMessage = value;
                    OnPropertyChanged();
                }
            }
        }

        public int TotalPoints => Points.Count;

        public ObservableCollection<ZCoordinatePointItemViewModel> Points { get; } = new();

        public ZCoordinatePointItemViewModel? SelectedPoint
        {
            get => _selectedPoint;
            set
            {
                if (!ReferenceEquals(_selectedPoint, value))
                {
                    _selectedPoint = value;
                    OnPropertyChanged();
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public ICommand BuildGridCommand { get; }
        public ICommand ReadCurrentToBaseCommand { get; }
        public ICommand ReadPointsFromPlcCommand { get; }
        public ICommand WriteAllPointsToPlcCommand { get; }
        public ICommand WriteSelectedPointToPlcCommand { get; }
        public ICommand SetBaseFromSelectedPointCommand { get; }
        public ICommand CaptureLiveToSelectedPointCommand { get; }
        public ICommand CaptureLiveAndWriteSelectedPointCommand { get; }

        public ZCoordinateProfileViewModel(
            string name,
            Lx5vPlc plc,
            SemaphoreSlim plcLock,
            Action<string> statusCallback,
            Func<LiveAxisPositionSnapshot>? liveSnapshotFactory = null)
        {
            _name = name;
            _plc = plc;
            _plcLock = plcLock;
            _statusCallback = statusCallback;
            _liveSnapshotFactory = liveSnapshotFactory ?? (() => LiveAxisPositionSnapshot.Empty);

            PointCount = 18;
            ZStartAddress = 5900;
            RegisterStridePerPoint = 2;
            CurrentZAddress = 5900;
            BaseZ = 0;
            StepZ = 0;
            Scale = 100;

            BuildGridCommand = new RelayCommand(_ => BuildGrid(), _ => !IsBusy);
            ReadCurrentToBaseCommand = new RelayCommand(_ => _ = ReadCurrentToBaseAsync(), _ => !IsBusy);
            ReadPointsFromPlcCommand = new RelayCommand(_ => _ = ReadPointsFromPlcAsync(), _ => !IsBusy);
            WriteAllPointsToPlcCommand = new RelayCommand(_ => _ = WriteAllPointsToPlcAsync(), _ => !IsBusy && Points.Count > 0);
            WriteSelectedPointToPlcCommand = new RelayCommand(_ => _ = WriteSelectedPointToPlcAsync(), _ => !IsBusy && SelectedPoint != null);
            SetBaseFromSelectedPointCommand = new RelayCommand(_ => SetBaseFromSelectedPoint(), _ => !IsBusy && SelectedPoint != null);
            CaptureLiveToSelectedPointCommand = new RelayCommand(_ => CaptureLiveToSelectedPoint(), _ => !IsBusy && SelectedPoint != null);
            CaptureLiveAndWriteSelectedPointCommand = new RelayCommand(_ => _ = CaptureLiveAndWriteSelectedPointAsync(), _ => !IsBusy && SelectedPoint != null);

            BuildGrid();
        }

        public void ApplySettings(ZCoordinateProfileSettings settings)
        {
            ZCoordinateProfileSettings safeSettings = settings ?? new ZCoordinateProfileSettings();
            bool hasNewAddressSettings = safeSettings.ZStartAddress > 0;
            int legacyPointCount = Math.Max(0, safeSettings.Rows) * Math.Max(0, safeSettings.Columns);

            PointCount = Math.Max(
                1,
                safeSettings.PointCount > 0
                    ? safeSettings.PointCount
                    : legacyPointCount);
            RegisterStridePerPoint = hasNewAddressSettings
                ? Math.Max(2, safeSettings.RegisterStridePerPoint)
                : 2;
            ZStartAddress = hasNewAddressSettings
                ? Math.Max(0, safeSettings.ZStartAddress)
                : ZStartAddress;
            CurrentZAddress = hasNewAddressSettings
                ? Math.Max(0, safeSettings.CurrentZAddress)
                : CurrentZAddress;
            BaseZ = safeSettings.BaseZ;
            StepZ = safeSettings.StepZ;
            Scale = safeSettings.Scale <= 0 ? 100 : safeSettings.Scale;

            if (safeSettings.Points is { Count: > 0 })
            {
                LoadPointsFromSettings(safeSettings.Points);
                PointCount = Points.Count;
                AlignPointAddressesFromProfileSettings();
                ProfileStatusMessage = $"{DateTime.Now:HH:mm:ss} 已从配置加载 {Points.Count} 个独立点位。";
            }
            else
            {
                BuildGrid();
            }
        }

        public ZCoordinateProfileSettings ExportSettings()
        {
            return new ZCoordinateProfileSettings
            {
                PointCount = PointCount,
                ZStartAddress = ZStartAddress,
                RegisterStridePerPoint = RegisterStridePerPoint,
                CurrentZAddress = CurrentZAddress,
                BaseZ = BaseZ,
                StepZ = StepZ,
                Scale = Scale,
                Points = Points.Select(point => new ZCoordinatePointSetting
                {
                    Index = point.Index,
                    Description = point.Description,
                    ZAddress = point.ZAddress,
                    Z = point.Z
                }).ToList(),

                // DMSJ：兼容历史字段，保留旧版行列语义。
                Rows = 1,
                Columns = PointCount
            };
        }

        private async Task ExecuteBusyAsync(Func<Task> action, string successMessage)
        {
            if (IsBusy)
            {
                return;
            }

            IsBusy = true;
            try
            {
                ValidateProfileSettings();
                await action();
                ProfileStatusMessage = $"{DateTime.Now:HH:mm:ss} {successMessage}";
                _statusCallback($"{Name}: {successMessage}");
            }
            catch (Exception ex)
            {
                ProfileStatusMessage = $"{DateTime.Now:HH:mm:ss} 操作失败: {ex.Message}";
                _statusCallback($"{Name}: 操作失败 - {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ValidateProfileSettings()
        {
            if (!CommunicationManager.Is485Open)
            {
                throw new InvalidOperationException("RS485未连接，请先连接通讯。");
            }

            if (PointCount <= 0)
            {
                throw new InvalidOperationException("点位总数必须大于0。");
            }

            if (RegisterStridePerPoint < 2)
            {
                throw new InvalidOperationException("每点寄存器步长至少为2（每轴占低位/高位两个寄存器）。");
            }

            if (Scale <= 0)
            {
                throw new InvalidOperationException("比例系数必须大于0。");
            }

            if (ZStartAddress < 0)
            {
                throw new InvalidOperationException("起始地址不能为负数。");
            }
        }

        private static ushort EnsureUShortAddress(int value, string fieldName)
        {
            if (value < 0 || value > ushort.MaxValue)
            {
                throw new InvalidOperationException($"{fieldName}超出PLC地址范围(0-65535): {value}");
            }

            return (ushort)value;
        }

        private void BuildGrid()
        {
            int pointCount = Math.Max(1, PointCount);
            int stride = Math.Max(2, RegisterStridePerPoint);

            Points.Clear();

            for (int index = 1; index <= pointCount; index++)
            {
                int offsetIndex = index - 1;
                int zAddress = ZStartAddress + offsetIndex * stride;

                Points.Add(new ZCoordinatePointItemViewModel
                {
                    Index = index,
                    Description = GetDefaultDescription(offsetIndex),
                    ZAddress = zAddress
                });
            }

            OnPropertyChanged(nameof(TotalPoints));
            ProfileStatusMessage = $"{DateTime.Now:HH:mm:ss} 已重建点位列表，共 {Points.Count} 点。";
        }

        private void LoadPointsFromSettings(IReadOnlyList<ZCoordinatePointSetting> pointSettings)
        {
            Points.Clear();

            int fallbackIndex = 1;
            foreach (ZCoordinatePointSetting item in pointSettings.OrderBy(p => p.Index <= 0 ? int.MaxValue : p.Index))
            {
                int index = item.Index > 0 ? item.Index : fallbackIndex;

                Points.Add(new ZCoordinatePointItemViewModel
                {
                    Index = index,
                    Description = string.IsNullOrWhiteSpace(item.Description) ? GetDefaultDescription(index - 1) : item.Description,
                    ZAddress = item.ZAddress,
                    Z = item.Z
                });

                fallbackIndex++;
            }

            OnPropertyChanged(nameof(TotalPoints));
        }

        /// <summary>
        /// 按当前起始地址和步长重新对齐Z点表地址
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由配置加载后调用，避免历史配置中点位地址仍停留在旧地址而覆盖错误PLC区域。
        /// </remarks>
        private void AlignPointAddressesFromProfileSettings()
        {
            int stride = Math.Max(2, RegisterStridePerPoint);

            foreach (ZCoordinatePointItemViewModel point in Points)
            {
                int offsetIndex = Math.Max(0, point.Index - 1);
                point.ZAddress = ZStartAddress + offsetIndex * stride;
            }
        }

        private async Task ReadCurrentToBaseAsync()
        {
            await ExecuteBusyAsync(async () =>
            {
                ushort zAddr = EnsureUShortAddress(CurrentZAddress, "当前Z低位地址");
                BaseZ = FromInt32(await ReadInt32AtAddressAsync(zAddr));
            }, "已读取当前坐标并更新起点。");
        }

        private async Task ReadPointsFromPlcAsync()
        {
            await ExecuteBusyAsync(async () =>
            {
                foreach (ZCoordinatePointItemViewModel point in Points)
                {
                    EnsureAxisAddressRange(point.ZAddress, "Z轴地址");
                    ushort zAddr = EnsureUShortAddress(point.ZAddress, "Z低位地址");
                    point.Z = FromInt32(await ReadInt32AtAddressAsync(zAddr));
                }
            }, $"已从PLC读取 {Points.Count} 个点位。");
        }

        private async Task WriteAllPointsToPlcAsync()
        {
            await ExecuteBusyAsync(async () =>
            {
                ValidateAllPointAddresses();

                foreach (ZCoordinatePointItemViewModel point in Points)
                {
                    await WritePointAsync(point);
                }
            }, $"已写入PLC {Points.Count} 个点位。");
        }

        private async Task WriteSelectedPointToPlcAsync()
        {
            if (SelectedPoint == null)
            {
                return;
            }

            await ExecuteBusyAsync(
                async () => await WritePointAsync(SelectedPoint),
                $"已写入选中点 P{SelectedPoint.Index:000}。");
        }

        private void SetBaseFromSelectedPoint()
        {
            if (SelectedPoint == null)
            {
                return;
            }

            BaseZ = SelectedPoint.Z;
            ProfileStatusMessage = $"{DateTime.Now:HH:mm:ss} 已将 P{SelectedPoint.Index:000} 设为起点。";
            _statusCallback($"{Name}: 已将选中点设为起点。");
        }

        /// <summary>
        /// 将实时Z坐标采集到当前选中点。
        /// </summary>
        /// By:ChengLei
        /// <returns>返回是否成功采集实时坐标。</returns>
        /// <remarks>
        /// 由“采集当前到选中点”和“采集并写入选中点”命令调用，直接使用PLC读取值不做比例换算。
        /// </remarks>
        private bool CaptureLiveToSelectedPoint()
        {
            if (SelectedPoint == null)
            {
                return false;
            }

            LiveAxisPositionSnapshot snapshot = _liveSnapshotFactory();
            if (!snapshot.Z.HasValue)
            {
                ProfileStatusMessage = $"{DateTime.Now:HH:mm:ss} 实时Z坐标未刷新，无法采集。";
                _statusCallback($"{Name}: 实时Z坐标未刷新，无法采集。");
                return false;
            }

            SelectedPoint.Z = snapshot.Z.Value;
            ProfileStatusMessage = $"{DateTime.Now:HH:mm:ss} 已采集实时Z到 P{SelectedPoint.Index:000}。";
            _statusCallback($"{Name}: 已采集实时Z到选中点。");
            return true;
        }

        /// <summary>
        /// 将实时Z坐标采集到选中点并立即写入PLC点表。
        /// </summary>
        /// By:ChengLei
        /// <returns>返回采集和写入的异步任务。</returns>
        /// <remarks>
        /// 由“采集并写入选中点”按钮调用，用于减少标定步骤。
        /// </remarks>
        private async Task CaptureLiveAndWriteSelectedPointAsync()
        {
            if (SelectedPoint == null || !CaptureLiveToSelectedPoint())
            {
                return;
            }

            await ExecuteBusyAsync(
                async () => await WritePointAsync(SelectedPoint),
                $"已采集并写入选中点 P{SelectedPoint.Index:000}。");
        }

        private async Task WritePointAsync(ZCoordinatePointItemViewModel point)
        {
            EnsureAxisAddressRange(point.ZAddress, "Z轴地址");
            ushort zAddr = EnsureUShortAddress(point.ZAddress, "Z低位地址");
            int zRaw = ToInt32(point.Z);
            await WriteInt32AtAddressAsync(zAddr, zRaw);
        }

        /// <summary>
        /// 写入前检查整张Z点表的寄存器地址
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由“写入全部点”调用，确保发现危险地址时不会出现部分点位已经写入PLC的情况。
        /// </remarks>
        private void ValidateAllPointAddresses()
        {
            var writtenAddresses = new HashSet<int>();

            foreach (ZCoordinatePointItemViewModel point in Points)
            {
                EnsureAxisAddressRange(point.ZAddress, $"P{point.Index:000} Z轴地址");
                EnsureUniqueWrittenAddress(writtenAddresses, point.ZAddress, $"P{point.Index:000} Z低位");
                EnsureUniqueWrittenAddress(writtenAddresses, point.ZAddress + 1, $"P{point.Index:000} Z高位");
            }
        }

        /// <summary>
        /// 检查本次批量写入中寄存器地址不会被重复写入
        /// </summary>
        /// By:ChengLei
        /// <param name="writtenAddresses">已经登记的写入地址集合。</param>
        /// <param name="address">当前准备写入的D寄存器地址。</param>
        /// <param name="owner">当前地址对应的点位说明。</param>
        /// <remarks>
        /// 由批量写入预检调用，避免同一张Z点表内地址重复导致后写值覆盖先写值。
        /// </remarks>
        private static void EnsureUniqueWrittenAddress(HashSet<int> writtenAddresses, int address, string owner)
        {
            if (!writtenAddresses.Add(address))
            {
                throw new InvalidOperationException($"坐标点表地址重复写入 D{address}，冲突位置: {owner}，请调整Z起始地址或寄存器步长。");
            }
        }

        private static void EnsureAxisAddressRange(int lowAddress, string fieldName)
        {
            CoordinateDebugAddressGuard.EnsureCoordinatePairAddress(lowAddress, fieldName);
        }

        private static string GetDefaultDescription(int zeroBasedIndex)
        {
            return zeroBasedIndex >= 0 && zeroBasedIndex < DefaultZDescriptions.Length
                ? DefaultZDescriptions[zeroBasedIndex]
                : $"未命名点位{zeroBasedIndex + 1}";
        }

        private double FromInt32(int raw)
        {
            return raw;
        }

        private int ToInt32(double value)
        {
            double scaled = Math.Round(value, MidpointRounding.AwayFromZero);
            if (scaled < int.MinValue || scaled > int.MaxValue)
            {
                string text = value.ToString("F3", CultureInfo.InvariantCulture);
                throw new InvalidOperationException($"坐标值超出32位寄存器范围: {text}");
            }

            return (int)scaled;
        }

        private static int ComposeInt32(ushort lowWord, ushort highWord)
        {
            uint raw = ((uint)highWord << 16) | lowWord;
            return unchecked((int)raw);
        }

        private static void SplitInt32(int value, out ushort lowWord, out ushort highWord)
        {
            unchecked
            {
                uint raw = (uint)value;
                lowWord = (ushort)(raw & 0xFFFF);
                highWord = (ushort)((raw >> 16) & 0xFFFF);
            }
        }

        private async Task<int> ReadInt32AtAddressAsync(ushort lowAddress)
        {
            ushort[] regs = await ReadHoldingRegistersAsync(lowAddress, 2);
            if (regs.Length < 2)
            {
                throw new InvalidOperationException($"读取地址 {lowAddress} 失败，返回长度不足2。");
            }

            return ComposeInt32(regs[0], regs[1]);
        }

        private async Task WriteInt32AtAddressAsync(ushort lowAddress, int value)
        {
            SplitInt32(value, out ushort lowWord, out ushort highWord);

            await _plcLock.WaitAsync();
            try
            {
                var writeLow = await _plc.TryWriteSingleRegisterAsync(lowAddress, lowWord);
                if (!writeLow.Success)
                {
                    throw new InvalidOperationException(writeLow.Error);
                }

                await Task.Delay(PlcWriteInterval);

                var writeHigh = await _plc.TryWriteSingleRegisterAsync((ushort)(lowAddress + 1), highWord);
                if (!writeHigh.Success)
                {
                    throw new InvalidOperationException(writeHigh.Error);
                }

                await Task.Delay(PlcWriteInterval);
            }
            finally
            {
                _plcLock.Release();
            }
        }

        private async Task<ushort[]> ReadHoldingRegistersAsync(ushort address, ushort length)
        {
            await _plcLock.WaitAsync();
            try
            {
                var read = await _plc.TryReadHoldingRegistersAsync(address, length);
                if (!read.Success)
                {
                    throw new InvalidOperationException(read.Error);
                }

                return read.Values;
            }
            finally
            {
                _plcLock.Release();
            }
        }
    }

    public class ZCoordinatePointItemViewModel : BaseViewModel
    {
        private int _index;
        private string _description = string.Empty;
        private int _zAddress;
        private double _z;

        public int Index
        {
            get => _index;
            set
            {
                if (_index != value)
                {
                    _index = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Description
        {
            get => _description;
            set
            {
                if (_description != value)
                {
                    _description = value;
                    OnPropertyChanged();
                }
            }
        }

        public int ZAddress
        {
            get => _zAddress;
            set
            {
                if (_zAddress != value)
                {
                    _zAddress = value;
                    OnPropertyChanged();
                }
            }
        }

        public double Z
        {
            get => _z;
            set
            {
                if (Math.Abs(_z - value) > 0.000001d)
                {
                    _z = value;
                    OnPropertyChanged();
                }
            }
        }

    }

    public class CoordinateDebugConfig
    {
        public CoordinateProfileSettings BloodTube { get; set; } = new CoordinateProfileSettings
        {
            Rows = 5,
            Columns = 10,
            XStartAddress = 5100,
            YStartAddress = 5200,
            RegisterStridePerPoint = 2,
            CurrentXAddress = 5100,
            CurrentYAddress = 5200,
            BaseX = 0,
            BaseY = 0,
            StepX = 0,
            StepY = 0,
            Scale = 100
        };

        public CoordinateProfileSettings HeadspaceVial { get; set; } = new CoordinateProfileSettings
        {
            Rows = 10,
            Columns = 10,
            XStartAddress = 5300,
            YStartAddress = 5500,
            RegisterStridePerPoint = 2,
            CurrentXAddress = 5300,
            CurrentYAddress = 5500,
            BaseX = 0,
            BaseY = 0,
            StepX = 0,
            StepY = 0,
            Scale = 100
        };

        public CoordinateProfileSettings OtherPosition { get; set; } = new CoordinateProfileSettings
        {
            Rows = 5,
            Columns = 10,
            XStartAddress = 5700,
            YStartAddress = 5800,
            RegisterStridePerPoint = 2,
            CurrentXAddress = 5700,
            CurrentYAddress = 5800,
            BaseX = 0,
            BaseY = 0,
            StepX = 0,
            StepY = 0,
            Scale = 100
        };

        public CoordinateProfileSettings PipetteTip { get; set; } = new CoordinateProfileSettings
        {
            Rows = 5,
            Columns = 10,
            XStartAddress = 6100,
            YStartAddress = 6200,
            RegisterStridePerPoint = 2,
            CurrentXAddress = 6100,
            CurrentYAddress = 6200,
            BaseX = 0,
            BaseY = 0,
            StepX = 0,
            StepY = 0,
            Scale = 100
        };

        public ZCoordinateProfileSettings ZAxis { get; set; } = new ZCoordinateProfileSettings
        {
            PointCount = 18,
            ZStartAddress = 5900,
            RegisterStridePerPoint = 2,
            CurrentZAddress = 5900,
            BaseZ = 0,
            StepZ = 0,
            Scale = 100
        };
    }

    public class CoordinateProfileSettings
    {
        public int Rows { get; set; } = 1;
        public int Columns { get; set; } = 1;
        public int XStartAddress { get; set; }
        public int YStartAddress { get; set; }
        public int RegisterStridePerPoint { get; set; } = 2;
        public int CurrentXAddress { get; set; }
        public int CurrentYAddress { get; set; }
        public double BaseX { get; set; }
        public double BaseY { get; set; }
        public double StepX { get; set; }
        public double StepY { get; set; }
        public double Scale { get; set; } = 100;
        public List<CoordinatePointSetting> Points { get; set; } = new();

        // DMSJ：历史字段，保留用于兼容旧版配置文件（已弃用）。
        public int TableStartAddress { get; set; }
        public int CurrentZAddress { get; set; }
        public double BaseZ { get; set; }
        public double StepZ { get; set; }
    }

    public class ZCoordinateProfileSettings
    {
        public int PointCount { get; set; } = 1;
        public int ZStartAddress { get; set; }
        public int RegisterStridePerPoint { get; set; } = 2;
        public int CurrentZAddress { get; set; }
        public double BaseZ { get; set; }
        public double StepZ { get; set; }
        public double Scale { get; set; } = 100;
        public List<ZCoordinatePointSetting> Points { get; set; } = new();

        // DMSJ：历史字段，保留用于兼容旧版配置文件（已弃用）。
        public int Rows { get; set; } = 1;
        public int Columns { get; set; } = 1;
    }

    public class CoordinatePointSetting
    {
        public int Index { get; set; }
        public int Row { get; set; }
        public int Column { get; set; }
        public string Description { get; set; } = string.Empty;
        public int XAddress { get; set; }
        public int YAddress { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
    }

    public class ZCoordinatePointSetting
    {
        public int Index { get; set; }
        public string Description { get; set; } = string.Empty;
        public int ZAddress { get; set; }
        public double Z { get; set; }
    }
}
