using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Blood_Alcohol.Services;

namespace Blood_Alcohol.ViewModels
{
    public sealed class AxisDebugViewModel : BaseViewModel, IDisposable
    {
        private const int AxisOffset = 100;
        private const ushort BaseMAddress = 1000;
        private const ushort BaseDAddress = 1000;
        private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(250);
        private static readonly TimeSpan CoilCacheMaxAge = TimeSpan.FromMilliseconds(800);

        private static readonly Brush LampOnBrush = Brushes.LimeGreen;
        private static readonly Brush LampOffBrush = Brushes.Gainsboro;

        private readonly SemaphoreSlim _plcLock = CommunicationManager.PlcAccessLock;
        private readonly AxisBinding[] _axes;
        private readonly HashSet<ushort> _registeredPollingCoils = new();
        private readonly CancellationTokenSource _pollCts = new();
        private readonly Task _pollTask;
        private bool _disposed;
        private volatile bool _isAxisCommandBusy;

        public AxisControlCardViewModel XAxis { get; }
        public AxisControlCardViewModel YAxis { get; }
        public AxisControlCardViewModel ZAxis { get; }
        public AxisControlCardViewModel ShakeAxis { get; }

        private string _actionMessage = "等待PLC连接...";
        public string ActionMessage
        {
            get => _actionMessage;
            set
            {
                if (_actionMessage != value)
                {
                    _actionMessage = value;
                    OnPropertyChanged();
                }
            }
        }

        public AxisDebugViewModel()
        {
            AxisBinding xAxis = BuildLinearAxis(1, "M1X轴伺服", "X轴手动定位", true);
            AxisBinding yAxis = BuildLinearAxis(2, "M2Y轴伺服", "Y轴手动定位", true);
            AxisBinding zAxis = BuildLinearAxis(3, "M3Z轴伺服", "Z轴手动定位", true);
            AxisBinding shakeAxis = BuildShakeAxis(4, "M4顶空进样阀");

            XAxis = xAxis.Card;
            YAxis = yAxis.Card;
            ZAxis = zAxis.Card;
            ShakeAxis = shakeAxis.Card;
            _axes = new[] { xAxis, yAxis, zAxis, shakeAxis };
            RegisterAxisPollingPoints();

            _pollTask = Task.Run(() => PollAxisLoopAsync(_pollCts.Token));
        }

        private void RegisterAxisPollingPoints()
        {
            foreach (AxisBinding axis in _axes)
            {
                ushort startM = (ushort)(axis.BaseM + 3);
                RegisterPollingCoil(startM);
                RegisterPollingCoil((ushort)(startM + 9));
                RegisterPollingCoil((ushort)(startM + 10));
                RegisterPollingCoil((ushort)(startM + 11));
            }
        }

        private void RegisterPollingCoil(ushort address)
        {
            if (_registeredPollingCoils.Add(address))
            {
                CommunicationManager.PlcPolling.RegisterCoil(address, PollInterval);
            }
        }

        private void UnregisterAxisPollingPoints()
        {
            foreach (ushort address in _registeredPollingCoils)
            {
                CommunicationManager.PlcPolling.UnregisterCoil(address);
            }

            _registeredPollingCoils.Clear();
        }

        private AxisBinding BuildLinearAxis(int axisNo, string title, string manualLocateText, bool showManualLocate)
        {
            var card = new AxisControlCardViewModel
            {
                Title = title,
                CurrentPosition = "0",
                TargetPosition = "0",
                ManualSpeed = "0",
                AutoSpeed = "0",
                ShowPositionFields = true,
                ShowManualLocate = showManualLocate,
                ManualLocateText = manualLocateText,
                ManualLocateInput = "0"
            };

            card.StatusLamps.Add(new AxisStatusLampViewModel("正限"));
            card.StatusLamps.Add(new AxisStatusLampViewModel("原点"));
            card.StatusLamps.Add(new AxisStatusLampViewModel("反限"));
            card.StatusLamps.Add(new AxisStatusLampViewModel("回原点OK"));

            var axis = new AxisBinding(axisNo, card);
            BindAxisCommands(axis);
            return axis;
        }

