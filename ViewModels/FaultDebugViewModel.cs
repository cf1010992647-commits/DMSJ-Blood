using Blood_Alcohol.Communication.Serial;
using Blood_Alcohol.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;

namespace Blood_Alcohol.ViewModels
{
    public class FaultDebugViewModel : BaseViewModel, IDisposable
    {
        private const string FaultDebugConfigFileName = "FaultDebugConfig.json";
        private const ushort MaxCoilsPerBatch = 120;
        private static readonly TimeSpan MonitorInterval = TimeSpan.FromMilliseconds(500);
        private static readonly TimeSpan CoilCacheMaxAge = TimeSpan.FromMilliseconds(1200);

        private readonly Lx5vPlc _plc;
        private readonly SemaphoreSlim _plcLock;
        private readonly Dispatcher _dispatcher;
        private readonly ConfigService<FaultDebugConfig> _configService;
        private readonly HashSet<ushort> _registeredPollingCoils = new();

        private CancellationTokenSource? _cts;
        private bool _isMonitoring;
        private FaultAlarmItemViewModel? _selectedAlarm;
        private bool _showActiveOnly;
        private bool _isGlobalShieldEnabled;
        private int _globalShieldAddress = 12;
        private string _statusMessage = "故障调试已加载，等待 PLC 连接。";

        public ObservableCollection<FaultAlarmItemViewModel> AlarmPoints { get; } = new();
        public ObservableCollection<FaultEventRecordViewModel> AlarmEvents { get; } = new();
        public ICollectionView AlarmPointsView { get; }

        public ICommand ClearFaultCommand { get; }
        public ICommand ToggleGlobalShieldCommand { get; }
        public ICommand ToggleSelectedMaskCommand { get; }
        public ICommand ClearEventLogCommand { get; }
        public ICommand AddAlarmPointCommand { get; }
        public ICommand DeleteSelectedAlarmCommand { get; }
        public ICommand SaveConfigCommand { get; }
        public ICommand ReloadConfigCommand { get; }

        public FaultAlarmItemViewModel? SelectedAlarm
        {
            get => _selectedAlarm;
            set
            {
                if (!ReferenceEquals(_selectedAlarm, value))
                {
                    _selectedAlarm = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ClearFaultButtonText));
                    OnPropertyChanged(nameof(SelectedAlarmMaskButtonText));
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public bool ShowActiveOnly
        {
            get => _showActiveOnly;
            set
            {
                if (_showActiveOnly != value)
                {
                    _showActiveOnly = value;
                    OnPropertyChanged();
                    AlarmPointsView.Refresh();
                }
            }
        }

        public bool IsGlobalShieldEnabled
        {
            get => _isGlobalShieldEnabled;
            private set
            {
                if (_isGlobalShieldEnabled != value)
                {
                    _isGlobalShieldEnabled = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(GlobalShieldButtonText));
                }
            }
        }

        public int GlobalShieldAddress
        {
            get => _globalShieldAddress;
            set
            {
                if (_globalShieldAddress != value)
                {
                    _globalShieldAddress = value;
                    OnPropertyChanged();
                }
            }
        }

        public string GlobalShieldButtonText => IsGlobalShieldEnabled ? "取消全局屏蔽" : "全局屏蔽报警";
        public string ClearFaultButtonText => SelectedAlarm == null ? "清除选中故障" : $"清除 {SelectedAlarm.Address}";
        public string SelectedAlarmMaskButtonText => SelectedAlarm == null
            ? "屏蔽选中报警"
            : (SelectedAlarm.IsMasked ? "解除屏蔽选中" : "屏蔽选中报警");

        public int ActiveAlarmCount => AlarmPoints.Count(x => x.IsActive && !x.IsMasked);
        public int MaskedAlarmCount => AlarmPoints.Count(x => x.IsMasked);

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

