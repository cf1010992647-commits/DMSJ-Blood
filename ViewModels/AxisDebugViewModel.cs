using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Blood_Alcohol.Models;
using Blood_Alcohol.Services;

namespace Blood_Alcohol.ViewModels
{
    /// <summary>
    /// 轴调试视图模型。
    /// </summary>
    /// By:ChengLei
    /// <remarks>
    /// 负责轴状态监控与调试命令下发。
    /// </remarks>
    public sealed class AxisDebugViewModel : BaseViewModel, IDisposable
    {
        private const string AxisAddressConfigFileName = "AxisDebugAddressConfig.json";
        private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(250);
        private static readonly TimeSpan CoilCacheMaxAge = TimeSpan.FromMilliseconds(800);

        private static readonly Brush LampOnBrush = Brushes.LimeGreen;
        private static readonly Brush LampOffBrush = Brushes.Gainsboro;

        private readonly SemaphoreSlim _plcLock = CommunicationManager.PlcAccessLock;
        private readonly ConfigService<AxisDebugAddressConfig> _axisAddressConfigService = new(AxisAddressConfigFileName);
        private readonly AxisDebugAddressConfig _axisAddressConfig;
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

        /// <summary>
        /// 初始化轴调试视图模型并加载地址配置。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由 AxisDebugView 创建 DataContext 时调用，并启动 PollAxisLoopAsync。
        /// </remarks>
        public AxisDebugViewModel()
        {
            _axisAddressConfig = LoadAxisAddressConfig();

            AxisBinding xAxis = BuildLinearAxis(1, ResolveAddressProfile(1), "M1 X轴伺服", "X轴手动定位", true);
            AxisBinding yAxis = BuildLinearAxis(2, ResolveAddressProfile(2), "M2 Y轴伺服", "Y轴手动定位", true);
            AxisBinding zAxis = BuildLinearAxis(3, ResolveAddressProfile(3), "M3 Z轴伺服", "Z轴手动定位", true);
            AxisBinding shakeAxis = BuildShakeAxis(4, ResolveAddressProfile(4), "M4 摇匀轴");

            XAxis = xAxis.Card;
            YAxis = yAxis.Card;
            ZAxis = zAxis.Card;
            ShakeAxis = shakeAxis.Card;
            _axes = new[] { xAxis, yAxis, zAxis, shakeAxis };
            RegisterAxisPollingPoints();

            _pollTask = Task.Run(() => PollAxisLoopAsync(_pollCts.Token));
        }

        /// <summary>
        /// 加载轴地址映射配置并在缺项时回填默认值。
        /// </summary>
        /// By:ChengLei
        /// <returns>返回可用的轴地址配置对象。</returns>
        /// <remarks>
        /// 由构造函数调用；加载失败时自动回退默认配置并保存。
        /// </remarks>
        private AxisDebugAddressConfig LoadAxisAddressConfig()
        {
            AxisDebugAddressConfig defaults = new AxisDebugAddressConfig();

            try
            {
                AxisDebugAddressConfig config = _axisAddressConfigService.Load() ?? new AxisDebugAddressConfig();
                config.Axis1 ??= defaults.Axis1;
                config.Axis2 ??= defaults.Axis2;
                config.Axis3 ??= defaults.Axis3;
                config.Axis4 ??= defaults.Axis4;

                if (string.IsNullOrWhiteSpace(config.Axis1.AxisName))
                {
                    config.Axis1.AxisName = defaults.Axis1.AxisName;
                }

                if (string.IsNullOrWhiteSpace(config.Axis2.AxisName))
                {
                    config.Axis2.AxisName = defaults.Axis2.AxisName;
                }

                if (string.IsNullOrWhiteSpace(config.Axis3.AxisName))
                {
                    config.Axis3.AxisName = defaults.Axis3.AxisName;
                }

                if (string.IsNullOrWhiteSpace(config.Axis4.AxisName))
                {
                    config.Axis4.AxisName = defaults.Axis4.AxisName;
                }

                _axisAddressConfigService.Save(config);
                return config;
            }
            catch
            {
                _axisAddressConfigService.Save(defaults);
                return defaults;
            }
        }

