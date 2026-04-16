using Blood_Alcohol.Models;
using Blood_Alcohol.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using static Blood_Alcohol.Services.CommunicationManager;

namespace Blood_Alcohol.ViewModels
{
    /// <summary>
    /// 通信页日志项模型。
    /// </summary>
    /// By:ChengLei
    /// <remarks>
    /// 由 CommunicationViewModel.Log 方法创建并绑定到页面日志列表。
    /// </remarks>
    public class LogItem
    {
        public string Time { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public Brush Color { get; set; } = Brushes.DarkSlateGray;
    }

    /// <summary>
    /// 通信配置与通信测试页面视图模型。
    /// </summary>
    /// By:ChengLei
    /// <remarks>
    /// 由 CommunicationView 创建并作为 DataContext，负责485/TCP连接、设备映射和联机测试。
    /// </remarks>
    public class CommunicationViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// 触发属性变更通知。
        /// </summary>
        /// By:ChengLei
        /// <param name="prop">发生变更的属性名。</param>
        /// <remarks>
        /// 由本类各属性 setter 调用，驱动界面绑定刷新。
        /// </remarks>
        private void Raise(string prop) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        private readonly SemaphoreSlim _tcpReceiveLock = CommunicationManager.TcpReceiveLock;

        private readonly ConfigService<CommunicationSettings> _configService;

        private CommunicationSettings _settings;

        private ObservableCollection<TcpDeviceMapping> _tcpDevices = new();
        public ObservableCollection<TcpDeviceMapping> TcpDevices
        {
            get => _tcpDevices;
            set
            {
                _tcpDevices = value;
                Raise(nameof(TcpDevices));
            }
        }

        public ObservableCollection<string> AvailableComPorts { get; }
            = new ObservableCollection<string>();

        public ObservableCollection<int> AvailableBaudRates { get; }
            = new ObservableCollection<int>
            {
                1200,
                2400,
                4800,
                9600,
                19200,
                38400,
                57600,
                115200
            };

        private string _selectedComPort = string.Empty;
        public string SelectedComPort
        {
            get => _selectedComPort;
            set
            {
                _selectedComPort = value;
                Raise(nameof(SelectedComPort));
            }
        }

        private int _selectedBaudRate = 9600;
        public int SelectedBaudRate
        {
            get => _selectedBaudRate;
            set
            {
                EnsureBaudRateOption(value);
                _selectedBaudRate = value;
                Raise(nameof(SelectedBaudRate));
            }
        }

        private int _tcpPort;
        public int TcpPort
        {
            get => _tcpPort;
            set
            {
                _tcpPort = value;
                Raise(nameof(TcpPort));
            }
        }

        private string _tcpButtonText = "启动服务";
        public string TcpButtonText
        {
            get => _tcpButtonText;
            set
            {
                _tcpButtonText = value;
                Raise(nameof(TcpButtonText));
            }
        }

        private Brush _tcpStatusColor = Brushes.Red;
        public Brush TcpStatusColor
        {
            get => _tcpStatusColor;
            set
            {
                _tcpStatusColor = value;
                Raise(nameof(TcpStatusColor));
            }
        }

        private string _rs485ButtonText = "连接";
        public string Rs485ButtonText
        {
            get => _rs485ButtonText;
            set
            {
                _rs485ButtonText = value;
                Raise(nameof(Rs485ButtonText));
            }
        }

        private Brush _rs485StatusColor = Brushes.Red;
        public Brush Rs485StatusColor
        {
            get => _rs485StatusColor;
            set
            {
                _rs485StatusColor = value;
                Raise(nameof(Rs485StatusColor));
            }
        }

        public ICommand ToggleRs485Command { get; }
        public ICommand RefreshComPortsCommand { get; }
        public ICommand ToggleTcpCommand { get; }
        public ICommand SaveTcpConfigCommand { get; }
        public ICommand TestTemperatureCommand { get; }
        public ICommand TestWeightCommand { get; }

        public ObservableCollection<LogItem> Logs { get; }
            = new ObservableCollection<LogItem>();