        public FaultDebugViewModel()
        {
            _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
            _plc = CommunicationManager.Plc;
            _plcLock = CommunicationManager.PlcAccessLock;
            _configService = new ConfigService<FaultDebugConfig>(FaultDebugConfigFileName);

            AlarmPointsView = CollectionViewSource.GetDefaultView(AlarmPoints);
            AlarmPointsView.Filter = FilterAlarm;

            ClearFaultCommand = new RelayCommand(_ => _ = ClearFaultAsync(), _ => SelectedAlarm != null);
            ToggleGlobalShieldCommand = new RelayCommand(_ => _ = ToggleGlobalShieldAsync());
            ToggleSelectedMaskCommand = new RelayCommand(_ => ToggleSelectedMask(), _ => SelectedAlarm != null);
            ClearEventLogCommand = new RelayCommand(_ => AlarmEvents.Clear());
            AddAlarmPointCommand = new RelayCommand(_ => AddAlarmPoint());
            DeleteSelectedAlarmCommand = new RelayCommand(_ => DeleteSelectedAlarm(), _ => SelectedAlarm != null);
            SaveConfigCommand = new RelayCommand(_ => SaveConfig());
            ReloadConfigCommand = new RelayCommand(_ => ReloadConfig());

            ReloadConfig();
            StartMonitoring();
        }

        #region 配置读写

        private void ReloadConfig()
        {
            try
            {
                FaultDebugConfig config = _configService.Load() ?? new FaultDebugConfig();
                if (!config.AlarmPoints.Any())
                {
                    config = BuildDefaultConfig();
                    _configService.Save(config);
                }

                GlobalShieldAddress = config.GlobalShieldAddress <= 0 ? 12 : config.GlobalShieldAddress;
                ApplyAlarmPoints(config.AlarmPoints);
                StatusMessage = $"{DateTime.Now:HH:mm:ss} 报警配置已加载。";
            }
            catch (Exception ex)
            {
                StatusMessage = $"{DateTime.Now:HH:mm:ss} 报警配置加载失败: {ex.Message}";
            }
        }

        private void SaveConfig()
        {
            try
            {
                FaultDebugConfig config = new()
                {
                    GlobalShieldAddress = GlobalShieldAddress,
                    AlarmPoints = AlarmPoints
                        .Where(x => !string.IsNullOrWhiteSpace(x.Address))
                        .Select(x => new FaultAlarmDefinition
                        {
                            Address = x.Address.Trim(),
                            Description = x.Description?.Trim() ?? string.Empty
                        })
                        .ToList()
                };

                _configService.Save(config);
                StatusMessage = $"{DateTime.Now:HH:mm:ss} 报警配置已保存。";
            }
            catch (Exception ex)
            {
                StatusMessage = $"{DateTime.Now:HH:mm:ss} 报警配置保存失败: {ex.Message}";
            }
        }

        private void ApplyAlarmPoints(IEnumerable<FaultAlarmDefinition> definitions)
        {
            AlarmPoints.Clear();

            foreach (FaultAlarmDefinition definition in definitions.Where(x => !string.IsNullOrWhiteSpace(x.Address)))
            {
                AlarmPoints.Add(new FaultAlarmItemViewModel
                {
                    Address = definition.Address.Trim(),
                    Description = definition.Description?.Trim() ?? string.Empty
                });
            }

            SelectedAlarm = AlarmPoints.FirstOrDefault();
            SyncPollingPoints();
            OnPropertyChanged(nameof(ActiveAlarmCount));
            OnPropertyChanged(nameof(MaskedAlarmCount));
            AlarmPointsView.Refresh();
        }

        private static FaultDebugConfig BuildDefaultConfig()
        {
            FaultDebugConfig config = new()
            {
                GlobalShieldAddress = 12
            };

            for (int i = 0; i < 50; i++)
            {
                config.AlarmPoints.Add(new FaultAlarmDefinition
                {
                    Address = $"M{500 + i}",
                    Description = $"C{i:00}回原位超时报警"
                });
            }

            for (int i = 0; i < 50; i++)
            {
                config.AlarmPoints.Add(new FaultAlarmDefinition
                {
                    Address = $"M{550 + i}",
                    Description = $"C{i:00}回工位超时报警"
                });
            }

            for (int i = 600; i <= 699; i++)
            {
                config.AlarmPoints.Add(new FaultAlarmDefinition
                {
                    Address = $"M{i}",
                    Description = $"其他报警{i}"
                });
            }

            return config;
        }

        #endregion

        #region 报警点位编辑

