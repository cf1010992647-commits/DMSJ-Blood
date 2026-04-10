using Blood_Alcohol.Models;
using Blood_Alcohol.Services;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO.Ports;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using static Blood_Alcohol.Services.CommunicationManager;

namespace Blood_Alcohol.ViewModels
{
    public class LogItem
    {
        public string Time { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public Brush Color { get; set; } = Brushes.DarkSlateGray;
    }

    public class CommunicationViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void Raise(string prop) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        // ================= 配置服务 =================
        private readonly ConfigService<CommunicationSettings> _configService;

        private CommunicationSettings _settings;

        // ================= TCP设备映射 =================
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

        public ObservableCollection<string> DeviceTypes { get; set; }
            = new ObservableCollection<string>
            {
                "温控",
                "扫码枪",
                "天平",
                "待定"
            };

        // ================= 串口 =================
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

        // ================= TCP =================
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

        // ================= 日志 =================
        public ObservableCollection<LogItem> Logs { get; }
            = new ObservableCollection<LogItem>();

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

        // ================= 构造 =================
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

            SyncTcpDevicesFromClients();

            TcpPort = _settings.TcpPort;
            SelectedComPort = _settings.ComPort;
            SelectedBaudRate = _settings.BaudRate;

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
                    SyncTcpDevicesFromClients();
                });
            };
        }

        // ================= 保存配置 =================
        public void SaveTcpConfig()
        {
            _settings.TcpDevices = TcpDevices.ToList();
            _settings.TcpPort = TcpPort;
            _settings.ComPort = SelectedComPort;
            _settings.BaudRate = SelectedBaudRate;

            _configService.Save(_settings);

            Log("配置已保存", Brushes.DarkGoldenrod);
        }
        private int? GetDevicePort(string deviceType)
        {
            var device = TcpDevices
                .FirstOrDefault(x => x.DeviceType == deviceType);

            return device?.Port;
        }
        public void SyncTcpDevicesFromClients()
        {
            var connectedPorts =
                CommunicationManager.TcpServer.GetConnectedPorts();

            var configDevices = _settings.TcpDevices;

            TcpDevices.Clear();

            foreach (var port in connectedPorts)
            {
                var existing = configDevices
                    .FirstOrDefault(x => x.Port == port);

                TcpDevices.Add(new TcpDeviceMapping
                {
                    Port = port,
                    DeviceType = existing?.DeviceType ?? "待定"
                });
            }

            Raise(nameof(TcpDevices));
        }
        // ================= 功能 =================
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
        public async Task TestWeight()
        {
            try
            {
                Log("开始读取重量...");

                var port = GetDevicePort("天平");

                if (port == null)
                {
                    Log("未找到天平设备端口配置", Brushes.Red);
                    return;
                }

                await CommunicationManager.TcpServer.SendToPort(
                    port.Value,
                    CommunicationManager.Balance.GetDotCommand());

                byte[] dotResponse =
                    await CommunicationManager.TcpServer.ReceiveOnceFromPortAsync(port.Value);

                await CommunicationManager.TcpServer.SendToPort(
                    port.Value,
                    CommunicationManager.Balance.GetWeightCommand());

                byte[] weightResponse =
                    await CommunicationManager.TcpServer.ReceiveOnceFromPortAsync(port.Value);

                double weight =
                    CommunicationManager.Balance.ReadWeight(
                        dotResponse,
                        weightResponse);

                Log($"实际重量: {weight:F2}");
            }
            catch (Exception ex)
            {
                Log($"读取重量失败: {ex.Message}", Brushes.Red);
            }
        }
        public async Task TestTemperature()
        {
            try
            {
                Log("开始读取温度...");

                var port = GetDevicePort("温控");

                if (port == null)
                {
                    Log("未找到温控设备端口配置", Brushes.Red);
                    return;
                }

                byte[] cmd = CommunicationManager.Shimaden.ReadSV();

                await CommunicationManager.TcpServer.SendToPort(port.Value, cmd);

                byte[] response =
                    await CommunicationManager.TcpServer.ReceiveOnceFromPortAsync(port.Value);

                double temp =
                    CommunicationManager.Shimaden.ParseTemperature(response);

                Log($"当前温度: {temp:F1} ℃");
            }
            catch (Exception ex)
            {
                Log($"读取温度失败: {ex.Message}", Brushes.Red);
            }
        }
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

        public void UpdateRs485Status()
        {
            Rs485ButtonText =
                CommunicationManager.Is485Open ? "断开" : "连接";

            Rs485StatusColor =
                CommunicationManager.Is485Open ? Brushes.Green : Brushes.Red;
        }

        public void UpdateTcpStatus()
        {
            TcpButtonText =
                CommunicationManager.IsTcpRunning ? "停止服务" : "启动服务";

            TcpStatusColor =
                CommunicationManager.IsTcpRunning ? Brushes.Green : Brushes.Red;
        }

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