        private AxisBinding BuildShakeAxis(int axisNo, string title)
        {
            var card = new AxisControlCardViewModel
            {
                Title = title,
                CurrentPosition = string.Empty,
                TargetPosition = string.Empty,
                ManualSpeed = "0",
                AutoSpeed = "0",
                ShowPositionFields = false,
                ShowManualLocate = false,
                ManualLocateText = string.Empty,
                ManualLocateInput = string.Empty
            };

            card.StatusLamps.Add(new AxisStatusLampViewModel("原点"));
            card.StatusLamps.Add(new AxisStatusLampViewModel("回原点OK"));

            var axis = new AxisBinding(axisNo, card);
            BindAxisCommands(axis);
            return axis;
        }

        private void BindAxisCommands(AxisBinding axis)
        {
            axis.Card.JogPlusPressCommand = new RelayCommand(_ => _ = WriteAxisCommandLevelAsync(axis, AxisCommand.JogPlus, true));
            axis.Card.JogPlusReleaseCommand = new RelayCommand(_ => _ = WriteAxisCommandLevelAsync(axis, AxisCommand.JogPlus, false));

            axis.Card.JogMinusPressCommand = new RelayCommand(_ => _ = WriteAxisCommandLevelAsync(axis, AxisCommand.JogMinus, true));
            axis.Card.JogMinusReleaseCommand = new RelayCommand(_ => _ = WriteAxisCommandLevelAsync(axis, AxisCommand.JogMinus, false));

            axis.Card.GoHomePressCommand = new RelayCommand(_ => _ = WriteAxisCommandLevelAsync(axis, AxisCommand.GoHome, true));
            axis.Card.GoHomeReleaseCommand = new RelayCommand(_ => _ = WriteAxisCommandLevelAsync(axis, AxisCommand.GoHome, false));

            axis.Card.ManualLocatePressCommand = new RelayCommand(_ => _ = ExecuteManualLocateAsync(axis, true));
            axis.Card.ManualLocateReleaseCommand = new RelayCommand(_ => _ = ExecuteManualLocateAsync(axis, false));
        }

        private async Task PollAxisLoopAsync(CancellationToken token)
        {
            bool warnedNotConnected = false;

            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (_isAxisCommandBusy)
                    {
                        await Task.Delay(40, token);
                        continue;
                    }

                    if (!CommunicationManager.Is485Open)
                    {
                        if (!warnedNotConnected)
                        {
                            SetActionMessage("PLC未连接，轴状态监控等待中...");
                            warnedNotConnected = true;
                        }

                        await Task.Delay(PollInterval, token);
                        continue;
                    }

                    warnedNotConnected = false;

                    foreach (AxisBinding axis in _axes)
                    {
                        await RefreshAxisAsync(axis, token);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    SetActionMessage($"轴状态刷新失败: {ex.Message}");
                }

                await Task.Delay(PollInterval, token);
            }
        }