        /// <summary>
        /// 根据轴号返回对应地址映射。
        /// </summary>
        /// By:ChengLei
        /// <param name="axisNo">轴编号（1~4）。</param>
        /// <returns>返回指定轴号的地址映射对象。</returns>
        /// <remarks>
        /// 由构造函数调用，用于构建四个轴卡片。
        /// </remarks>
        private AxisAddressProfile ResolveAddressProfile(int axisNo)
        {
            return axisNo switch
            {
                1 => _axisAddressConfig.Axis1 ?? new AxisAddressProfile(),
                2 => _axisAddressConfig.Axis2 ?? new AxisAddressProfile(),
                3 => _axisAddressConfig.Axis3 ?? new AxisAddressProfile(),
                4 => _axisAddressConfig.Axis4 ?? new AxisAddressProfile(),
                _ => new AxisAddressProfile()
            };
        }

        /// <summary>
        /// 注册当前页面使用的轴状态轮询点位。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由构造函数调用；与 Dispose 中注销流程成对出现。
        /// </remarks>
        private void RegisterAxisPollingPoints()
        {
            foreach (AxisBinding axis in _axes)
            {
                RegisterPollingCoil(axis.Addresses.HomeDoneCoil);
                RegisterPollingCoil(axis.Addresses.PositiveLimitCoil);
                RegisterPollingCoil(axis.Addresses.NegativeLimitCoil);
                RegisterPollingCoil(axis.Addresses.HomeSensorCoil);
            }
        }

        /// <summary>
        /// 向轮询服务注册单个线圈地址。
        /// </summary>
        /// By:ChengLei
        /// <param name="address">PLC地址。</param>
        /// <remarks>
        /// 由 RegisterAxisPollingPoints 内部循环调用。
        /// </remarks>
        private void RegisterPollingCoil(ushort address)
        {
            if (_registeredPollingCoils.Add(address))
            {
                CommunicationManager.PlcPolling.RegisterCoil(address, PollInterval);
            }
        }

        /// <summary>
        /// 注销本页注册的全部轮询线圈。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由 Dispose 调用，避免页面关闭后继续轮询。
        /// </remarks>
        private void UnregisterAxisPollingPoints()
        {
            foreach (ushort address in _registeredPollingCoils)
            {
                CommunicationManager.PlcPolling.UnregisterCoil(address);
            }

            _registeredPollingCoils.Clear();
        }

        /// <summary>
        /// 构建线性轴卡片并初始化状态灯和输入区域。
        /// </summary>
        /// By:ChengLei
        /// <param name="axisNo">轴编号（1~4）。</param>
        /// <param name="addresses">该轴对应的地址映射配置。</param>
        /// <param name="fallbackTitle">地址配置缺省时显示的默认标题。</param>
        /// <param name="manualLocateText">手动定位按钮显示文本。</param>
        /// <param name="showManualLocate">是否展示手动定位输入区域。</param>
        /// <returns>返回构建完成的线性轴绑定对象。</returns>
        /// <remarks>
        /// 由构造函数调用三次，分别创建X/Y/Z轴。
        /// </remarks>
        private AxisBinding BuildLinearAxis(int axisNo, AxisAddressProfile addresses, string fallbackTitle, string manualLocateText, bool showManualLocate)
        {
            var card = new AxisControlCardViewModel
            {
                Title = string.IsNullOrWhiteSpace(addresses.AxisName) ? fallbackTitle : addresses.AxisName,
                CurrentPosition = "0",
                TargetPosition = "0",
                ManualSpeed = "0",
                AutoSpeed = "0",
                ManualSpeedInput = "0",
                AutoSpeedInput = "0",
                ShowPositionFields = true,
                ShowManualLocate = showManualLocate,
                ManualLocateText = manualLocateText,
                ManualLocateInput = "0"
            };

            card.StatusLamps.Add(new AxisStatusLampViewModel("正限位"));
            card.StatusLamps.Add(new AxisStatusLampViewModel("原点"));
            card.StatusLamps.Add(new AxisStatusLampViewModel("负限位"));
            card.StatusLamps.Add(new AxisStatusLampViewModel("回原点完成"));

            var axis = new AxisBinding(axisNo, card, addresses);
            BindAxisCommands(axis);
            return axis;
        }

