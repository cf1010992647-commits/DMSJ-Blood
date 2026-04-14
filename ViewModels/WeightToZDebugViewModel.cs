using Blood_Alcohol.Communication.Serial;
using Blood_Alcohol.Models;
using Blood_Alcohol.Services;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Blood_Alcohol.ViewModels
{
    /// <summary>
    /// 重量系数标定视图模型，负责重量到Z轴与重量到微升系数计算。
    /// </summary>
    /// By:ChengLei
    /// <remarks>
    /// 由 WeightToZDebugView 创建为 DataContext，联动天平TCP读取与PLC坐标下发。
    /// </remarks>
    public class WeightToZDebugViewModel : BaseViewModel
    {
        private const string WeightToZConfigFileName = "WeightToZCalibrationConfig.json";
        private const string CoordinateConfigFileName = "CoordinateDebugConfig.json";
        private const int ZCurrentPositionLowAddress = 1202;

        private readonly ConfigService<WeightToZCalibrationConfig> _weightConfigService;
        private readonly ConfigService<CoordinateDebugConfig> _coordinateConfigService;
        private readonly Lx5vPlc _plc;
        private readonly SemaphoreSlim _plcLock;
        private readonly SemaphoreSlim _tcpReceiveLock = CommunicationManager.TcpReceiveLock;

        private string _statusMessage = "重量->Z 坐标调试已加载。";
        private int _zAddress = ZCurrentPositionLowAddress;
        private double _zScale = 100;
        private double _currentWeight;
        private double _currentZ;
        private double _zPerWeight;
        private bool _hasCoefficient;
        private bool _hasCurrentWeightSample;
        private bool _hasCurrentZSample;
        private double _inputMicroliter;
        private double _microliterPerWeight;
        private bool _hasMicroliterCoefficient;

        /// <summary>
        /// 初始化重量到Z轴标定视图模型并绑定调试命令。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由 WeightToZDebugView 创建为 DataContext，构造后会加载地址与历史标定配置。
        /// </remarks>
        public WeightToZDebugViewModel()
        {
            _weightConfigService = new ConfigService<WeightToZCalibrationConfig>(WeightToZConfigFileName);
            _coordinateConfigService = new ConfigService<CoordinateDebugConfig>(CoordinateConfigFileName);
            _plc = CommunicationManager.Plc;
            _plcLock = CommunicationManager.PlcAccessLock;

            ReadCurrentWeightCommand = new RelayCommand(_ => _ = ReadCurrentWeightAsync());
            ReadCurrentZCommand = new RelayCommand(_ => _ = ReadCurrentZAsync());
            AcquireAndComputeCoefficientCommand = new RelayCommand(_ => _ = AcquireAndComputeCoefficientAsync());
            ComputeCoefficientCommand = new RelayCommand(_ => ComputeCoefficientFromCurrent(), _ => CanComputeCoefficient());
            ComputeMicroliterCoefficientCommand = new RelayCommand(_ => ComputeMicroliterCoefficientFromCurrent(), _ => CanComputeMicroliterCoefficient());
            SaveConfigCommand = new RelayCommand(_ => SaveConfig());
            LoadConfigCommand = new RelayCommand(_ => LoadConfig());

            ReloadAddressFromCoordinateConfig();
            LoadConfig();
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

        public int ZAddress
        {
            get => _zAddress;
            private set
            {
                if (_zAddress != value)
                {
                    _zAddress = value;
                    OnPropertyChanged();
                }
            }
        }

        public double ZScale
        {
            get => _zScale;
            private set
            {
                if (Math.Abs(_zScale - value) > 0.000001d)
                {
                    _zScale = value;
                    OnPropertyChanged();
                }
            }
        }

        public double CurrentWeight
        {
            get => _currentWeight;
            set
            {
                if (Math.Abs(_currentWeight - value) > 0.000001d)
                {
                    _currentWeight = value;
                    OnPropertyChanged();
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public double CurrentZ
        {
            get => _currentZ;
            private set
            {
                if (Math.Abs(_currentZ - value) > 0.000001d)
                {
                    _currentZ = value;
                    OnPropertyChanged();
                }
            }
        }

        public double ZPerWeight
        {
            get => _zPerWeight;
            private set
            {
                if (Math.Abs(_zPerWeight - value) > 0.000001d)
                {
                    _zPerWeight = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool HasCoefficient
        {
            get => _hasCoefficient;
            private set
            {
                if (_hasCoefficient != value)
                {
                    _hasCoefficient = value;
                    OnPropertyChanged();
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public double InputMicroliter
        {
            get => _inputMicroliter;
            set
            {
                if (Math.Abs(_inputMicroliter - value) > 0.000001d)
                {
                    _inputMicroliter = value;
                    OnPropertyChanged();
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public double MicroliterPerWeight
        {
            get => _microliterPerWeight;
            private set
            {
                if (Math.Abs(_microliterPerWeight - value) > 0.000001d)
                {
                    _microliterPerWeight = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool HasMicroliterCoefficient
        {
            get => _hasMicroliterCoefficient;
            private set
            {
                if (_hasMicroliterCoefficient != value)
                {
                    _hasMicroliterCoefficient = value;
                    OnPropertyChanged();
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public ICommand ReadCurrentZCommand { get; }
        public ICommand ReadCurrentWeightCommand { get; }
        public ICommand AcquireAndComputeCoefficientCommand { get; }
        public ICommand ComputeCoefficientCommand { get; }
        public ICommand ComputeMicroliterCoefficientCommand { get; }
        public ICommand SaveConfigCommand { get; }
        public ICommand LoadConfigCommand { get; }

        /// <summary>
        /// 从坐标配置加载当前Z地址与比例。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由构造函数与读取Z坐标流程调用，保持地址与坐标调试配置一致。
        /// </remarks>
        private void ReloadAddressFromCoordinateConfig()
        {
            try
            {
                CoordinateDebugConfig cfg = _coordinateConfigService.Load() ?? new CoordinateDebugConfig();
                ZCoordinateProfileSettings zCfg = cfg.ZAxis ?? new ZCoordinateProfileSettings();

                ZAddress = ResolveCurrentZAddress(zCfg);
                ZScale = zCfg.Scale > 0 ? zCfg.Scale : 100;
            }
            catch
            {
                ZAddress = ZCurrentPositionLowAddress;
                ZScale = 100;
            }
        }

        /// <summary>
        /// 解析用于读取当前Z坐标的寄存器低位地址。
        /// </summary>
        /// By:ChengLei
        /// <param name="settings">Z轴坐标配置对象。</param>
        /// <returns>返回可用于读取当前Z坐标的低位地址。</returns>
        /// <remarks>
        /// 由 ReloadAddressFromCoordinateConfig 调用。
        /// </remarks>
        private static int ResolveCurrentZAddress(ZCoordinateProfileSettings settings)
        {
            if (settings == null)
            {
                return ZCurrentPositionLowAddress;
            }

            if (settings.CurrentZAddress > 0 && settings.CurrentZAddress != settings.ZStartAddress)
            {
                return settings.CurrentZAddress;
            }

            return ZCurrentPositionLowAddress;
        }

        /// <summary>
        /// 读取当前Z轴坐标并保存为标定样本。
        /// </summary>
        /// By:ChengLei
        /// <returns>返回读取当前Z坐标异步任务。</returns>
        /// <remarks>
        /// 由获取当前坐标按钮和一键采集流程调用。
        /// </remarks>
        private async Task ReadCurrentZAsync()
        {
            try
            {
                ReloadAddressFromCoordinateConfig();
                if (!CommunicationManager.Is485Open)
                {
                    StatusMessage = $"{DateTime.Now:HH:mm:ss} 读取失败：RS485 未连接。";
                    return;
                }

                ushort address = EnsureLowAddress(ZAddress, "Z地址");
                int raw = await ReadInt32AtAddressAsync(address);
                CurrentZ = FromPlcRaw(raw);
                _hasCurrentZSample = true;
                StatusMessage = $"{DateTime.Now:HH:mm:ss} 已获取当前坐标：D{ZAddress}/D{ZAddress + 1} -> Z={CurrentZ:F3} mm";
                CommandManager.InvalidateRequerySuggested();
            }
            catch (Exception ex)
            {
                _hasCurrentZSample = false;
                StatusMessage = $"{DateTime.Now:HH:mm:ss} 读取当前Z失败：{ex.Message}";
                CommandManager.InvalidateRequerySuggested();
            }
        }

        /// <summary>
        /// 读取当前天平重量并保存为标定样本。
        /// </summary>
        /// By:ChengLei
        /// <returns>返回读取当前重量异步任务。</returns>
        /// <remarks>
        /// 由获取当前重量按钮调用。
        /// </remarks>
        private async Task ReadCurrentWeightAsync()
        {
            try
            {
                double weight = await ReadBalanceWeightAsync(CancellationToken.None);
                CurrentWeight = weight;
                _hasCurrentWeightSample = true;
                StatusMessage = $"{DateTime.Now:HH:mm:ss} 已获取当前重量：{CurrentWeight:F3} g";
                CommandManager.InvalidateRequerySuggested();
            }
            catch (Exception ex)
            {
                _hasCurrentWeightSample = false;
                StatusMessage = $"{DateTime.Now:HH:mm:ss} 获取当前重量失败：{ex.Message}";
                CommandManager.InvalidateRequerySuggested();
            }
        }

        /// <summary>
        /// 一键采集重量与坐标并计算系数。
        /// </summary>
        /// By:ChengLei
        /// <returns>返回一键采集并计算异步任务。</returns>
        /// <remarks>
        /// 由一键采集并计算按钮调用。
        /// </remarks>
        private async Task AcquireAndComputeCoefficientAsync()
        {
            try
            {
                double weight = await ReadBalanceWeightAsync(CancellationToken.None);
                CurrentWeight = weight;
                _hasCurrentWeightSample = true;

                await ReadCurrentZAsync();
                if (!_hasCurrentZSample)
                {
                    return;
                }

                ComputeCoefficientFromCurrent();
            }
            catch (Exception ex)
            {
                StatusMessage = $"{DateTime.Now:HH:mm:ss} 一键采集失败：{ex.Message}";
            }
        }

        /// <summary>
        /// 判断当前样本是否满足系数计算条件。
        /// </summary>
        /// By:ChengLei
        /// <returns>返回是否满足系数计算条件。</returns>
        /// <remarks>
        /// 由计算系数命令的可执行条件调用。
        /// </remarks>
        private bool CanComputeCoefficient()
        {
            return _hasCurrentWeightSample && _hasCurrentZSample && CurrentWeight > 0;
        }

        /// <summary>
        /// 判断当前样本是否满足重量到微升系数计算条件。
        /// </summary>
        /// By:ChengLei
        /// <returns>返回是否满足重量到微升系数计算条件。</returns>
        /// <remarks>
        /// 由计算重量转微升系数命令的可执行条件调用。
        /// </remarks>
        private bool CanComputeMicroliterCoefficient()
        {
            return _hasCurrentWeightSample && CurrentWeight > 0 && InputMicroliter > 0;
        }

        /// <summary>
        /// 根据当前重量与坐标计算重量到Z的系数。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由手动计算按钮和一键采集流程调用。
        /// </remarks>
        private void ComputeCoefficientFromCurrent()
        {
            if (!_hasCurrentWeightSample || !_hasCurrentZSample)
            {
                StatusMessage = $"{DateTime.Now:HH:mm:ss} 请先获取当前重量和当前坐标，再计算系数。";
                return;
            }

            if (CurrentWeight <= 0)
            {
                StatusMessage = $"{DateTime.Now:HH:mm:ss} 当前重量必须大于 0。";
                return;
            }

            ZPerWeight = CurrentZ / CurrentWeight;
            HasCoefficient = true;
            StatusMessage = $"{DateTime.Now:HH:mm:ss} 系数已更新：k={ZPerWeight:F6} (mm/g)，标定点 重量={CurrentWeight:F3}g, Z={CurrentZ:F3}mm";
        }

        /// <summary>
        /// 根据当前重量和手动输入微升计算重量到微升系数。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由计算重量转微升系数按钮调用。
        /// </remarks>
        private void ComputeMicroliterCoefficientFromCurrent()
        {
            if (!_hasCurrentWeightSample)
            {
                StatusMessage = $"{DateTime.Now:HH:mm:ss} 请先获取当前重量。";
                return;
            }

            if (CurrentWeight <= 0)
            {
                StatusMessage = $"{DateTime.Now:HH:mm:ss} 当前重量必须大于 0。";
                return;
            }

            if (InputMicroliter <= 0)
            {
                StatusMessage = $"{DateTime.Now:HH:mm:ss} 微升输入必须大于 0。";
                return;
            }

            MicroliterPerWeight = InputMicroliter / CurrentWeight;
            HasMicroliterCoefficient = true;
            StatusMessage = $"{DateTime.Now:HH:mm:ss} 微升系数已更新：k={MicroliterPerWeight:F6} (ul/g)，标定点 重量={CurrentWeight:F3}g, 微升={InputMicroliter:F3}ul";
        }

        /// <summary>
        /// 保存重量到Z标定参数到配置文件。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由保存配置按钮调用。
        /// </remarks>
        private void SaveConfig()
        {
            try
            {
                WeightToZCalibrationConfig cfg = new()
                {
                    CurrentWeight = CurrentWeight,
                    CurrentZ = CurrentZ,
                    ZPerWeight = ZPerWeight,
                    HasCoefficient = HasCoefficient,
                    InputMicroliter = InputMicroliter,
                    MicroliterPerWeight = MicroliterPerWeight,
                    HasMicroliterCoefficient = HasMicroliterCoefficient
                };

                _weightConfigService.Save(cfg);
                StatusMessage = $"{DateTime.Now:HH:mm:ss} 系数配置已保存。";
            }
            catch (Exception ex)
            {
                StatusMessage = $"{DateTime.Now:HH:mm:ss} 保存失败：{ex.Message}";
            }
        }

        /// <summary>
        /// 加载重量到Z标定参数并恢复界面状态。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由构造函数和加载配置按钮调用。
        /// </remarks>
        private void LoadConfig()
        {
            try
            {
                WeightToZCalibrationConfig cfg = _weightConfigService.Load() ?? new WeightToZCalibrationConfig();
                CurrentWeight = cfg.CurrentWeight;
                CurrentZ = cfg.CurrentZ;
                InputMicroliter = cfg.InputMicroliter;

                if (cfg.HasCoefficient && Math.Abs(cfg.ZPerWeight) > 0.0000001d)
                {
                    ZPerWeight = cfg.ZPerWeight;
                    HasCoefficient = true;
                }
                else
                {
                    HasCoefficient = false;
                    ZPerWeight = 0;
                }

                if (cfg.HasMicroliterCoefficient && Math.Abs(cfg.MicroliterPerWeight) > 0.0000001d)
                {
                    MicroliterPerWeight = cfg.MicroliterPerWeight;
                    HasMicroliterCoefficient = true;
                }
                else
                {
                    MicroliterPerWeight = 0;
                    HasMicroliterCoefficient = false;
                }

                _hasCurrentWeightSample = false;
                _hasCurrentZSample = false;
                StatusMessage = $"{DateTime.Now:HH:mm:ss} 系数配置已加载。";
                CommandManager.InvalidateRequerySuggested();
            }
            catch (Exception ex)
            {
                StatusMessage = $"{DateTime.Now:HH:mm:ss} 加载失败：{ex.Message}";
            }
        }

        /// <summary>
        /// 通过TCP向天平读取当前重量。
        /// </summary>
        /// By:ChengLei
        /// <param name="token">取消令牌，用于终止当前异步流程。</param>
        /// <returns>返回读取到的重量值。</returns>
        /// <remarks>
        /// 由读取重量和一键采集流程调用。
        /// </remarks>
        private async Task<double> ReadBalanceWeightAsync(CancellationToken token)
        {
            int port = CommunicationManager.GetPort("天平");
            EnsureTcpPortConnected(port, "天平");
            await _tcpReceiveLock.WaitAsync(token);
            try
            {
                await DrainStaleTcpFramesAsync(port, token);
                await CommunicationManager.TcpServer.SendToPort(port, CommunicationManager.Balance.GetAllCommand());
                byte[] response = await ReceiveValidBalanceAllResponseAsync(port, TimeSpan.FromSeconds(5), token);
                return CommunicationManager.Balance.ReadWeight(response);
            }
            finally
            {
                _tcpReceiveLock.Release();
            }
        }

        /// <summary>
        /// 校验TCP服务及设备端口连接状态。
        /// </summary>
        /// By:ChengLei
        /// <param name="port">TCP设备端口号。</param>
        /// <param name="deviceName">设备名称，用于异常提示。</param>
        /// <remarks>
        /// 由 ReadBalanceWeightAsync 调用。
        /// </remarks>
        private static void EnsureTcpPortConnected(int port, string deviceName)
        {
            if (!CommunicationManager.IsTcpRunning)
            {
                throw new InvalidOperationException("TCP 服务未启动。");
            }

            if (!CommunicationManager.TcpServer.GetConnectedPorts().Contains(port))
            {
                throw new InvalidOperationException($"{deviceName} 端口未连接：{port}");
            }
        }

        /// <summary>
        /// 在超时时间内接收一次TCP报文。
        /// </summary>
        /// By:ChengLei
        /// <param name="port">TCP设备端口号。</param>
        /// <param name="timeout">本次等待超时时长。</param>
        /// <param name="token">取消令牌，用于终止当前异步流程。</param>
        /// <returns>返回接收到的原始报文。</returns>
        /// <remarks>
        /// 由缓存清理与有效报文接收流程调用。
        /// </remarks>
        private static async Task<byte[]> ReceiveOnceWithTimeoutAsync(int port, TimeSpan timeout, CancellationToken token)
        {
            using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeoutCts.CancelAfter(timeout);
            try
            {
                return await CommunicationManager.TcpServer.ReceiveOnceFromPortAsync(port, timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested && timeoutCts.IsCancellationRequested)
            {
                throw new TimeoutException($"等待天平数据超时（{timeout.TotalSeconds:F0}s）。");
            }
        }

        /// <summary>
        /// 清理端口缓存中的历史TCP报文。
        /// </summary>
        /// By:ChengLei
        /// <param name="port">TCP设备端口号。</param>
        /// <param name="token">取消令牌，用于终止当前异步流程。</param>
        /// <returns>返回清理缓存异步任务。</returns>
        /// <remarks>
        /// 由 ReadBalanceWeightAsync 在发送新命令前调用。
        /// </remarks>
        private static async Task DrainStaleTcpFramesAsync(int port, CancellationToken token)
        {
            for (int i = 0; i < 4; i++)
            {
                try
                {
                    _ = await ReceiveOnceWithTimeoutAsync(port, TimeSpan.FromMilliseconds(60), token);
                }
                catch (TimeoutException)
                {
                    break;
                }
            }
        }

        /// <summary>
        /// 循环接收并筛选有效天平响应。
        /// </summary>
        /// By:ChengLei
        /// <param name="port">TCP设备端口号。</param>
        /// <param name="timeout">本次等待超时时长。</param>
        /// <param name="token">取消令牌，用于终止当前异步流程。</param>
        /// <returns>返回通过校验的天平响应报文。</returns>
        /// <remarks>
        /// 由 ReadBalanceWeightAsync 调用。
        /// </remarks>
        private static async Task<byte[]> ReceiveValidBalanceAllResponseAsync(int port, TimeSpan timeout, CancellationToken token)
        {
            DateTime deadline = DateTime.UtcNow + timeout;
            while (true)
            {
                TimeSpan remain = deadline - DateTime.UtcNow;
                if (remain <= TimeSpan.Zero)
                {
                    throw new TimeoutException($"等待天平重量数据超时（{timeout.TotalSeconds:F0}s）。");
                }

                byte[] response = await ReceiveOnceWithTimeoutAsync(port, remain, token);
                if (IsBalanceAllResponse(response))
                {
                    return response;
                }
            }
        }

        /// <summary>
        /// 判断报文是否为可解析的天平全量响应。
        /// </summary>
        /// By:ChengLei
        /// <param name="response">待校验的原始响应报文。</param>
        /// <returns>返回是否为有效天平响应。</returns>
        /// <remarks>
        /// 由 ReceiveValidBalanceAllResponseAsync 判定报文有效性。
        /// </remarks>
        private static bool IsBalanceAllResponse(byte[] response)
        {
            return response.Length >= 13 && response[0] == 1 && response[1] == 3 && response[2] >= 8;
        }

        /// <summary>
        /// 将PLC原始整数值转换为Z轴工程值。
        /// </summary>
        /// By:ChengLei
        /// <param name="raw">PLC原始整数值。</param>
        /// <returns>返回转换后的Z轴工程值。</returns>
        /// <remarks>
        /// 由读取寄存器后转换坐标值时调用。
        /// </remarks>
        private double FromPlcRaw(int raw)
        {
            if (Math.Abs(ZScale) < 0.0000001d)
            {
                throw new InvalidOperationException("Z比例不能为0。");
            }

            return raw / ZScale;
        }

        /// <summary>
        /// 校验并转换双寄存器低位地址。
        /// </summary>
        /// By:ChengLei
        /// <param name="address">PLC地址或地址文本。</param>
        /// <param name="fieldName">字段名称，用于异常提示。</param>
        /// <returns>返回校验后的低位地址。</returns>
        /// <remarks>
        /// 由读取Z地址前调用，保证双寄存器寻址合法。
        /// </remarks>
        private static ushort EnsureLowAddress(int address, string fieldName)
        {
            if (address < 0 || address > ushort.MaxValue - 1)
            {
                throw new InvalidOperationException($"{fieldName}超出范围(0-65534)：{address}");
            }

            return (ushort)address;
        }

        /// <summary>
        /// 将高低位寄存器组合为32位整数。
        /// </summary>
        /// By:ChengLei
        /// <param name="lowWord">低16位寄存器值。</param>
        /// <param name="highWord">高16位寄存器值。</param>
        /// <returns>返回组合后的32位整数。</returns>
        /// <remarks>
        /// 由 ReadInt32AtAddressAsync 解析寄存器值时调用。
        /// </remarks>
        private static int ComposeInt32(ushort lowWord, ushort highWord)
        {
            uint raw = ((uint)highWord << 16) | lowWord;
            return unchecked((int)raw);
        }

        /// <summary>
        /// 读取指定低位地址的32位寄存器值。
        /// </summary>
        /// By:ChengLei
        /// <param name="lowAddress">双寄存器低位地址。</param>
        /// <returns>返回读取到的32位整数值。</returns>
        /// <remarks>
        /// 由 ReadCurrentZAsync 调用。
        /// </remarks>
        private async Task<int> ReadInt32AtAddressAsync(ushort lowAddress)
        {
            await _plcLock.WaitAsync();
            try
            {
                var read = await _plc.TryReadHoldingRegistersAsync(lowAddress, 2);
                if (!read.Success)
                {
                    throw new InvalidOperationException(read.Error);
                }

                ushort[] regs = read.Values;
                if (regs.Length < 2)
                {
                    throw new InvalidOperationException("PLC 返回寄存器数量不足。");
                }

                return ComposeInt32(regs[0], regs[1]);
            }
            finally
            {
                _plcLock.Release();
            }
        }

    }

}