        private async Task RefreshAxisAsync(AxisBinding axis, CancellationToken token)
        {
            ushort startM = (ushort)(axis.BaseM + 3);
            bool homeDone = await ReadCoilStateWithCacheAsync(startM, token);
            bool posLimit = await ReadCoilStateWithCacheAsync((ushort)(startM + 9), token);
            bool negLimit = await ReadCoilStateWithCacheAsync((ushort)(startM + 10), token);
            bool homeSensor = await ReadCoilStateWithCacheAsync((ushort)(startM + 11), token);

            ushort[] regs = await ReadRegistersWithLockAsync((ushort)(axis.BaseD + 2), 16, token);
            int currentPosition = ComposeInt32(GetRegisterValue(regs, 0), GetRegisterValue(regs, 1)); // D+2 low, D+3 high
            short jogSpeed = ToInt16(GetRegisterValue(regs, 2));         // D+4
            short autoSpeed = ToInt16(GetRegisterValue(regs, 6));        // D+8
            int manualTarget = ComposeInt32(GetRegisterValue(regs, 14), GetRegisterValue(regs, 15)); // D+16 low, D+17 high

            RunOnUiThread(() =>
            {
                if (axis.Card.ShowPositionFields)
                {
                    axis.Card.CurrentPosition = currentPosition.ToString(CultureInfo.InvariantCulture);
                    axis.Card.TargetPosition = manualTarget.ToString(CultureInfo.InvariantCulture);
                }

                axis.Card.ManualSpeed = jogSpeed.ToString(CultureInfo.InvariantCulture);
                axis.Card.AutoSpeed = autoSpeed.ToString(CultureInfo.InvariantCulture);

                if (axis.Card.StatusLamps.Count == 4)
                {
                    // 顺序与UI一致：正限 / 原点 / 反限 / 回原点OK
                    axis.Card.StatusLamps[0].Color = posLimit ? LampOnBrush : LampOffBrush;
                    axis.Card.StatusLamps[1].Color = homeSensor ? LampOnBrush : LampOffBrush;
                    axis.Card.StatusLamps[2].Color = negLimit ? LampOnBrush : LampOffBrush;
                    axis.Card.StatusLamps[3].Color = homeDone ? LampOnBrush : LampOffBrush;
                }
                else if (axis.Card.StatusLamps.Count == 2)
                {
                    // M4：原点 / 回原点OK
                    axis.Card.StatusLamps[0].Color = homeSensor ? LampOnBrush : LampOffBrush;
                    axis.Card.StatusLamps[1].Color = homeDone ? LampOnBrush : LampOffBrush;
                }
            });
        }

        private async Task ExecuteManualLocateAsync(AxisBinding axis, bool level)
        {
            _isAxisCommandBusy = true;
            try
            {
                SetActionMessage($"{axis.Card.Title}: 手动定位触发={(level ? 1 : 0)}...");

                if (!axis.Card.ShowManualLocate)
                {
                    return;
                }

                if (!CommunicationManager.Is485Open)
                {
                    SetActionMessage($"{axis.Card.Title}: PLC未连接，手动定位未执行。");
                    return;
                }

                if (level)
                {
                    if (!TryParseInt32(axis.Card.ManualLocateInput, out int target))
                    {
                        SetActionMessage($"{axis.Card.Title}: 手动定位值无效。");
                        return;
                    }

                    SplitInt32(target, out ushort lowWord, out ushort highWord);
                    await WriteRegisterWithLockAsync((ushort)(axis.BaseD + 16), lowWord);
                    await WriteRegisterWithLockAsync((ushort)(axis.BaseD + 17), highWord);
                    await WriteCoilWithLockAsync((ushort)(axis.BaseM + 19), true);
                    SetActionMessage($"{axis.Card.Title}: 手动定位触发=1，目标={target}");
                }
                else
                {
                    await WriteCoilWithLockAsync((ushort)(axis.BaseM + 19), false);
                    SetActionMessage($"{axis.Card.Title}: 手动定位触发=0");
                }
            }
            catch (Exception ex)
            {
                SetActionMessage($"{axis.Card.Title}: 手动定位失败 - {ex.Message}");
            }
            finally
            {
                _isAxisCommandBusy = false;
            }
        }

        private async Task WriteAxisCommandLevelAsync(AxisBinding axis, AxisCommand command, bool level)
        {
            _isAxisCommandBusy = true;
            try
            {
                SetActionMessage($"{axis.Card.Title}: {ToCommandText(command)}={(level ? 1 : 0)}...");

                if (!CommunicationManager.Is485Open)
                {
                    SetActionMessage($"{axis.Card.Title}: PLC未连接，{command}未执行。");
                    return;
                }

                ushort address = command switch
                {
                    AxisCommand.JogPlus => (ushort)(axis.BaseM + 0),
                    AxisCommand.JogMinus => (ushort)(axis.BaseM + 1),
                    AxisCommand.GoHome => (ushort)(axis.BaseM + 2),
                    _ => throw new ArgumentOutOfRangeException(nameof(command), command, null)
                };

                await WriteCoilWithLockAsync(address, level);
                SetActionMessage($"{axis.Card.Title}: {ToCommandText(command)}={(level ? 1 : 0)}");
            }
            catch (Exception ex)
            {
                SetActionMessage($"{axis.Card.Title}: 写入失败 - {ex.Message}");
            }
            finally
            {
                _isAxisCommandBusy = false;
            }
        }