        /// <summary>
        /// 构建摇匀轴卡片并初始化状态灯。
        /// </summary>
        /// By:ChengLei
        /// <param name="axisNo">轴编号（1~4）。</param>
        /// <param name="addresses">该轴对应的地址映射配置。</param>
        /// <param name="fallbackTitle">地址配置缺省时显示的默认标题。</param>
        /// <returns>返回构建完成的摇匀轴绑定对象。</returns>
        /// <remarks>
        /// 由构造函数调用一次，创建M4摇匀轴。
        /// </remarks>
        private AxisBinding BuildShakeAxis(int axisNo, AxisAddressProfile addresses, string fallbackTitle)
        {
            var card = new AxisControlCardViewModel
            {
                Title = string.IsNullOrWhiteSpace(addresses.AxisName) ? fallbackTitle : addresses.AxisName,
                CurrentPosition = string.Empty,
                TargetPosition = string.Empty,
                ManualSpeed = "0",
                AutoSpeed = "0",
                ManualSpeedInput = "0",
                AutoSpeedInput = "0",
                ShowPositionFields = false,
                ShowManualLocate = false,
                ManualLocateText = string.Empty,
                ManualLocateInput = string.Empty
            };

            card.StatusLamps.Add(new AxisStatusLampViewModel("原点"));
            card.StatusLamps.Add(new AxisStatusLampViewModel("回原点完成"));

            var axis = new AxisBinding(axisNo, card, addresses);
            BindAxisCommands(axis);
            return axis;
        }

        /// <summary>
        /// 绑定轴卡片命令（点动、回零、手动定位、速度写入）。
        /// </summary>
        /// By:ChengLei
        /// <param name="axis">当前轴绑定对象。</param>
        /// <remarks>
        /// 由 BuildLinearAxis 与 BuildShakeAxis 在卡片创建后调用。
        /// </remarks>
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
            axis.Card.WriteManualSpeedCommand = new RelayCommand(_ => _ = WriteAxisSpeedAsync(axis, true));
            axis.Card.WriteAutoSpeedCommand = new RelayCommand(_ => _ = WriteAxisSpeedAsync(axis, false));
        }

        /// <summary>
        /// 后台轮询所有轴状态并刷新界面。
        /// </summary>
        /// By:ChengLei
        /// <param name="token">取消令牌，用于终止当前异步流程。</param>
        /// <returns>返回轴状态轮询异步任务。</returns>
        /// <remarks>
        /// 由构造函数内 Task.Run 启动，循环调用 RefreshAxisAsync。
        /// </remarks>
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