        private void AddAlarmPoint()
        {
            FaultAlarmItemViewModel alarm = new()
            {
                Address = BuildSuggestedAddress(),
                Description = "新建报警点"
            };

            AlarmPoints.Add(alarm);
            SelectedAlarm = alarm;
            SyncPollingPoints();
            StatusMessage = $"{DateTime.Now:HH:mm:ss} 已新增报警点 {alarm.Address}。";
        }

        private void DeleteSelectedAlarm()
        {
            if (SelectedAlarm == null)
            {
                return;
            }

            string address = SelectedAlarm.Address;
            AlarmPoints.Remove(SelectedAlarm);
            SelectedAlarm = AlarmPoints.FirstOrDefault();
            SyncPollingPoints();
            OnPropertyChanged(nameof(ActiveAlarmCount));
            OnPropertyChanged(nameof(MaskedAlarmCount));
            StatusMessage = $"{DateTime.Now:HH:mm:ss} 已删除报警点 {address}。";
        }

        private string BuildSuggestedAddress()
        {
            int max = AlarmPoints
                .Select(x => TryParsePlcAddress(x.Address))
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .DefaultIfEmpty((ushort)500)
                .Max();

            return $"M{max + 1}";
        }

        #endregion

        #region PLC监控与操作
        private bool FilterAlarm(object obj)
        {
            if (obj is not FaultAlarmItemViewModel alarm)
            {
                return false;
            }

            return !ShowActiveOnly || alarm.IsActive;
        }

        private void StartMonitoring()
        {
            if (_isMonitoring)
            {
                return;
            }

            _isMonitoring = true;
            _cts = new CancellationTokenSource();
            Task.Run(() => MonitorAsyncV2(_cts.Token));
        }

        private void StopMonitoring()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            _isMonitoring = false;
        }

        public void Dispose()
        {
            StopMonitoring();
            UnregisterPollingPoints();
        }

        private async Task MonitorAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (!CommunicationManager.Is485Open)
                    {
                        StatusMessage = $"{DateTime.Now:HH:mm:ss} RS485 未连接，等待重连。";
                        await Task.Delay(500, token);
                        continue;
                    }

                    List<FaultAlarmItemViewModel> snapshot =
                        await _dispatcher.InvokeAsync(() => AlarmPoints.ToList());

                    DateTime now = DateTime.Now;
                    foreach (FaultAlarmItemViewModel alarm in snapshot)
                    {
                        token.ThrowIfCancellationRequested();
                        bool? state = await ReadAlarmStateSafeAsync(alarm, token);
                        if (!state.HasValue)
                        {
                            continue;
                        }

                        await _dispatcher.InvokeAsync(() => ApplyAlarmState(alarm, state.Value, now));
                    }