        private static string ToCommandText(AxisCommand command)
        {
            return command switch
            {
                AxisCommand.JogPlus => "JOG+",
                AxisCommand.JogMinus => "JOG-",
                AxisCommand.GoHome => "回原点",
                _ => command.ToString()
            };
        }

        private async Task<bool> ReadCoilStateWithCacheAsync(ushort address, CancellationToken token)
        {
            if (CommunicationManager.PlcPolling.TryGetCoil(address, CoilCacheMaxAge, out PlcPollingService.CoilSnapshot cached)
                && cached.Success)
            {
                return cached.Value;
            }

            await _plcLock.WaitAsync(token);
            try
            {
                var read = await CommunicationManager.Plc.TryReadCoilsAsync(address, 1);
                if (!read.Success)
                {
                    throw new InvalidOperationException(read.Error);
                }

                return read.Values.Length > 0 && read.Values[0];
            }
            finally
            {
                _plcLock.Release();
            }
        }

        private async Task<ushort[]> ReadRegistersWithLockAsync(ushort address, ushort length, CancellationToken token)
        {
            await _plcLock.WaitAsync(token);
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
                _plcLock.Release();
            }
        }

        private async Task WriteCoilWithLockAsync(ushort address, bool value)
        {
            await _plcLock.WaitAsync();
            try
            {
                var write = await CommunicationManager.Plc.TryWriteSingleCoilAsync(address, value);
                if (!write.Success)
                {
                    throw new InvalidOperationException(write.Error);
                }
            }
            finally
            {
                _plcLock.Release();
            }
        }

        private async Task WriteRegisterWithLockAsync(ushort address, ushort value)
        {
            await _plcLock.WaitAsync();
            try
            {
                var write = await CommunicationManager.Plc.TryWriteSingleRegisterAsync(address, value);
                if (!write.Success)
                {
                    throw new InvalidOperationException(write.Error);
                }
            }
            finally
            {
                _plcLock.Release();
            }
        }

        private static bool GetCoilValue(bool[] source, int index)
        {
            return index >= 0 && index < source.Length && source[index];
        }

        private static ushort GetRegisterValue(ushort[] source, int index)
        {
            return index >= 0 && index < source.Length ? source[index] : (ushort)0;
        }

        private static short ToInt16(ushort value)
        {
            unchecked
            {
                return (short)value;
            }
        }

        private static bool TryParseInt32(string input, out int value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            if (int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out int direct))
            {
                value = direct;
                return true;
            }

            if (double.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out double d)
                || double.TryParse(input, NumberStyles.Float, CultureInfo.CurrentCulture, out d))
            {
                int rounded = (int)Math.Round(d);
                if (rounded >= int.MinValue && rounded <= int.MaxValue)
                {
                    value = rounded;
                    return true;
                }
            }

            return false;
        }

        private static int ComposeInt32(ushort lowWord, ushort highWord)
        {
            int raw = (highWord << 16) | lowWord;
            return raw;
        }

        private static void SplitInt32(int value, out ushort lowWord, out ushort highWord)
        {
            unchecked
            {
                lowWord = (ushort)(value & 0xFFFF);
                highWord = (ushort)((value >> 16) & 0xFFFF);
            }
        }

        private void SetActionMessage(string action)
        {
            string text = $"{DateTime.Now:HH:mm:ss}  {action}";
            RunOnUiThread(() => ActionMessage = text);
        }

        private static void RunOnUiThread(Action action)
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                action();
                return;
            }

            dispatcher.Invoke(action, DispatcherPriority.Send);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _pollCts.Cancel();
            _pollCts.Dispose();
            UnregisterAxisPollingPoints();
        }

        private sealed class AxisBinding
        {
            public AxisBinding(int axisNo, AxisControlCardViewModel card)
            {
                AxisNo = axisNo;
                Card = card;
            }

            public int AxisNo { get; }
            public AxisControlCardViewModel Card { get; }
            public ushort BaseM => (ushort)(BaseMAddress + (AxisNo - 1) * AxisOffset);
            public ushort BaseD => (ushort)(BaseDAddress + (AxisNo - 1) * AxisOffset);
        }

        private enum AxisCommand
        {
            JogPlus,
            JogMinus,
            GoHome
        }
    }

    public class AxisControlCardViewModel : BaseViewModel
    {
        private string _title = string.Empty;
        private string _currentPosition = string.Empty;
        private string _targetPosition = string.Empty;
        private string _manualSpeed = string.Empty;
        private string _autoSpeed = string.Empty;
        private bool _showPositionFields = true;
        private bool _showManualLocate;
        private string _manualLocateText = string.Empty;
        private string _manualLocateInput = string.Empty;

        public string Title
        {
            get => _title;
            set
            {
                if (_title != value)
                {
                    _title = value;
                    OnPropertyChanged();
                }
            }
        }

        public ObservableCollection<AxisStatusLampViewModel> StatusLamps { get; } = new();

        public string CurrentPosition
        {
            get => _currentPosition;
            set
            {
                if (_currentPosition != value)
                {
                    _currentPosition = value;
                    OnPropertyChanged();
                }
            }
        }

        public string TargetPosition
        {
            get => _targetPosition;
            set
            {
                if (_targetPosition != value)
                {
                    _targetPosition = value;
                    OnPropertyChanged();
                }
            }
        }

        public string ManualSpeed
        {
            get => _manualSpeed;
            set
            {
                if (_manualSpeed != value)
                {
                    _manualSpeed = value;
                    OnPropertyChanged();
                }
            }
        }

        public string AutoSpeed
        {
            get => _autoSpeed;
            set
            {
                if (_autoSpeed != value)
                {
                    _autoSpeed = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool ShowPositionFields
        {
            get => _showPositionFields;
            set
            {
                if (_showPositionFields != value)
                {
                    _showPositionFields = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool ShowManualLocate
        {
            get => _showManualLocate;
            set
            {
                if (_showManualLocate != value)
                {
                    _showManualLocate = value;
                    OnPropertyChanged();
                }
            }
        }

        public string ManualLocateText
        {
            get => _manualLocateText;
            set
            {
                if (_manualLocateText != value)
                {
                    _manualLocateText = value;
                    OnPropertyChanged();
                }
            }
        }

        public string ManualLocateInput
        {
            get => _manualLocateInput;
            set
            {
                if (_manualLocateInput != value)
                {
                    _manualLocateInput = value;
                    OnPropertyChanged();
                }
            }
        }

        public ICommand? JogPlusPressCommand { get; set; }
        public ICommand? JogPlusReleaseCommand { get; set; }
        public ICommand? JogMinusPressCommand { get; set; }
        public ICommand? JogMinusReleaseCommand { get; set; }
        public ICommand? GoHomePressCommand { get; set; }
        public ICommand? GoHomeReleaseCommand { get; set; }
        public ICommand? ManualLocatePressCommand { get; set; }
        public ICommand? ManualLocateReleaseCommand { get; set; }
    }

    public class AxisStatusLampViewModel : BaseViewModel
    {
        private Brush _color = Brushes.Gainsboro;

        public AxisStatusLampViewModel(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public Brush Color
        {
            get => _color;
            set
            {
                if (_color != value)
                {
                    _color = value;
                    OnPropertyChanged();
                }
            }
        }
    }
}