        /// <summary>
        /// 读取单轴状态、位置、速度并更新界面绑定值。
        /// </summary>
        /// By:ChengLei
        /// <param name="axis">当前轴绑定对象。</param>
        /// <param name="token">取消令牌，用于终止当前异步流程。</param>
        /// <returns>返回单轴刷新异步任务。</returns>
        /// <remarks>
        /// 由 PollAxisLoopAsync 在每个轮询周期对各轴调用。
        /// </remarks>
        private async Task RefreshAxisAsync(AxisBinding axis, CancellationToken token)
        {
            bool homeDone = await ReadCoilStateWithCacheAsync(axis.Addresses.HomeDoneCoil, token);
            bool posLimit = await ReadCoilStateWithCacheAsync(axis.Addresses.PositiveLimitCoil, token);
            bool negLimit = await ReadCoilStateWithCacheAsync(axis.Addresses.NegativeLimitCoil, token);
            bool homeSensor = await ReadCoilStateWithCacheAsync(axis.Addresses.HomeSensorCoil, token);

            ushort currentPositionLow = await ReadRegisterValueWithLockAsync(axis.Addresses.CurrentPositionLowRegister, token);
            ushort currentPositionHigh = await ReadRegisterValueWithLockAsync(axis.Addresses.CurrentPositionHighRegister, token);
            ushort manualSpeedRaw = await ReadRegisterValueWithLockAsync(axis.Addresses.ManualSpeedRegister, token);
            ushort autoSpeedRaw = await ReadRegisterValueWithLockAsync(axis.Addresses.AutoSpeedRegister, token);
            ushort manualTargetLow = await ReadRegisterValueWithLockAsync(axis.Addresses.ManualTargetLowRegister, token);
            ushort manualTargetHigh = await ReadRegisterValueWithLockAsync(axis.Addresses.ManualTargetHighRegister, token);

            int currentPosition = ComposeInt32(currentPositionLow, currentPositionHigh);
            short jogSpeed = ToInt16(manualSpeedRaw);
            short autoSpeed = ToInt16(autoSpeedRaw);
            int manualTarget = ComposeInt32(manualTargetLow, manualTargetHigh);

            RunOnUiThread(() =>
            {
                if (axis.Card.ShowPositionFields)
                {
                    axis.Card.CurrentPosition = currentPosition.ToString(CultureInfo.InvariantCulture);
                    axis.Card.TargetPosition = manualTarget.ToString(CultureInfo.InvariantCulture);
                }

                axis.Card.UpdateSpeedFromPlc(jogSpeed, autoSpeed);

                if (axis.Card.StatusLamps.Count == 4)
                {
                    // 顺序与UI一致：正限位 / 原点 / 负限位 / 回原点完成
                    axis.Card.StatusLamps[0].Color = posLimit ? LampOnBrush : LampOffBrush;
                    axis.Card.StatusLamps[1].Color = homeSensor ? LampOnBrush : LampOffBrush;
                    axis.Card.StatusLamps[2].Color = negLimit ? LampOnBrush : LampOffBrush;
                    axis.Card.StatusLamps[3].Color = homeDone ? LampOnBrush : LampOffBrush;
                }
                else if (axis.Card.StatusLamps.Count == 2)
                {
                    // M4：原点 / 回原点完成
                    axis.Card.StatusLamps[0].Color = homeSensor ? LampOnBrush : LampOffBrush;
                    axis.Card.StatusLamps[1].Color = homeDone ? LampOnBrush : LampOffBrush;
                }
            });
        }

