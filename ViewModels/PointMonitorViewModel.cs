using Blood_Alcohol.Communication.Serial;
using Blood_Alcohol.Models;
using Blood_Alcohol.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Blood_Alcohol.ViewModels
{
    /// <summary>
    /// 点位监控页面视图模型，负责点位配置、实时读取与写入测试。
    /// </summary>
    /// By:ChengLei
    /// <remarks>
    /// 由 PointMonitorView 创建为 DataContext，运行时维护点位列表与监控后台任务。
    /// </remarks>
    public class PointMonitorViewModel : BaseViewModel, IDisposable
    {
        private const string PointMonitorConfigFileName = "PointMonitorConfig.json";
        private const ushort MaxCoilsPerBatch = 120;
        private const ushort MaxRegistersPerBatch = 120;
        private static readonly TimeSpan MonitorInterval = TimeSpan.FromMilliseconds(500);
        private static readonly TimeSpan CoilCacheMaxAge = TimeSpan.FromMilliseconds(1200);

        private readonly Lx5vPlc _plc;
        private readonly Dispatcher _dispatcher;
        private readonly SemaphoreSlim _plcLock;
        private readonly ConfigService<PointMonitorConfig> _configService;
        private readonly HashSet<ushort> _registeredPollingCoils = new();

        private CancellationTokenSource? _cts;
        private bool _isMonitoring;
        private PlcPoint? _selectedPoint;
        private string _statusMessage = "点位监控已加载。";

        private readonly Brush _connectedOnColor = Brushes.Green;
        private readonly Brush _connectedOffColor = Brushes.Gray;
        private readonly Brush _notConnectedColor = Brushes.Black;

        public ObservableCollection<PlcPoint> Points { get; } = new();

        public PlcPoint? SelectedPoint
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

        public ICommand TogglePointCommand { get; }
        public ICommand TogglePointOffCommand { get; }
        public ICommand AddPointCommand { get; }
        public ICommand DeletePointCommand { get; }
        public ICommand SaveConfigCommand { get; }
        public ICommand ReloadConfigCommand { get; }

        /// <summary>
        /// 初始化点位监控视图模型并装配监控命令。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由 PointMonitorView 创建为 DataContext，构造完成后会加载配置并启动监控。
        /// </remarks>
        public PointMonitorViewModel()
        {
            _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
            _plcLock = CommunicationManager.PlcAccessLock;
            _plc = CommunicationManager.Plc;
            _configService = new ConfigService<PointMonitorConfig>(PointMonitorConfigFileName);

            TogglePointCommand = new RelayCommand(
                execute: parameter =>
                {
                    if (parameter is PlcPoint point)
                    {
                        _ = WritePointAsync(point, true);
                    }
                },
                canExecute: parameter => parameter is PlcPoint);

            TogglePointOffCommand = new RelayCommand(
                execute: parameter =>
                {
                    if (parameter is PlcPoint point)
                    {
                        _ = WritePointAsync(point, false);
                    }
                },
                canExecute: parameter => parameter is PlcPoint);

            AddPointCommand = new RelayCommand(_ => AddPoint());
            DeletePointCommand = new RelayCommand(_ => DeletePoint(), _ => SelectedPoint != null);
            SaveConfigCommand = new RelayCommand(_ => SaveConfig());
            ReloadConfigCommand = new RelayCommand(_ => ReloadConfig());

            ReloadConfig();
            StartMonitoring();
        }

        #region 配置读写

        /// <summary>
        /// 从配置文件读取监控点位并应用到页面。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由构造函数与重载配置按钮调用。
        /// </remarks>
        private void ReloadConfig()
        {
            try
            {
                PointMonitorConfig config = _configService.Load() ?? new PointMonitorConfig();

                if (!config.Points.Any() && (config.LeftPoints.Any() || config.RightPoints.Any()))
                {
                    config.Points = config.LeftPoints.Concat(config.RightPoints).ToList();
                }

                if (!config.Points.Any())
                {
                    config = BuildDefaultConfig();
                    _configService.Save(config);
                }

                ApplyConfig(config);
                SyncPollingPoints();
                StatusMessage = $"{DateTime.Now:HH:mm:ss} 点位配置已加载。";
            }
            catch (Exception ex)
            {
                StatusMessage = $"{DateTime.Now:HH:mm:ss} 点位配置加载失败: {ex.Message}";
            }
        }

        /// <summary>
        /// 保存当前监控点位配置到本地文件。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由保存配置按钮调用。
        /// </remarks>
        private void SaveConfig()
        {
            try
            {
                PointMonitorConfig config = ExportConfig();
                _configService.Save(config);
                StatusMessage = $"{DateTime.Now:HH:mm:ss} 点位配置已保存。";
            }
            catch (Exception ex)
            {
                StatusMessage = $"{DateTime.Now:HH:mm:ss} 点位配置保存失败: {ex.Message}";
            }
        }

        /// <summary>
        /// 将配置对象中的点位列表加载到页面集合。
        /// </summary>
        /// By:ChengLei
        /// <param name="config">点位监控配置对象。</param>
        /// <remarks>
        /// 由 ReloadConfig 调用，将配置映射为 PlcPoint 对象集合。
        /// </remarks>
        private void ApplyConfig(PointMonitorConfig config)
        {
            Points.Clear();

            foreach (PlcPointConfigItem item in config.Points.Where(x => !string.IsNullOrWhiteSpace(x.Address)))
            {
                Points.Add(new PlcPoint
                {
                    Address = item.Address.Trim(),
                    Description = item.Description,
                    RegisterBitWidth = item.RegisterBitWidth == 32 ? 32 : 16,
                    ValueText = "--",
                    StatusColor = _connectedOffColor
                });
            }

            SelectedPoint = Points.FirstOrDefault();
        }

        /// <summary>
        /// 导出当前页面点位为可持久化配置对象。
        /// </summary>
        /// By:ChengLei
        /// <returns>返回当前页面导出的点位配置对象。</returns>
        /// <remarks>
        /// 由 SaveConfig 调用，序列化当前页面配置。
        /// </remarks>
        private PointMonitorConfig ExportConfig()
        {
            return new PointMonitorConfig
            {
                Points = Points
                    .Where(x => !string.IsNullOrWhiteSpace(x.Address))
                    .Select(x => new PlcPointConfigItem
                    {
                        Address = x.Address.Trim(),
                        Description = x.Description?.Trim() ?? string.Empty,
                        RegisterBitWidth = x.RegisterBitWidth == 32 ? 32 : 16
                    })
                    .ToList()
            };
        }

        /// <summary>
        /// 构建默认点位配置模板。
        /// </summary>
        /// By:ChengLei
        /// <returns>返回默认点位配置。</returns>
        /// <remarks>
        /// 由 ReloadConfig 在配置为空时调用。
        /// </remarks>
        private static PointMonitorConfig BuildDefaultConfig()
        {
            PointMonitorConfig config = new();

            for (int i = 0; i < 50; i++)
            {
                config.Points.Add(new PlcPointConfigItem
                {
                    Address = $"M{3000 + i * 2}",
                    Description = $"C{i}XM原位"
                });
                config.Points.Add(new PlcPointConfigItem
                {
                    Address = $"M{3001 + i * 2}",
                    Description = $"C{i}XM工位"
                });
                config.Points.Add(new PlcPointConfigItem
                {
                    Address = $"M{3100 + i * 2}",
                    Description = $"C{i}MY原位"
                });
                config.Points.Add(new PlcPointConfigItem
                {
                    Address = $"M{3101 + i * 2}",
                    Description = $"C{i}MY工位"
                });
            }

            return config;
        }

        #endregion

        #region 点位编辑

        /// <summary>
        /// 新增一个监控点位并设置为当前选中项。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由新增点位按钮调用。
        /// </remarks>
        private void AddPoint()
        {
            PlcPoint point = new()
            {
                Address = BuildSuggestedAddress(Points),
                Description = "新建点位",
                RegisterBitWidth = 16,
                ValueText = "--",
                StatusColor = _connectedOffColor
            };

            Points.Add(point);
            SelectedPoint = point;
            SyncPollingPoints();
            StatusMessage = $"{DateTime.Now:HH:mm:ss} 已新增点位 {point.Address}。";
        }

        /// <summary>
        /// 删除当前选中的监控点位。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由删除点位按钮调用。
        /// </remarks>
        private void DeletePoint()
        {
            if (SelectedPoint == null)
            {
                return;
            }

            string address = SelectedPoint.Address;
            Points.Remove(SelectedPoint);
            SelectedPoint = Points.FirstOrDefault();
            SyncPollingPoints();
            StatusMessage = $"{DateTime.Now:HH:mm:ss} 已删除点位 {address}。";
        }

        /// <summary>
        /// 根据现有点位生成建议的下一个M地址。
        /// </summary>
        /// By:ChengLei
        /// <param name="points">当前点位集合。</param>
        /// <returns>返回建议的新点位地址文本。</returns>
        /// <remarks>
        /// 由 AddPoint 调用，避免新增地址重复。
        /// </remarks>
        private static string BuildSuggestedAddress(IEnumerable<PlcPoint> points)
        {
            int max = points
                .Select(x => TryParseCoilAddress(x.Address))
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .DefaultIfEmpty((ushort)0)
                .Max();

            return $"M{max + 1}";
        }

        #endregion

        #region PLC实时监控

        /// <summary>
        /// 启动后台点位监控循环。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由构造函数调用，也可由外部生命周期控制调用。
        /// </remarks>
        public void StartMonitoring()
        {
            if (_isMonitoring)
            {
                return;
            }

            _isMonitoring = true;
            _cts = new CancellationTokenSource();
            Task.Run(() => MonitorPointsAsync(_cts.Token));
        }

        /// <summary>
        /// 停止后台点位监控循环。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由 Dispose 调用，也可用于手动停止监控。
        /// </remarks>
        public void StopMonitoring()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            _isMonitoring = false;
        }

        /// <summary>
        /// 释放监控资源并注销轮询点位。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由页面销毁流程调用。
        /// </remarks>
        public void Dispose()
        {
            StopMonitoring();
            UnregisterPollingPoints();
        }

        /// <summary>
        /// 循环读取PLC点位并刷新页面显示。
        /// </summary>
        /// By:ChengLei
        /// <param name="token">取消令牌，用于停止异步监控循环。</param>
        /// <returns>返回点位监控异步任务。</returns>
        /// <remarks>
        /// 由 StartMonitoring 通过 Task.Run 启动并持续循环。
        /// </remarks>
        private async Task MonitorPointsAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (IsPlcConnected())
                    {
                        await UpdatePointsCollectionAsync(token);
                    }
                    else
                    {
                        await SetAllPointsColorAsync(_notConnectedColor);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    await SetAllPointsColorAsync(_notConnectedColor);
                }

                try
                {
                    await Task.Delay(MonitorInterval, token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            _isMonitoring = false;
        }

        /// <summary>
        /// 判断当前PLC链路是否在线。
        /// </summary>
        /// By:ChengLei
        /// <returns>返回PLC是否在线。</returns>
        /// <remarks>
        /// 由 MonitorPointsAsync 判断是否执行实时读取。
        /// </remarks>
        private static bool IsPlcConnected() => CommunicationManager.Is485Open;

        /// <summary>
        /// 批量读取点位值并更新点位状态缓存。
        /// </summary>
        /// By:ChengLei
        /// <param name="token">取消令牌，用于停止异步监控循环。</param>
        /// <returns>返回点位集合更新异步任务。</returns>
        /// <remarks>
        /// 由 MonitorPointsAsync 在PLC在线时调用。
        /// </remarks>
        private async Task UpdatePointsCollectionAsync(CancellationToken token)
        {
            List<PlcPoint> snapshot = await _dispatcher.InvokeAsync(() => Points.ToList());
            var statusByPoint = new Dictionary<PlcPoint, Brush>();
            var valueByPoint = new Dictionary<PlcPoint, string>();
            var coilItems = new List<CoilReadItem>();
            var registerItems = new List<RegisterReadItem>();

            foreach (PlcPoint point in snapshot)
            {
                ushort? coilAddress = TryParseCoilAddress(point.Address);
                if (coilAddress.HasValue)
                {
                    coilItems.Add(new CoilReadItem(point, coilAddress.Value));
                    continue;
                }

                ushort? registerAddress = TryParseRegisterAddress(point.Address);
                if (!registerAddress.HasValue)
                {
                    statusByPoint[point] = _notConnectedColor;
                    valueByPoint[point] = "--";
                    continue;
                }

                ushort wordLength = GetRegisterWordLength(point);
                if (wordLength == 2 && registerAddress.Value >= ushort.MaxValue)
                {
                    statusByPoint[point] = _notConnectedColor;
                    valueByPoint[point] = "--";
                    continue;
                }

                registerItems.Add(new RegisterReadItem(point, registerAddress.Value, wordLength));
            }

            var unresolvedCoils = new List<CoilReadItem>();
            foreach (CoilReadItem item in coilItems)
            {
                if (CommunicationManager.PlcPolling.TryGetCoil(item.Address, CoilCacheMaxAge, out PlcPollingService.CoilSnapshot cached))
                {
                    if (cached.Success)
                    {
                        statusByPoint[item.Point] = cached.Value ? _connectedOnColor : _connectedOffColor;
                        valueByPoint[item.Point] = cached.Value ? "1" : "0";
                    }
                    else
                    {
                        statusByPoint[item.Point] = _notConnectedColor;
                        valueByPoint[item.Point] = "--";
                    }
                }
                else
                {
                    unresolvedCoils.Add(item);
                }
            }

            foreach (CoilReadSegment segment in BuildCoilReadSegments(unresolvedCoils))
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    bool[] states = await ReadCoilsRangeAsync(segment.StartAddress, segment.Length, token);
                    foreach (CoilReadItem item in segment.Items)
                    {
                        int offset = item.Address - segment.StartAddress;
                        bool value = offset >= 0 && offset < states.Length && states[offset];
                        statusByPoint[item.Point] = value ? _connectedOnColor : _connectedOffColor;
                        valueByPoint[item.Point] = value ? "1" : "0";
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    foreach (CoilReadItem item in segment.Items)
                    {
                        statusByPoint[item.Point] = _notConnectedColor;
                        valueByPoint[item.Point] = "--";
                    }
                }
            }

            foreach (RegisterReadSegment segment in BuildRegisterReadSegments(registerItems))
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    ushort[] regs = await ReadHoldingRegistersRangeAsync(segment.StartAddress, segment.Length, token);
                    foreach (RegisterReadItem item in segment.Items)
                    {
                        int offset = item.Address - segment.StartAddress;
                        if (offset < 0 || offset >= regs.Length)
                        {
                            statusByPoint[item.Point] = _notConnectedColor;
                            valueByPoint[item.Point] = "--";
                            continue;
                        }

                        if (item.WordLength == 1)
                        {
                            statusByPoint[item.Point] = _connectedOnColor;
                            valueByPoint[item.Point] = regs[offset].ToString();
                            continue;
                        }

                        if (offset + 1 >= regs.Length)
                        {
                            statusByPoint[item.Point] = _notConnectedColor;
                            valueByPoint[item.Point] = "--";
                            continue;
                        }

                        int value32 = ConvertToInt32(regs[offset], regs[offset + 1]);
                        statusByPoint[item.Point] = _connectedOnColor;
                        valueByPoint[item.Point] = value32.ToString();
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    foreach (RegisterReadItem item in segment.Items)
                    {
                        statusByPoint[item.Point] = _notConnectedColor;
                        valueByPoint[item.Point] = "--";
                    }
                }
            }

            await _dispatcher.InvokeAsync(() =>
            {
                foreach (PlcPoint point in snapshot)
                {
                    point.StatusColor = statusByPoint.TryGetValue(point, out Brush? color) ? color : _notConnectedColor;
                    point.ValueText = valueByPoint.TryGetValue(point, out string? value) ? value : "--";
                }
            });
        }

        /// <summary>
        /// 根据点位位宽返回寄存器占用字数。
        /// </summary>
        /// By:ChengLei
        /// <param name="point">目标点位对象。</param>
        /// <returns>返回点位读取所需寄存器字数。</returns>
        /// <remarks>
        /// 由 UpdatePointsCollectionAsync 判断寄存器读取长度时调用。
        /// </remarks>
        private static ushort GetRegisterWordLength(PlcPoint point)
        {
            return point.RegisterBitWidth == 32 ? (ushort)2 : (ushort)1;
        }

        /// <summary>
        /// 将两个16位寄存器组合为32位有符号整数。
        /// </summary>
        /// By:ChengLei
        /// <param name="lowWord">低16位寄存器值。</param>
        /// <param name="highWord">高16位寄存器值。</param>
        /// <returns>返回组合后的32位整数值。</returns>
        /// <remarks>
        /// 由 UpdatePointsCollectionAsync 解析32位寄存器值时调用。
        /// </remarks>
        private static int ConvertToInt32(ushort lowWord, ushort highWord)
        {
            uint raw = (uint)lowWord | ((uint)highWord << 16);
            return unchecked((int)raw);
        }

        /// <summary>
        /// 按最大批量限制构建寄存器分段读取计划。
        /// </summary>
        /// By:ChengLei
        /// <param name="items">待分段的读取项列表。</param>
        /// <returns>返回寄存器读取分段列表。</returns>
        /// <remarks>
        /// 由 UpdatePointsCollectionAsync 构建寄存器分批读取时调用。
        /// </remarks>
        private static List<RegisterReadSegment> BuildRegisterReadSegments(List<RegisterReadItem> items)
        {
            var result = new List<RegisterReadSegment>();
            if (items.Count == 0)
            {
                return result;
            }

            List<RegisterReadItem> ordered = items
                .OrderBy(x => x.Address)
                .ToList();

            RegisterReadSegment? current = null;
            foreach (RegisterReadItem item in ordered)
            {
                int itemEndExclusive = item.Address + item.WordLength;
                if (current == null)
                {
                    current = new RegisterReadSegment(item.Address, item.WordLength);
                    current.Items.Add(item);
                    continue;
                }

                int newLength = Math.Max(current.Length, itemEndExclusive - current.StartAddress);
                if (newLength <= MaxRegistersPerBatch)
                {
                    current.Length = (ushort)newLength;
                    current.Items.Add(item);
                }
                else
                {
                    result.Add(current);
                    current = new RegisterReadSegment(item.Address, item.WordLength);
                    current.Items.Add(item);
                }
            }

            if (current != null)
            {
                result.Add(current);
            }

            return result;
        }

        /// <summary>
        /// 按最大批量限制构建线圈分段读取计划。
        /// </summary>
        /// By:ChengLei
        /// <param name="items">待分段的读取项列表。</param>
        /// <returns>返回线圈读取分段列表。</returns>
        /// <remarks>
        /// 由 UpdatePointsCollectionAsync 构建线圈分批读取时调用。
        /// </remarks>
        private static List<CoilReadSegment> BuildCoilReadSegments(List<CoilReadItem> items)
        {
            var result = new List<CoilReadSegment>();
            if (items.Count == 0)
            {
                return result;
            }

            List<CoilReadItem> ordered = items
                .OrderBy(x => x.Address)
                .ToList();

            CoilReadSegment? current = null;
            foreach (CoilReadItem item in ordered)
            {
                if (current == null)
                {
                    current = new CoilReadSegment(item.Address, 1);
                    current.Items.Add(item);
                    continue;
                }

                int nextLength = item.Address - current.StartAddress + 1;
                if (nextLength <= MaxCoilsPerBatch)
                {
                    current.Length = (ushort)nextLength;
                    current.Items.Add(item);
                }
                else
                {
                    result.Add(current);
                    current = new CoilReadSegment(item.Address, 1);
                    current.Items.Add(item);
                }
            }

            if (current != null)
            {
                result.Add(current);
            }

            return result;
        }

        /// <summary>
        /// 在互斥锁保护下读取一段线圈。
        /// </summary>
        /// By:ChengLei
        /// <param name="address">PLC地址文本或数值。</param>
        /// <param name="length">读取长度。</param>
        /// <param name="token">取消令牌，用于停止异步监控循环。</param>
        /// <returns>返回线圈读取结果数组。</returns>
        /// <remarks>
        /// 由 UpdatePointsCollectionAsync 在回源读取线圈时调用。
        /// </remarks>
        private async Task<bool[]> ReadCoilsRangeAsync(ushort address, ushort length, CancellationToken token)
        {
            await _plcLock.WaitAsync(token);
            try
            {
                var read = await _plc.TryReadCoilsAsync(address, length);
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

        /// <summary>
        /// 在互斥锁保护下读取一段保持寄存器。
        /// </summary>
        /// By:ChengLei
        /// <param name="address">PLC地址文本或数值。</param>
        /// <param name="length">读取长度。</param>
        /// <param name="token">取消令牌，用于停止异步监控循环。</param>
        /// <returns>返回寄存器读取结果数组。</returns>
        /// <remarks>
        /// 由 UpdatePointsCollectionAsync 在读取寄存器时调用。
        /// </remarks>
        private async Task<ushort[]> ReadHoldingRegistersRangeAsync(ushort address, ushort length, CancellationToken token)
        {
            await _plcLock.WaitAsync(token);
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

        /// <summary>
        /// 写入单个可写点位并回填界面状态。
        /// </summary>
        /// By:ChengLei
        /// <param name="point">目标点位对象。</param>
        /// <param name="value">写入线圈的目标值。</param>
        /// <returns>返回点位写入异步任务。</returns>
        /// <remarks>
        /// 由点位置位/复位按钮命令调用。
        /// </remarks>
        private async Task WritePointAsync(PlcPoint point, bool value)
        {
            try
            {
                if (!point.IsWriteSupported)
                {
                    StatusMessage = $"{DateTime.Now:HH:mm:ss} 仅支持写入 M 点位，当前为 {point.Address}。";
                    return;
                }

                ushort address = ParseCoilAddress(point.Address);

                await _plcLock.WaitAsync();
                try
                {
                    var write = await _plc.TryWriteSingleCoilAsync(address, value);
                    if (!write.Success)
                    {
                        throw new InvalidOperationException(write.Error);
                    }
                }
                finally
                {
                    _plcLock.Release();
                }

                await _dispatcher.InvokeAsync(() =>
                {
                    point.StatusColor = value ? _connectedOnColor : _connectedOffColor;
                    point.ValueText = value ? "1" : "0";
                });
            }
            catch (Exception ex)
            {
                await _dispatcher.InvokeAsync(() =>
                {
                    point.StatusColor = _notConnectedColor;
                    point.ValueText = "--";
                });
                StatusMessage = $"{DateTime.Now:HH:mm:ss} 写点位失败: {ex.Message}";
            }
        }

        /// <summary>
        /// 统一设置全部点位颜色与显示值。
        /// </summary>
        /// By:ChengLei
        /// <param name="color">要设置的点位状态颜色。</param>
        /// <returns>返回批量设置颜色异步任务。</returns>
        /// <remarks>
        /// 由监控异常或PLC离线分支调用。
        /// </remarks>
        private async Task SetAllPointsColorAsync(Brush color)
        {
            await _dispatcher.InvokeAsync(() =>
            {
                foreach (PlcPoint point in Points)
                {
                    point.StatusColor = color;
                    point.ValueText = "--";
                }
            });
        }

        /// <summary>
        /// 同步当前点位集合到轮询服务注册表。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由 ReloadConfig、AddPoint、DeletePoint 调用。
        /// </remarks>
        private void SyncPollingPoints()
        {
            HashSet<ushort> desired = Points
                .Select(x => TryParseCoilAddress(x.Address))
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .ToHashSet();

            foreach (ushort address in desired)
            {
                if (_registeredPollingCoils.Add(address))
                {
                    CommunicationManager.PlcPolling.RegisterCoil(address, MonitorInterval);
                }
            }

            ushort[] stale = _registeredPollingCoils.Where(x => !desired.Contains(x)).ToArray();
            foreach (ushort address in stale)
            {
                CommunicationManager.PlcPolling.UnregisterCoil(address);
                _registeredPollingCoils.Remove(address);
            }
        }

        /// <summary>
        /// 注销当前页面注册的全部轮询线圈。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由 Dispose 调用，释放轮询资源。
        /// </remarks>
        private void UnregisterPollingPoints()
        {
            foreach (ushort address in _registeredPollingCoils)
            {
                CommunicationManager.PlcPolling.UnregisterCoil(address);
            }

            _registeredPollingCoils.Clear();
        }

        /// <summary>
        /// 解析并校验M点位地址。
        /// </summary>
        /// By:ChengLei
        /// <param name="address">PLC地址文本或数值。</param>
        /// <returns>返回解析后的线圈地址。</returns>
        /// <remarks>
        /// 由 WritePointAsync 调用，确保写入目标为合法M地址。
        /// </remarks>
        private static ushort ParseCoilAddress(string address)
        {
            ushort? parsed = TryParseCoilAddress(address);
            if (!parsed.HasValue)
            {
                throw new Exception($"无法解析PLC地址: {address}");
            }

            return parsed.Value;
        }

        /// <summary>
        /// 尝试解析M点位地址文本。
        /// </summary>
        /// By:ChengLei
        /// <param name="address">PLC地址文本或数值。</param>
        /// <returns>返回解析到的线圈地址，失败时为空。</returns>
        /// <remarks>
        /// 由读取、分段、建议地址等流程复用。
        /// </remarks>
        private static ushort? TryParseCoilAddress(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                return null;
            }

            string trimmed = address.Trim();
            if (trimmed.StartsWith("M", StringComparison.OrdinalIgnoreCase)
                && ushort.TryParse(trimmed.Substring(1), out ushort addr))
            {
                return addr;
            }

            return null;
        }

        /// <summary>
        /// 尝试解析D寄存器地址文本。
        /// </summary>
        /// By:ChengLei
        /// <param name="address">PLC地址文本或数值。</param>
        /// <returns>返回解析到的寄存器地址，失败时为空。</returns>
        /// <remarks>
        /// 由 UpdatePointsCollectionAsync 识别D寄存器点位时调用。
        /// </remarks>
        private static ushort? TryParseRegisterAddress(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                return null;
            }

            string trimmed = address.Trim();
            if (trimmed.StartsWith("D", StringComparison.OrdinalIgnoreCase)
                && ushort.TryParse(trimmed.Substring(1), out ushort addr))
            {
                return addr;
            }

            return null;
        }

        private sealed class CoilReadItem
        {
            /// <summary>
            /// 初始化线圈读取项。
            /// </summary>
            /// By:ChengLei
            /// <param name="point">目标点位对象。</param>
            /// <param name="address">PLC地址文本或数值。</param>
            /// <remarks>
            /// 由 UpdatePointsCollectionAsync 构建线圈读取队列时调用。
            /// </remarks>
            public CoilReadItem(PlcPoint point, ushort address)
            {
                Point = point;
                Address = address;
            }

            public PlcPoint Point { get; }
            public ushort Address { get; }
        }

        private sealed class CoilReadSegment
        {
            /// <summary>
            /// 初始化线圈批量读取分段。
            /// </summary>
            /// By:ChengLei
            /// <param name="startAddress">分段起始地址。</param>
            /// <param name="length">读取长度。</param>
            /// <remarks>
            /// 由 BuildCoilReadSegments 构建批量读取段时调用。
            /// </remarks>
            public CoilReadSegment(ushort startAddress, ushort length)
            {
                StartAddress = startAddress;
                Length = length;
            }

            public ushort StartAddress { get; }
            public ushort Length { get; set; }
            public List<CoilReadItem> Items { get; } = new();
        }

        private sealed class RegisterReadItem
        {
            /// <summary>
            /// 初始化寄存器读取项。
            /// </summary>
            /// By:ChengLei
            /// <param name="point">目标点位对象。</param>
            /// <param name="address">PLC地址文本或数值。</param>
            /// <param name="wordLength">寄存器占用字数。</param>
            /// <remarks>
            /// 由 UpdatePointsCollectionAsync 构建寄存器读取队列时调用。
            /// </remarks>
            public RegisterReadItem(PlcPoint point, ushort address, ushort wordLength)
            {
                Point = point;
                Address = address;
                WordLength = wordLength;
            }

            public PlcPoint Point { get; }
            public ushort Address { get; }
            public ushort WordLength { get; }
        }

        private sealed class RegisterReadSegment
        {
            /// <summary>
            /// 初始化寄存器批量读取分段。
            /// </summary>
            /// By:ChengLei
            /// <param name="startAddress">分段起始地址。</param>
            /// <param name="length">读取长度。</param>
            /// <remarks>
            /// 由 BuildRegisterReadSegments 构建批量读取段时调用。
            /// </remarks>
            public RegisterReadSegment(ushort startAddress, ushort length)
            {
                StartAddress = startAddress;
                Length = length;
            }

            public ushort StartAddress { get; }
            public ushort Length { get; set; }
            public List<RegisterReadItem> Items { get; } = new();
        }

        #endregion
    }

    /// <summary>
    /// 点位监控配置模型，定义监控点位集合与兼容字段。
    /// </summary>
    /// By:ChengLei
    /// <remarks>
    /// 由 PointMonitorViewModel 读写配置文件时使用。
    /// </remarks>
    public class PointMonitorConfig
    {
        public List<PlcPointConfigItem> Points { get; set; } = new();

        public List<PlcPointConfigItem> LeftPoints { get; set; } = new();
        public List<PlcPointConfigItem> RightPoints { get; set; } = new();
    }

    /// <summary>
    /// 单个点位配置项模型。
    /// </summary>
    /// By:ChengLei
    /// <remarks>
    /// 表示配置文件中的地址、描述与寄存器位宽定义。
    /// </remarks>
    public class PlcPointConfigItem
    {
        public string Address { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int RegisterBitWidth { get; set; } = 16;
    }
}