        /// <summary>
        /// 向通信页日志列表追加一条日志。
        /// </summary>
        /// By:ChengLei
        /// <param name="msg">日志文本。</param>
        /// <param name="color">日志颜色，为空时使用默认颜色。</param>
        /// <remarks>
        /// 由本类所有通信操作调用；在UI线程中写入并限制最大日志条数。
        /// </remarks>
        private void Log(string msg, Brush? color = null)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Logs.Add(new LogItem
                {
                    Time = DateTime.Now.ToString("HH:mm:ss"),
                    Message = msg,
                    Color = color ?? Brushes.DarkSlateGray
                });

                if (Logs.Count > 300)
                    Logs.RemoveAt(0);
            });
        }

        /// <summary>
        /// 初始化通信视图模型并加载通信配置。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由 CommunicationView 创建时调用，完成命令绑定、状态初始化与通信事件订阅。
        /// </remarks>
        public CommunicationViewModel()
        {
            _configService = new ConfigService<CommunicationSettings>(CommunicationManager.CommunicationConfigFileName);
            ToggleRs485Command = new RelayCommand(_ => ToggleRs485());
            RefreshComPortsCommand = new RelayCommand(_ => RefreshComPorts());
            ToggleTcpCommand = new RelayCommand(_ => ToggleTcp());
            SaveTcpConfigCommand = new RelayCommand(_ => SaveTcpConfig());
            TestTemperatureCommand = new RelayCommand(async _ => await TestTemperature());
            TestWeightCommand = new RelayCommand(async _ => await TestWeight());

            _settings = _configService.Load();

            TcpDevices = new ObservableCollection<TcpDeviceMapping>();

            TcpPort = _settings.TcpPort;
            SelectedComPort = _settings.ComPort;
            SelectedBaudRate = _settings.BaudRate;
            RefreshTcpDeviceDisplay();

            RefreshComPorts();
            UpdateRs485Status();
            UpdateTcpStatus();

            // 状态变化同步
            CommunicationManager.OnStateChanged += () =>
            {
                UpdateRs485Status();
                UpdateTcpStatus();
            };

            // TCP日志
            CommunicationManager.TcpServer.OnMessageReceived += msg =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Log($"[TCP] {msg}");
                });
            };

            CommunicationManager.TcpServer.OnClientConnected += msg =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Log($"[TCP] {msg}");
                    RefreshTcpDeviceDisplay();
                });
            };
        }

        /// <summary>
        /// 保存当前通信配置到本地配置文件。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由通信页保存动作调用，同时保存 TCP 监听端口、串口参数和设备 IP 端口映射。
        /// </remarks>
        public void SaveTcpConfig()
        {
            _settings.TcpPort = TcpPort;
            _settings.ComPort = SelectedComPort;
            _settings.BaudRate = SelectedBaudRate;
            _settings.TcpDevices = BuildTcpDeviceMappingsForSave();
            CommunicationManager.Settings = _settings;
            CommunicationManager.SaveSettings();

            Log("配置已保存", Brushes.DarkGoldenrod);
        }

        /// <summary>
        /// 生成用于保存的 TCP 设备映射列表。
        /// </summary>
        /// By:ChengLei
        /// <returns>返回清理空白值后的 TCP 设备映射列表。</returns>
        /// <remarks>
        /// 由 SaveTcpConfig 调用，把界面编辑结果写回配置并将空 IP 文本规范为空值。
        /// </remarks>
        private List<TcpDeviceMapping> BuildTcpDeviceMappingsForSave()
        {
            return TcpDevices
                .Where(x => x.Port > 0 || !string.IsNullOrWhiteSpace(x.DeviceType) || !string.IsNullOrWhiteSpace(x.DeviceKey) || !string.IsNullOrWhiteSpace(x.ClientIp))
                .Select(x => new TcpDeviceMapping
                {
                    Port = x.Port,
                    DeviceType = string.IsNullOrWhiteSpace(x.DeviceType) ? "待定" : x.DeviceType.Trim(),
                    DeviceKey = string.IsNullOrWhiteSpace(x.DeviceKey)
                        ? (string.IsNullOrWhiteSpace(x.DeviceType) ? "待定" : x.DeviceType.Trim())
                        : x.DeviceKey.Trim(),
                    ClientIp = NormalizeOptionalText(x.ClientIp)
                })
                .ToList();
        }

        /// <summary>
        /// 规范可选文本配置值。
        /// </summary>
        /// By:ChengLei
        /// <param name="value">界面输入的文本。</param>
        /// <returns>返回去除空白后的文本，空白输入返回空值。</returns>
        /// <remarks>
        /// 用于避免配置文件中保存无意义的空字符串。
        /// </remarks>
        private static string? NormalizeOptionalText(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        /// <summary>
        /// 按设备类型获取对应的TCP设备映射。
        /// </summary>
        /// By:ChengLei
        /// <param name="deviceType">设备类型名称（如温控、天平）。</param>
        /// <returns>返回配置中的设备映射，未配置时返回空。</returns>
        /// <remarks>
        /// 由 TestTemperature 与 TestWeight 调用，用于确定发送目标端口。
        /// </remarks>
        private TcpDeviceMapping? GetDeviceMapping(string deviceType)
        {
            var device = TcpDevices
                .FirstOrDefault(x => x.DeviceType == deviceType);

            return device;
        }

        /// <summary>
        /// 刷新 TCP 设备映射编辑列表。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由构造函数和客户端连接事件调用，用于展示并编辑配置中的 DeviceKey 绑定信息。
        /// </remarks>
        public void RefreshTcpDeviceDisplay()
        {
            TcpDevices.Clear();

            foreach (TcpDeviceMapping device in _settings.TcpDevices)
            {
                TcpDevices.Add(new TcpDeviceMapping
                {
                    Port = device.Port,
                    DeviceType = device.DeviceType,
                    DeviceKey = device.DeviceKey,
                    ClientIp = device.ClientIp
                });
            }

            Raise(nameof(TcpDevices));
        }

        /// <summary>
        /// 刷新本机可用串口列表并更新当前选择。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由“刷新串口”按钮和构造函数调用。
        /// </remarks>
        public void RefreshComPorts()
        {
            AvailableComPorts.Clear();

            var ports = SerialPort.GetPortNames();
            Array.Sort(ports);

            foreach (var port in ports)
                AvailableComPorts.Add(port);

            if (AvailableComPorts.Count > 0)
                SelectedComPort = AvailableComPorts[0];

            Log("串口列表已刷新");
        }

        /// <summary>
        /// 发送天平读数命令并解析重量值。
        /// </summary>
        /// By:ChengLei
        /// <returns>返回重量测试异步任务。</returns>
        /// <remarks>
        /// 由“测试天平”按钮调用；流程包含端口检查、清理旧帧、发送命令与响应校验。
        /// </remarks>
        public async Task TestWeight()
        {
            try
            {
                Log("开始读取重量...");

                var mapping = GetDeviceMapping("天平");

                if (mapping == null)
                {
                    Log("未找到天平设备端口配置", Brushes.Red);
                    return;
                }

                if (!CommunicationManager.IsTcpRunning)
                {
                    Log("TCP服务未启动", Brushes.Red);
                    return;
                }

                if (!CommunicationManager.TcpServer.IsDeviceConnected(mapping.DeviceKey))
                {
                    Log($"天平未连接: {mapping.DeviceKey}", Brushes.Red);
                    return;
                }

                await _tcpReceiveLock.WaitAsync();
                try
                {
                    await DrainStaleTcpFramesAsync(mapping.DeviceKey);
                    await CommunicationManager.TcpServer.SendToDeviceAsync(
                        mapping.DeviceKey,
                        CommunicationManager.Balance.GetAllCommand());

                    byte[] response = await ReceiveValidBalanceAllResponseAsync(
                        mapping.DeviceKey,
                        TimeSpan.FromSeconds(5));

                    double weight = CommunicationManager.Balance.ReadWeight(response);
                    Log($"实际重量: {weight:F2}");
                }
                finally
                {
                    _tcpReceiveLock.Release();
                }

            }
            catch (Exception ex)
            {
                Log($"读取重量失败: {ex.Message}", Brushes.Red);
            }
        }

        /// <summary>
        /// 在超时时间内接收一次TCP报文。
        /// </summary>
        /// By:ChengLei
        /// <param name="deviceKey">逻辑设备键。</param>
        /// <param name="timeout">本次接收超时时长。</param>
        /// <returns>返回接收到的原始报文。</returns>
        /// <remarks>
        /// 由 DrainStaleTcpFramesAsync 与 ReceiveValidBalanceAllResponseAsync 调用。
        /// </remarks>
        private static async Task<byte[]> ReceiveOnceWithTimeoutAsync(string deviceKey, TimeSpan timeout)
        {
            using var cts = new CancellationTokenSource(timeout);
            try
            {
                return await CommunicationManager.TcpServer.ReceiveOnceFromDeviceAsync(deviceKey, cts.Token);
            }
            catch (OperationCanceledException)
            {
                if (cts.IsCancellationRequested)
                {
                    throw new TimeoutException($"TCP接收超时（{timeout.TotalSeconds:F0}s）。");
                }

                throw;
            }
        }

        /// <summary>
        /// 清理端口中可能残留的历史TCP报文。
        /// </summary>
        /// By:ChengLei
        /// <param name="deviceKey">逻辑设备键。</param>
        /// <returns>返回清理动作异步任务。</returns>
        /// <remarks>
        /// 由 TestWeight 在发送新命令前调用，降低旧帧干扰解析的风险。
        /// </remarks>
        private static async Task DrainStaleTcpFramesAsync(string deviceKey)
        {
            for (int i = 0; i < 4; i++)
            {
                try
                {
                    _ = await ReceiveOnceWithTimeoutAsync(deviceKey, TimeSpan.FromMilliseconds(60));
                }
                catch (TimeoutException)
                {
                    break;
                }
            }
        }

        /// <summary>
        /// 在限定时长内循环接收并筛选天平有效响应。
        /// </summary>
        /// By:ChengLei
        /// <param name="deviceKey">逻辑设备键。</param>
        /// <param name="timeout">等待有效响应的总超时时长。</param>
        /// <returns>返回通过校验的天平全量响应报文。</returns>
        /// <remarks>
        /// 由 TestWeight 调用，内部依赖 IsBalanceAllResponse 判定报文有效性。
        /// </remarks>
        private static async Task<byte[]> ReceiveValidBalanceAllResponseAsync(string deviceKey, TimeSpan timeout)
        {
            DateTime deadline = DateTime.UtcNow + timeout;
            while (true)
            {
                TimeSpan remain = deadline - DateTime.UtcNow;
                if (remain <= TimeSpan.Zero)
                {
                    throw new TimeoutException($"等待天平重量数据超时（{timeout.TotalSeconds:F0}s）。");
                }

                byte[] response = await ReceiveOnceWithTimeoutAsync(deviceKey, remain);
                if (IsBalanceAllResponse(response))
                {
                    return response;
                }
            }
        }

        /// <summary>
        /// 判断报文是否符合天平全量读数响应格式。
        /// </summary>
        /// By:ChengLei
        /// <param name="response">待校验的原始响应数据。</param>
        /// <returns>返回是否为可解析的天平响应。</returns>
        /// <remarks>
        /// 由 ReceiveValidBalanceAllResponseAsync 调用，过滤非目标报文。
        /// </remarks>
        private static bool IsBalanceAllResponse(byte[] response)
        {
            return response.Length >= 13 && response[0] == 1 && response[1] == 3 && response[2] >= 8;
        }

        /// <summary>
        /// 发送温控读数命令并解析当前温度。
        /// </summary>
        /// By:ChengLei
        /// <returns>返回温度测试异步任务。</returns>
        /// <remarks>
        /// 由“测试温度”按钮调用，完成读命令发送与返回解析。
        /// </remarks>
        public async Task TestTemperature()
        {
            try
            {
                Log("开始读取温度...");

                var mapping = GetDeviceMapping("温控");

                if (mapping == null)
                {
                    Log("未找到温控设备端口配置", Brushes.Red);
                    return;
                }

                byte[] cmd = CommunicationManager.Shimaden.ReadSV();

                await CommunicationManager.TcpServer.SendToDeviceAsync(mapping.DeviceKey, cmd);

                byte[] response =
                    await CommunicationManager.TcpServer.ReceiveOnceFromDeviceAsync(mapping.DeviceKey);

                double temp =
                    CommunicationManager.Shimaden.ParseTemperature(response);

                Log($"当前温度: {temp:F1} ℃");
            }
            catch (Exception ex)
            {
                Log($"读取温度失败: {ex.Message}", Brushes.Red);
            }
        }

        /// <summary>
        /// 切换485串口连接状态。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由“连接/断开”按钮调用，执行连接状态切换并刷新页面状态灯。
        /// </remarks>
        public void ToggleRs485()
        {
            try
            {
                if (!CommunicationManager.Is485Open)
                {
                    CommunicationManager.ConnectRs485(
                        SelectedComPort,
                        SelectedBaudRate);

                    Log($"485已连接 -> {SelectedComPort}/{SelectedBaudRate}");
                }
                else
                {
                    CommunicationManager.DisconnectRs485();
                    Log("485已断开", Brushes.DarkGoldenrod);
                }
            }
            catch (Exception ex)
            {
                Log($"485连接失败: {ex.Message}", Brushes.Red);
            }

            UpdateRs485Status();
        }

        /// <summary>
        /// 切换TCP服务运行状态。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由“启动服务/停止服务”按钮调用，执行TCP服务启停并刷新页面状态灯。
        /// </remarks>
        public void ToggleTcp()
        {
            try
            {
                if (!CommunicationManager.IsTcpRunning)
                {
                    CommunicationManager.StartTcp(TcpPort);
                    Log($"TCP 已启动 -> 端口 {TcpPort}");
                }
                else
                {
                    CommunicationManager.StopTcp();
                    Log("TCP 已停止", Brushes.DarkGoldenrod);
                }
            }
            catch (Exception ex)
            {
                Log($"TCP错误: {ex.Message}", Brushes.Red);
            }

            UpdateTcpStatus();
        }

        /// <summary>
        /// 根据当前485连接状态刷新按钮文本与状态颜色。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由 ToggleRs485、构造函数与通信状态变化事件调用。
        /// </remarks>
        public void UpdateRs485Status()
        {
            Rs485ButtonText =
                CommunicationManager.Is485Open ? "断开" : "连接";

            Rs485StatusColor =
                CommunicationManager.Is485Open ? Brushes.Green : Brushes.Red;
        }

        /// <summary>
        /// 根据当前TCP运行状态刷新按钮文本与状态颜色。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由 ToggleTcp、构造函数与通信状态变化事件调用。
        /// </remarks>
        public void UpdateTcpStatus()
        {
            TcpButtonText =
                CommunicationManager.IsTcpRunning ? "停止服务" : "启动服务";

            TcpStatusColor =
                CommunicationManager.IsTcpRunning ? Brushes.Green : Brushes.Red;
        }

        /// <summary>
        /// 确保波特率下拉列表包含指定值。
        /// </summary>
        /// By:ChengLei
        /// <param name="baudRate">需要存在于列表中的波特率值。</param>
        /// <remarks>
        /// 由 SelectedBaudRate 属性设置时调用，兼容配置中非常规波特率。
        /// </remarks>
        private void EnsureBaudRateOption(int baudRate)
        {
            if (baudRate <= 0 || AvailableBaudRates.Contains(baudRate))
            {
                return;
            }

            int insertIndex = 0;
            while (insertIndex < AvailableBaudRates.Count && AvailableBaudRates[insertIndex] < baudRate)
            {
                insertIndex++;
            }

            AvailableBaudRates.Insert(insertIndex, baudRate);
        }
    }
}