        /// <summary>
        /// 执行手动定位流程，写入目标坐标并触发定位位。
        /// </summary>
        /// By:ChengLei
        /// <param name="axis">当前轴绑定对象。</param>
        /// <param name="level">命令电平，true为置位，false为复位。</param>
        /// <returns>返回手动定位执行异步任务。</returns>
        /// <remarks>
        /// 由手动定位按钮按下/释放命令触发。
        /// </remarks>
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
                    SetActionMessage($"{axis.Card.Title}: PLC未连接，跳过手动定位。");
                    return;
                }

                if (level)
                {
                    if (!TryParseInt32(axis.Card.ManualLocateInput, out int target))
                    {
                        SetActionMessage($"{axis.Card.Title}: 手动定位输入无效。");
                        return;
                    }

                    SplitInt32(target, out ushort lowWord, out ushort highWord);
                    await WriteRegisterWithLockAsync(axis.Addresses.ManualTargetLowRegister, lowWord);
                    await WriteRegisterWithLockAsync(axis.Addresses.ManualTargetHighRegister, highWord);
                    await WriteCoilWithLockAsync(axis.Addresses.ManualLocateTriggerCoil, true);
                    SetActionMessage($"{axis.Card.Title}: 手动定位触发=1，目标={target}");
                }
                else
                {
                    await WriteCoilWithLockAsync(axis.Addresses.ManualLocateTriggerCoil, false);
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

        /// <summary>
        /// 下发轴控制命令电平。
        /// </summary>
        /// By:ChengLei
        /// <param name="axis">当前轴绑定对象。</param>
        /// <param name="command">轴命令类型（点动正向/反向/回原点）。</param>
        /// <param name="level">命令电平，true为置位，false为复位。</param>
        /// <returns>返回命令下发异步任务。</returns>
        /// <remarks>
        /// 由点动和回原点按钮按下/释放命令触发。
        /// </remarks>
        private async Task WriteAxisCommandLevelAsync(AxisBinding axis, AxisCommand command, bool level)
        {
            _isAxisCommandBusy = true;
            try
            {
                SetActionMessage($"{axis.Card.Title}: {ToCommandText(command)}={(level ? 1 : 0)}...");

                if (!CommunicationManager.Is485Open)
                {
                    SetActionMessage($"{axis.Card.Title}: PLC未连接，跳过指令下发。");
                    return;
                }

                if (level && await IsJogBlockedByLimitAsync(axis, command))
                {
                    string limitText = command == AxisCommand.JogPlus ? "正限位" : "负限位";
                    SetActionMessage($"{axis.Card.Title}: {limitText}已触发，阻止{ToCommandText(command)}=1");
                    return;
                }

                ushort address = command switch
                {
                    AxisCommand.JogPlus => axis.Addresses.JogPlusCoil,
                    AxisCommand.JogMinus => axis.Addresses.JogMinusCoil,
                    AxisCommand.GoHome => axis.Addresses.GoHomeCoil,
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

        /// <summary>
        /// 判断点动方向是否被限位信号阻挡。
        /// </summary>
        /// By:ChengLei
        /// <param name="axis">当前轴绑定对象。</param>
        /// <param name="command">轴命令类型（点动正向/反向/回原点）。</param>
        /// <returns>返回当前方向是否被限位阻挡。</returns>
        /// <remarks>
        /// 由 WriteAxisCommandLevelAsync 在点动置位前调用。
        /// </remarks>
        private async Task<bool> IsJogBlockedByLimitAsync(AxisBinding axis, AxisCommand command)
        {
            if (command != AxisCommand.JogPlus && command != AxisCommand.JogMinus)
            {
                return false;
            }

            ushort limitAddress = command == AxisCommand.JogPlus
                ? axis.Addresses.PositiveLimitCoil
                : axis.Addresses.NegativeLimitCoil;

            return await ReadCoilStateWithCacheAsync(limitAddress, CancellationToken.None);
        }

        /// <summary>
        /// 写入手动速度或自动速度到PLC。
        /// </summary>
        /// By:ChengLei
        /// <param name="axis">当前轴绑定对象。</param>
        /// <param name="manualSpeed">是否操作手动速度；false表示自动速度。</param>
        /// <returns>返回速度写入异步任务。</returns>
        /// <remarks>
        /// 由手动速度/自动速度写入按钮触发。
        /// </remarks>
        private async Task WriteAxisSpeedAsync(AxisBinding axis, bool manualSpeed)
        {
            _isAxisCommandBusy = true;
            try
            {
                string speedType = manualSpeed ? "手动速度" : "自动速度";
                string inputText = manualSpeed ? axis.Card.ManualSpeedInput : axis.Card.AutoSpeedInput;

                if (!CommunicationManager.Is485Open)
                {
                    SetActionMessage($"{axis.Card.Title}: PLC未连接，跳过速度下发。");
                    return;
                }

                if (!TryParseInt16(inputText, out short speed))
                {
                    SetActionMessage($"{axis.Card.Title}: {speedType}输入无效（范围: -32768~32767）。");
                    return;
                }

                ushort address = manualSpeed ? axis.Addresses.ManualSpeedRegister : axis.Addresses.AutoSpeedRegister;
                await WriteRegisterWithLockAsync(address, unchecked((ushort)speed));
                axis.Card.CommitSpeedInput(manualSpeed, speed);
                SetActionMessage($"{axis.Card.Title}: {speedType}已下发，D{address}={speed}");
            }
            catch (Exception ex)
            {
                SetActionMessage($"{axis.Card.Title}: 速度下发失败 - {ex.Message}");
            }
            finally
            {
                _isAxisCommandBusy = false;
            }
        }

        /// <summary>
        /// 将命令枚举转换为日志显示文本。
        /// </summary>
        /// By:ChengLei
        /// <param name="command">轴命令类型（点动正向/反向/回原点）。</param>
        /// <returns>返回命令显示文本。</returns>
        /// <remarks>
        /// 由 WriteAxisCommandLevelAsync 生成日志文本时调用。
        /// </remarks>
        private static string ToCommandText(AxisCommand command)
        {
            return command switch
            {
                AxisCommand.JogPlus => "点动正向",
                AxisCommand.JogMinus => "点动反向",
                AxisCommand.GoHome => "回原点",
                _ => command.ToString()
            };
        }

        /// <summary>
        /// 优先使用缓存读取线圈状态，未命中时回源PLC。
        /// </summary>
        /// By:ChengLei
        /// <param name="address">PLC地址。</param>
        /// <param name="token">取消令牌，用于终止当前异步流程。</param>
        /// <returns>返回线圈状态值。</returns>
        /// <remarks>
        /// 由 RefreshAxisAsync 和 IsJogBlockedByLimitAsync 调用。
        /// </remarks>
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

        /// <summary>
        /// 在互斥锁保护下读取连续寄存器。
        /// </summary>
        /// By:ChengLei
        /// <param name="address">PLC地址。</param>
        /// <param name="length">读取寄存器数量。</param>
        /// <param name="token">取消令牌，用于终止当前异步流程。</param>
        /// <returns>返回寄存器数组。</returns>
        /// <remarks>
        /// 由 ReadRegisterValueWithLockAsync 封装调用。
        /// </remarks>
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

        /// <summary>
        /// 在互斥锁保护下读取单个寄存器。
        /// </summary>
        /// By:ChengLei
        /// <param name="address">PLC地址。</param>
        /// <param name="token">取消令牌，用于终止当前异步流程。</param>
        /// <returns>返回单个寄存器值。</returns>
        /// <remarks>
        /// 由 RefreshAxisAsync 读取位置与速度时调用。
        /// </remarks>
        private async Task<ushort> ReadRegisterValueWithLockAsync(ushort address, CancellationToken token)
        {
            ushort[] values = await ReadRegistersWithLockAsync(address, 1, token);
            return values.Length > 0 ? values[0] : (ushort)0;
        }

        /// <summary>
        /// 在互斥锁保护下写入线圈。
        /// </summary>
        /// By:ChengLei
        /// <param name="address">PLC地址。</param>
        /// <param name="value">待写入或待转换的数值。</param>
        /// <returns>返回线圈写入异步任务。</returns>
        /// <remarks>
        /// 由 ExecuteManualLocateAsync 和 WriteAxisCommandLevelAsync 调用。
        /// </remarks>
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

        /// <summary>
        /// 在互斥锁保护下写入寄存器。
        /// </summary>
        /// By:ChengLei
        /// <param name="address">PLC地址。</param>
        /// <param name="value">待写入或待转换的数值。</param>
        /// <returns>返回寄存器写入异步任务。</returns>
        /// <remarks>
        /// 由 ExecuteManualLocateAsync 与 WriteAxisSpeedAsync 调用。
        /// </remarks>
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

        /// <summary>
        /// 从线圈数组按索引安全读取值。
        /// </summary>
        /// By:ChengLei
        /// <param name="source">源数组。</param>
        /// <param name="index">目标索引。</param>
        /// <returns>返回指定索引线圈值。</returns>
        /// <remarks>
        /// 工具方法，供数组安全读取场景复用。
        /// </remarks>
        private static bool GetCoilValue(bool[] source, int index)
        {
            return index >= 0 && index < source.Length && source[index];
        }

        /// <summary>
        /// 从寄存器数组按索引安全读取值。
        /// </summary>
        /// By:ChengLei
        /// <param name="source">源数组。</param>
        /// <param name="index">目标索引。</param>
        /// <returns>返回指定索引寄存器值。</returns>
        /// <remarks>
        /// 工具方法，供数组安全读取场景复用。
        /// </remarks>
        private static ushort GetRegisterValue(ushort[] source, int index)
        {
            return index >= 0 && index < source.Length ? source[index] : (ushort)0;
        }

        /// <summary>
        /// 将ushort按补码解释为short。
        /// </summary>
        /// By:ChengLei
        /// <param name="value">待写入或待转换的数值。</param>
        /// <returns>返回转换后的short值。</returns>
        /// <remarks>
        /// 由 RefreshAxisAsync 解析速度寄存器时调用。
        /// </remarks>
        private static short ToInt16(ushort value)
        {
            unchecked
            {
                return (short)value;
            }
        }

        /// <summary>
        /// 解析输入文本为Int32，支持小数四舍五入。
        /// </summary>
        /// By:ChengLei
        /// <param name="input">待解析输入文本。</param>
        /// <param name="value">待写入或待转换的数值。</param>
        /// <returns>返回是否成功解析为Int32。</returns>
        /// <remarks>
        /// 由 ExecuteManualLocateAsync 和 TryParseInt16 调用。
        /// </remarks>
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

        /// <summary>
        /// 解析输入文本为Int16并进行范围校验。
        /// </summary>
        /// By:ChengLei
        /// <param name="input">待解析输入文本。</param>
        /// <param name="value">待写入或待转换的数值。</param>
        /// <returns>返回是否成功解析为Int16。</returns>
        /// <remarks>
        /// 由 WriteAxisSpeedAsync 校验输入值时调用。
        /// </remarks>
        private static bool TryParseInt16(string input, out short value)
        {
            value = 0;
            if (!TryParseInt32(input, out int parsed))
            {
                return false;
            }

            if (parsed < short.MinValue || parsed > short.MaxValue)
            {
                return false;
            }

            value = (short)parsed;
            return true;
        }

        /// <summary>
        /// 将高低位寄存器合成为Int32。
        /// </summary>
        /// By:ChengLei
        /// <param name="lowWord">输出低16位寄存器值。</param>
        /// <param name="highWord">输出高16位寄存器值。</param>
        /// <returns>返回组合后的32位整数。</returns>
        /// <remarks>
        /// 由 RefreshAxisAsync 组合当前坐标和目标坐标时调用。
        /// </remarks>
        private static int ComposeInt32(ushort lowWord, ushort highWord)
        {
            int raw = (highWord << 16) | lowWord;
            return raw;
        }

        /// <summary>
        /// 将Int32拆分为低16位和高16位。
        /// </summary>
        /// By:ChengLei
        /// <param name="value">待写入或待转换的数值。</param>
        /// <param name="lowWord">输出低16位寄存器值。</param>
        /// <param name="highWord">输出高16位寄存器值。</param>
        /// <remarks>
        /// 由 ExecuteManualLocateAsync 拆分目标坐标时调用。
        /// </remarks>
        private static void SplitInt32(int value, out ushort lowWord, out ushort highWord)
        {
            unchecked
            {
                lowWord = (ushort)(value & 0xFFFF);
                highWord = (ushort)((value >> 16) & 0xFFFF);
            }
        }

        /// <summary>
        /// 写入带时间戳的动作提示并刷新界面。
        /// </summary>
        /// By:ChengLei
        /// <param name="action">需要在UI线程执行的委托。</param>
        /// <remarks>
        /// 由轴调试各动作节点统一调用，输出提示到页面底部。
        /// </remarks>
        private void SetActionMessage(string action)
        {
            string text = $"{DateTime.Now:HH:mm:ss}  {action}";
            RunOnUiThread(() => ActionMessage = text);
        }

        /// <summary>
        /// 确保指定操作在UI线程执行。
        /// </summary>
        /// By:ChengLei
        /// <param name="action">需要在UI线程执行的委托。</param>
        /// <remarks>
        /// 由 SetActionMessage 和 RefreshAxisAsync 调用。
        /// </remarks>
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

        /// <summary>
        /// 释放轮询资源并停止后台任务。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由页面生命周期结束调用，取消后台轮询并注销点位。
        /// </remarks>
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
            /// <summary>
            /// 初始化轴绑定对象。
            /// </summary>
            /// By:ChengLei
            /// <param name="axisNo">轴编号（1~4）。</param>
            /// <param name="card">轴卡片视图模型。</param>
            /// <param name="addresses">该轴对应的地址映射配置。</param>
            /// <remarks>
            /// 由 BuildLinearAxis 和 BuildShakeAxis 创建对象时调用。
            /// </remarks>
            public AxisBinding(int axisNo, AxisControlCardViewModel card, AxisAddressProfile addresses)
            {
                AxisNo = axisNo;
                Card = card;
                Addresses = addresses ?? new AxisAddressProfile();
            }

            public int AxisNo { get; }
            public AxisControlCardViewModel Card { get; }
            public AxisAddressProfile Addresses { get; }
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
        private string _manualSpeedInput = string.Empty;
        private string _autoSpeedInput = string.Empty;
        private bool _manualSpeedInputDirty;
        private bool _autoSpeedInputDirty;
        private bool _suppressInputDirty;
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

        public string ManualSpeedInput
        {
            get => _manualSpeedInput;
            set
            {
                if (_manualSpeedInput != value)
                {
                    _manualSpeedInput = value;
                    if (!_suppressInputDirty)
                    {
                        _manualSpeedInputDirty = true;
                    }

                    OnPropertyChanged();
                }
            }
        }

        public string AutoSpeedInput
        {
            get => _autoSpeedInput;
            set
            {
                if (_autoSpeedInput != value)
                {
                    _autoSpeedInput = value;
                    if (!_suppressInputDirty)
                    {
                        _autoSpeedInputDirty = true;
                    }

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
        public ICommand? WriteManualSpeedCommand { get; set; }
        public ICommand? WriteAutoSpeedCommand { get; set; }

        /// <summary>
        /// 用PLC速度刷新显示，并在未编辑时同步输入框。
        /// </summary>
        /// By:ChengLei
        /// <param name="manualSpeed">是否操作手动速度；false表示自动速度。</param>
        /// <param name="autoSpeed">PLC读取到的自动速度值。</param>
        /// <remarks>
        /// 由 RefreshAxisAsync 在轮询到新速度时调用。
        /// </remarks>
        public void UpdateSpeedFromPlc(short manualSpeed, short autoSpeed)
        {
            string manualText = manualSpeed.ToString(CultureInfo.InvariantCulture);
            string autoText = autoSpeed.ToString(CultureInfo.InvariantCulture);

            ManualSpeed = manualText;
            AutoSpeed = autoText;

            _suppressInputDirty = true;
            try
            {
                if (!_manualSpeedInputDirty)
                {
                    ManualSpeedInput = manualText;
                }

                if (!_autoSpeedInputDirty)
                {
                    AutoSpeedInput = autoText;
                }
            }
            finally
            {
                _suppressInputDirty = false;
            }
        }

        /// <summary>
        /// 速度写入成功后提交输入状态并同步显示。
        /// </summary>
        /// By:ChengLei
        /// <param name="manualSpeed">是否操作手动速度；false表示自动速度。</param>
        /// <param name="value">待写入或待转换的数值。</param>
        /// <remarks>
        /// 由 WriteAxisSpeedAsync 写入成功后调用。
        /// </remarks>
        public void CommitSpeedInput(bool manualSpeed, short value)
        {
            string text = value.ToString(CultureInfo.InvariantCulture);

            _suppressInputDirty = true;
            try
            {
                if (manualSpeed)
                {
                    _manualSpeedInputDirty = false;
                    ManualSpeedInput = text;
                    ManualSpeed = text;
                }
                else
                {
                    _autoSpeedInputDirty = false;
                    AutoSpeedInput = text;
                    AutoSpeed = text;
                }
            }
            finally
            {
                _suppressInputDirty = false;
            }
        }
    }

    public class AxisStatusLampViewModel : BaseViewModel
    {
        private Brush _color = Brushes.Gainsboro;

        /// <summary>
        /// 初始化状态灯视图模型。
        /// </summary>
        /// By:ChengLei
        /// <param name="name">状态灯显示名称。</param>
        /// <remarks>
        /// 由 BuildLinearAxis 和 BuildShakeAxis 初始化状态灯时调用。
        /// </remarks>
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

