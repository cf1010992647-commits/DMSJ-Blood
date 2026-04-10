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
    public class PointMonitorViewModel : BaseViewModel, IDisposable
    {
        private const string PointMonitorConfigFileName = "PointMonitorConfig.json";
        private const ushort MaxCoilsPerBatch = 120;
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

        private void ApplyConfig(PointMonitorConfig config)
        {
            Points.Clear();

            foreach (PlcPointConfigItem item in config.Points.Where(x => !string.IsNullOrWhiteSpace(x.Address)))
            {
                Points.Add(new PlcPoint
                {
                    Address = item.Address.Trim(),
                    Description = item.Description,
                    StatusColor = _connectedOffColor
                });
            }

            SelectedPoint = Points.FirstOrDefault();
        }

        private PointMonitorConfig ExportConfig()
        {
            return new PointMonitorConfig
            {
                Points = Points
                    .Where(x => !string.IsNullOrWhiteSpace(x.Address))
                    .Select(x => new PlcPointConfigItem
                    {
                        Address = x.Address.Trim(),
                        Description = x.Description?.Trim() ?? string.Empty
                    })
                    .ToList()
            };
        }

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

        private void AddPoint()
        {
            PlcPoint point = new()
            {
                Address = BuildSuggestedAddress(Points),
                Description = "新建点位",
                StatusColor = _connectedOffColor
            };

            Points.Add(point);
            SelectedPoint = point;
            SyncPollingPoints();
            StatusMessage = $"{DateTime.Now:HH:mm:ss} 已新增点位 {point.Address}。";
        }

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

        private static string BuildSuggestedAddress(IEnumerable<PlcPoint> points)
        {
            int max = points
                .Select(x => TryParseAddress(x.Address))
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .DefaultIfEmpty((ushort)0)
                .Max();

            return $"M{max + 1}";
        }

        #endregion

        #region PLC实时监控

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

        public void StopMonitoring()
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

        private static bool IsPlcConnected() => CommunicationManager.Is485Open;

        private async Task UpdatePointsCollectionAsync(CancellationToken token)
        {
            List<PlcPoint> snapshot = await _dispatcher.InvokeAsync(() => Points.ToList());
            var invalidResults = new Dictionary<PlcPoint, Brush>();
            var validItems = new List<CoilReadItem>();

            foreach (PlcPoint point in snapshot)
            {
                ushort? parsed = TryParseAddress(point.Address);
                if (!parsed.HasValue)
                {
                    invalidResults[point] = _notConnectedColor;
                    continue;
                }

                validItems.Add(new CoilReadItem(point, parsed.Value));
            }

            var statusByPoint = new Dictionary<PlcPoint, Brush>(invalidResults);
            var unresolvedItems = new List<CoilReadItem>();

            foreach (CoilReadItem item in validItems)
            {
                if (CommunicationManager.PlcPolling.TryGetCoil(item.Address, CoilCacheMaxAge, out PlcPollingService.CoilSnapshot cached))
                {
                    if (cached.Success)
                    {
                        statusByPoint[item.Point] = cached.Value ? _connectedOnColor : _connectedOffColor;
                    }
                    else
                    {
                        statusByPoint[item.Point] = _notConnectedColor;
                    }
                }
                else
                {
                    unresolvedItems.Add(item);
                }
            }

            List<CoilReadSegment> segments = BuildCoilReadSegments(unresolvedItems);

            foreach (CoilReadSegment segment in segments)
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
                    }
                }
            }

            await _dispatcher.InvokeAsync(() =>
            {
                foreach (KeyValuePair<PlcPoint, Brush> item in statusByPoint)
                {
                    item.Key.StatusColor = item.Value;
                }
            });
        }

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

        private async Task WritePointAsync(PlcPoint point, bool value)
        {
            try
            {
                ushort address = ParsePlcAddress(point.Address);

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
                });
            }
            catch (Exception ex)
            {
                await _dispatcher.InvokeAsync(() => point.StatusColor = _notConnectedColor);
                StatusMessage = $"{DateTime.Now:HH:mm:ss} 写点位失败: {ex.Message}";
            }
        }

        private async Task SetAllPointsColorAsync(Brush color)
        {
            await _dispatcher.InvokeAsync(() =>
            {
                foreach (PlcPoint point in Points)
                {
                    point.StatusColor = color;
                }
            });
        }

        private void SyncPollingPoints()
        {
            HashSet<ushort> desired = Points
                .Select(x => TryParseAddress(x.Address))
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

        private static ushort ParsePlcAddress(string address)
        {
            ushort? parsed = TryParseAddress(address);
            if (!parsed.HasValue)
            {
                throw new Exception($"无法解析PLC地址: {address}");
            }

            return parsed.Value;
        }

        private static ushort? TryParseAddress(string address)
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

        private sealed class CoilReadItem
        {
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
            public CoilReadSegment(ushort startAddress, ushort length)
            {
                StartAddress = startAddress;
                Length = length;
            }

            public ushort StartAddress { get; }
            public ushort Length { get; set; }
            public List<CoilReadItem> Items { get; } = new();
        }

        #endregion
    }

    public class PointMonitorConfig
    {
        public List<PlcPointConfigItem> Points { get; set; } = new();

        public List<PlcPointConfigItem> LeftPoints { get; set; } = new();
        public List<PlcPointConfigItem> RightPoints { get; set; } = new();
    }

    public class PlcPointConfigItem
    {
        public string Address { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}