                    StatusMessage = $"{DateTime.Now:HH:mm:ss} 报警监控中，点位总数 {AlarmPoints.Count}。";
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    StatusMessage = $"{DateTime.Now:HH:mm:ss} 报警监控失败: {ex.Message}";
                }

                try
                {
                    await Task.Delay(250, token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            _isMonitoring = false;
        }

        private async Task<bool?> ReadAlarmStateSafeAsync(FaultAlarmItemViewModel alarm, CancellationToken token)
        {
            ushort? address = TryParsePlcAddress(alarm.Address);
            if (!address.HasValue)
            {
                return null;
            }

            await _plcLock.WaitAsync(token);
            try
            {
                var read = await _plc.TryReadCoilsAsync(address.Value, 1);
                if (!read.Success)
                {
                    throw new InvalidOperationException(read.Error);
                }

                bool[] states = read.Values;
                return states.Length > 0 && states[0];
            }
            finally
            {
                _plcLock.Release();
            }
        }

        private async Task MonitorAsyncV2(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (!CommunicationManager.Is485Open)
                    {
                        SetStatusMessage($"{DateTime.Now:HH:mm:ss} RS485 未连接，等待重连。");
                        await Task.Delay(MonitorInterval, token);
                        continue;
                    }

                    List<FaultAlarmItemViewModel> snapshot =
                        await _dispatcher.InvokeAsync(() => AlarmPoints.ToList());

                    Dictionary<FaultAlarmItemViewModel, bool> states = await ReadAlarmStatesBatchAsync(snapshot, token);
                    DateTime now = DateTime.Now;
                    await _dispatcher.InvokeAsync(() =>
                    {
                        foreach (KeyValuePair<FaultAlarmItemViewModel, bool> pair in states)
                        {
                            ApplyAlarmState(pair.Key, pair.Value, now);
                        }
                    });

                    SetStatusMessage($"{DateTime.Now:HH:mm:ss} 报警监控中，点位总数 {snapshot.Count}。");
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    SetStatusMessage($"{DateTime.Now:HH:mm:ss} 报警监控失败: {ex.Message}");
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

        private async Task<Dictionary<FaultAlarmItemViewModel, bool>> ReadAlarmStatesBatchAsync(
            List<FaultAlarmItemViewModel> snapshot,
            CancellationToken token)
        {
            var result = new Dictionary<FaultAlarmItemViewModel, bool>();
            var validItems = new List<AlarmReadItem>();

            foreach (FaultAlarmItemViewModel alarm in snapshot)
            {
                ushort? address = TryParsePlcAddress(alarm.Address);
                if (!address.HasValue)
                {
                    continue;
                }

                validItems.Add(new AlarmReadItem(alarm, address.Value));
            }

            var unresolvedItems = new List<AlarmReadItem>();
            foreach (AlarmReadItem item in validItems)
            {
                if (CommunicationManager.PlcPolling.TryGetCoil(item.Address, CoilCacheMaxAge, out PlcPollingService.CoilSnapshot cached))
                {
                    if (cached.Success)
                    {
                        result[item.Alarm] = cached.Value;
                    }
                    // DMSJ: cache read failed -> keep previous UI state, avoid frequent false flips.
                }
                else
                {
                    unresolvedItems.Add(item);
                }
            }

            List<AlarmReadSegment> segments = BuildReadSegments(unresolvedItems);
            foreach (AlarmReadSegment segment in segments)
            {
                try
                {
                    token.ThrowIfCancellationRequested();
                    bool[] states = await ReadCoilsRangeAsync(segment.StartAddress, segment.Length, token);

                    foreach (AlarmReadItem item in segment.Items)
                    {
                        int offset = item.Address - segment.StartAddress;
                        bool state = offset >= 0 && offset < states.Length && states[offset];
                        result[item.Alarm] = state;
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    // DMSJ: 单段读取失败时跳过该段，保留上一轮状态，避免误触发抖动。
                }
            }

            return result;
        }

        private static List<AlarmReadSegment> BuildReadSegments(List<AlarmReadItem> items)
        {
            var result = new List<AlarmReadSegment>();
            if (items.Count == 0)
            {
                return result;
            }

            List<AlarmReadItem> ordered = items
                .OrderBy(x => x.Address)
                .ToList();

            AlarmReadSegment? current = null;
            foreach (AlarmReadItem item in ordered)
            {
                if (current == null)
                {
                    current = new AlarmReadSegment(item.Address, 1);
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
                    current = new AlarmReadSegment(item.Address, 1);
                    current.Items.Add(item);
                }
            }

            if (current != null)
            {
                result.Add(current);
            }

            return result;
        }

        private async Task<bool[]> ReadCoilsRangeAsync(ushort startAddress, ushort length, CancellationToken token)
        {
            await _plcLock.WaitAsync(token);
            try
            {
                var read = await _plc.TryReadCoilsAsync(startAddress, length);
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

        private void SetStatusMessage(string message)
        {
            if (_dispatcher.CheckAccess())
            {
                StatusMessage = message;
            }
            else
            {
                _ = _dispatcher.BeginInvoke(() => StatusMessage = message);
            }
        }

        private void SyncPollingPoints()
        {
            HashSet<ushort> desired = AlarmPoints
                .Select(x => TryParsePlcAddress(x.Address))
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

        private void UnregisterPollingPoints()
        {
            foreach (ushort address in _registeredPollingCoils)
            {
                CommunicationManager.PlcPolling.UnregisterCoil(address);
            }

            _registeredPollingCoils.Clear();
        }

        private void ApplyAlarmState(FaultAlarmItemViewModel alarm, bool next, DateTime now)
        {
            if (alarm.IsActive == next)
            {
                return;
            }

            alarm.IsActive = next;
            alarm.LastChangedAt = now;

            if (next)
            {
                alarm.ActiveSince = now;
                alarm.LastAlarmTime = now;
                AppendEvent(alarm, "报警触发");
            }
            else
            {
                alarm.ActiveSince = null;
                AppendEvent(alarm, "报警恢复");
            }

            OnPropertyChanged(nameof(ActiveAlarmCount));
            AlarmPointsView.Refresh();
        }

        private async Task ClearFaultAsync()
        {
            if (SelectedAlarm == null)
            {
                return;
            }

            ushort address = ParsePlcAddress(SelectedAlarm.Address);
            try
            {
                await _plcLock.WaitAsync();
                try
                {
                    var write = await _plc.TryWriteSingleCoilAsync(address, false);
                    if (!write.Success)
                    {
                        throw new InvalidOperationException(write.Error);
                    }
                }
                finally
                {
                    _plcLock.Release();
                }

                StatusMessage = $"{DateTime.Now:HH:mm:ss} 已发送清故障: {SelectedAlarm.Address} -> 0。";
                AppendEvent(SelectedAlarm, "手动清除报警");
            }
            catch (Exception ex)
            {
                StatusMessage = $"{DateTime.Now:HH:mm:ss} 清除报警失败: {ex.Message}";
                AppendEvent(SelectedAlarm.Address, SelectedAlarm.Description, $"清除失败: {ex.Message}");
            }
        }

        private async Task ToggleGlobalShieldAsync()
        {
            bool next = !IsGlobalShieldEnabled;
            ushort address = ParsePlcAddress($"M{GlobalShieldAddress}");

            try
            {
                await _plcLock.WaitAsync();
                try
                {
                    var write = await _plc.TryWriteSingleCoilAsync(address, next);
                    if (!write.Success)
                    {
                        throw new InvalidOperationException(write.Error);
                    }
                }
                finally
                {
                    _plcLock.Release();
                }

                IsGlobalShieldEnabled = next;
                StatusMessage = next
                    ? $"{DateTime.Now:HH:mm:ss} 已开启全局屏蔽(M{GlobalShieldAddress})。"
                    : $"{DateTime.Now:HH:mm:ss} 已关闭全局屏蔽(M{GlobalShieldAddress})。";

                AppendEvent($"M{GlobalShieldAddress}", "报警屏蔽", next ? "开启全局屏蔽" : "关闭全局屏蔽");
            }
            catch (Exception ex)
            {
                StatusMessage = $"{DateTime.Now:HH:mm:ss} 全局屏蔽操作失败: {ex.Message}";
                AppendEvent($"M{GlobalShieldAddress}", "报警屏蔽", $"全局屏蔽操作失败: {ex.Message}");
            }
        }

        private void ToggleSelectedMask()
        {
            if (SelectedAlarm == null)
            {
                return;
            }

            SelectedAlarm.IsMasked = !SelectedAlarm.IsMasked;
            OnPropertyChanged(nameof(SelectedAlarmMaskButtonText));
            OnPropertyChanged(nameof(ActiveAlarmCount));
            OnPropertyChanged(nameof(MaskedAlarmCount));

            AppendEvent(
                SelectedAlarm.Address,
                SelectedAlarm.Description,
                SelectedAlarm.IsMasked ? "本地屏蔽报警" : "取消本地屏蔽");
        }

        #endregion

        #region 事件记录

        private void AppendEvent(FaultAlarmItemViewModel alarm, string action)
        {
            AppendEvent(alarm.Address, alarm.Description, action);
        }

        private void AppendEvent(string address, string description, string action)
        {
            void Add()
            {
                AlarmEvents.Insert(0, new FaultEventRecordViewModel
                {
                    Time = DateTime.Now,
                    Address = address,
                    Description = description,
                    Action = action
                });

                if (AlarmEvents.Count > 500)
                {
                    AlarmEvents.RemoveAt(AlarmEvents.Count - 1);
                }
            }

            if (_dispatcher.CheckAccess())
            {
                Add();
            }
            else
            {
                _dispatcher.Invoke(Add);
            }
        }

        #endregion

        #region 地址解析

        private static ushort ParsePlcAddress(string address)
        {
            ushort? parsed = TryParsePlcAddress(address);
            if (!parsed.HasValue)
            {
                throw new Exception($"无效PLC地址: {address}");
            }

            return parsed.Value;
        }

        private static ushort? TryParsePlcAddress(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                return null;
            }

            string trimmed = address.Trim();
            if (trimmed.StartsWith("M", StringComparison.OrdinalIgnoreCase)
                && ushort.TryParse(trimmed.Substring(1), out ushort result))
            {
                return result;
            }

            return null;
        }

        private sealed class AlarmReadItem
        {
            public AlarmReadItem(FaultAlarmItemViewModel alarm, ushort address)
            {
                Alarm = alarm;
                Address = address;
            }

            public FaultAlarmItemViewModel Alarm { get; }
            public ushort Address { get; }
        }

        private sealed class AlarmReadSegment
        {
            public AlarmReadSegment(ushort startAddress, ushort length)
            {
                StartAddress = startAddress;
                Length = length;
            }

            public ushort StartAddress { get; }
            public ushort Length { get; set; }
            public List<AlarmReadItem> Items { get; } = new();
        }

        #endregion
    }

    /// <summary>
    /// 故障报警项视图模型，承载单个报警点的状态与显示字段。
    /// </summary>
    /// By:ChengLei
    /// <remarks>
    /// 由 FaultDebugViewModel 维护并绑定到报警列表，用于展示当前状态、时间与屏蔽状态。
    /// </remarks>
    public class FaultAlarmItemViewModel : BaseViewModel
    {
        private string _address = string.Empty;
        private string _description = string.Empty;
        private bool _isActive;
        private bool _isMasked;
        private DateTime? _activeSince;
        private DateTime? _lastAlarmTime;
        private DateTime _lastChangedAt;

        public string Address
        {
            get => _address;
            set
            {
                if (_address != value)
                {
                    _address = value;
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

        public bool IsActive
        {
            get => _isActive;
            set
            {
                if (_isActive != value)
                {
                    _isActive = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(StateText));
                    OnPropertyChanged(nameof(StateColor));
                    OnPropertyChanged(nameof(AlarmTimeText));
                }
            }
        }

        public bool IsMasked
        {
            get => _isMasked;
            set
            {
                if (_isMasked != value)
                {
                    _isMasked = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(StateText));
                    OnPropertyChanged(nameof(StateColor));
                }
            }
        }

        public DateTime? ActiveSince
        {
            get => _activeSince;
            set
            {
                if (_activeSince != value)
                {
                    _activeSince = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(AlarmTimeText));
                }
            }
        }

        public DateTime? LastAlarmTime
        {
            get => _lastAlarmTime;
            set
            {
                if (_lastAlarmTime != value)
                {
                    _lastAlarmTime = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(AlarmTimeText));
                }
            }
        }

        public DateTime LastChangedAt
        {
            get => _lastChangedAt;
            set
            {
                if (_lastChangedAt != value)
                {
                    _lastChangedAt = value;
                    OnPropertyChanged();
                }
            }
        }

        public string StateText
        {
            get
            {
                if (!IsActive)
                {
                    return "正常";
                }

                return IsMasked ? "报警(已屏蔽)" : "报警中";
            }
        }

        public string StateColor
        {
            get
            {
                if (!IsActive)
                {
                    return "#16A34A";
                }

                return IsMasked ? "#64748B" : "#DC2626";
            }
        }

        public string AlarmTimeText
        {
            get
            {
                if (IsActive && ActiveSince.HasValue)
                {
                    return ActiveSince.Value.ToString("yyyy-MM-dd HH:mm:ss");
                }

                if (LastAlarmTime.HasValue)
                {
                    return LastAlarmTime.Value.ToString("yyyy-MM-dd HH:mm:ss");
                }

                return "--";
            }
        }
    }

    public class FaultEventRecordViewModel
    {
        public DateTime Time { get; set; } = DateTime.Now;
        public string Address { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
    }

    public class FaultDebugConfig
    {
        public int GlobalShieldAddress { get; set; } = 12;
        public List<FaultAlarmDefinition> AlarmPoints { get; set; } = new();
    }

    public class FaultAlarmDefinition
    {
        public string Address { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}

