using Blood_Alcohol.Communication.Protocols;
using Blood_Alcohol.Communication.Serial;
using Blood_Alcohol.Models;
using Blood_Alcohol.Protocols;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Threading;

namespace Blood_Alcohol.Services
{
    public static class CommunicationManager
    {
        public const string CommunicationConfigFileName = "CommunicationConfig.json";
        private static readonly DeviceRegistry _devices = new DeviceRegistry();
        private static readonly CommunicationSettingsStore _settingsStore =
            new CommunicationSettingsStore(CommunicationConfigFileName, "communication.json");
        private static readonly CommunicationConnectionCoordinator _connections =
            new CommunicationConnectionCoordinator(
                _devices,
                () => Settings,
                SaveSettings,
                ConfigureTcpDeviceMappings,
                RaiseLog,
                RaiseStateChanged);
        private static IReadOnlyList<string> _configurationErrors = Array.Empty<string>();

        public enum LogLevel
        {
            Info,
            Warning,
            Error
        }

        public class LogMessage
        {
            public DateTime Time { get; set; } = DateTime.Now;
            public string Source { get; set; } = string.Empty;
            public string Message { get; set; } = string.Empty;
            public LogLevel Level { get; set; }
        }

        public static event Action<LogMessage>? OnLogReceived;

        /// <summary>
        /// 按设备类型获取 TCP 设备映射。
        /// </summary>
        /// By:ChengLei
        /// <param name="deviceType">设备类型名称。</param>
        /// <returns>返回匹配的 TCP 设备映射。</returns>
        /// <remarks>
        /// 由业务流程按逻辑设备身份定位 TCP 客户端时调用。
        /// </remarks>
        public static TcpDeviceMapping GetDeviceMapping(string deviceType)
        {
            TcpDeviceMapping? device = Settings.TcpDevices
                .FirstOrDefault(x => string.Equals(x.DeviceType, deviceType, StringComparison.OrdinalIgnoreCase));

            if (device == null)
            {
                string message = $"未找到设备类型映射: {deviceType}";
                LogTcpMessage(message, LogLevel.Error);
                throw new InvalidOperationException(message);
            }

            return device;
        }

        /// <summary>
        /// 按设备类型获取逻辑设备键。
        /// </summary>
        /// By:ChengLei
        /// <param name="deviceType">设备类型名称。</param>
        /// <returns>返回逻辑设备键。</returns>
        /// <remarks>
        /// 由 WorkflowEngine 等调用方替代旧端口定位方式。
        /// </remarks>
        public static string GetDeviceKey(string deviceType)
        {
            string deviceKey = GetDeviceMapping(deviceType).DeviceKey;
            if (string.IsNullOrWhiteSpace(deviceKey))
            {
                string message = $"设备 {deviceType} 未配置 DeviceKey";
                LogTcpMessage(message, LogLevel.Error);
                throw new InvalidOperationException(message);
            }

            return deviceKey;
        }
        private static void RaiseLog(string source, string message, LogLevel level = LogLevel.Info)
        {
            OnLogReceived?.Invoke(new LogMessage
            {
                Time = DateTime.Now,
                Source = source,
                Message = message,
                Level = level
            });
        }

        public static void LogTcpMessage(string message)
        {
            RaiseLog("TCP", message);
        }

        /// <summary>
        /// 写入 TCP 通信日志。
        /// </summary>
        /// By:ChengLei
        /// <param name="message">日志文本。</param>
        /// <param name="level">日志级别。</param>
        /// <remarks>
        /// 由设备身份解析失败等异常路径调用，确保错误不会被静默吞掉。
        /// </remarks>
        public static void LogTcpMessage(string message, LogLevel level)
        {
            RaiseLog("TCP", message, level);
        }

        public static void Log485Message(string message)
        {
            RaiseLog("RS485", message);
        }

        /// <summary>
        /// 写入配置治理日志。
        /// </summary>
        /// <param name="message">日志文本。</param>
        /// <param name="level">日志级别。</param>
        public static void LogConfigurationMessage(string message, LogLevel level = LogLevel.Info)
        {
            RaiseLog("配置", message, level);
        }

        public static event Action? OnStateChanged;
        private static void RaiseStateChanged() => OnStateChanged?.Invoke();
        // ==================== 设备实例 ====================
        public static Rs485Helper Rs485 => _devices.Rs485;
        public static Lx5vPlc Plc => _devices.Plc;
        public static SemaphoreSlim PlcAccessLock => _devices.PlcAccessLock;
        public static SemaphoreSlim TcpReceiveLock => _devices.TcpReceiveLock;
        public static PlcPollingService PlcPolling => _devices.PlcPolling;
        public static TcpServer TcpServer => _devices.TcpServer;
        public static ShimadenSrs11A Shimaden => _devices.Shimaden;
        public static BalanceProtocolService Balance => _devices.Balance;

        // ==================== 配置 ====================
        public static CommunicationSettings Settings
        {
            get => _settingsStore.Settings;
            set => _settingsStore.Settings = value ?? new CommunicationSettings();
        }

        /// <summary>
        /// 当前通信配置是否存在校验错误。
        /// </summary>
        public static bool HasConfigurationErrors => _configurationErrors.Count > 0;

        /// <summary>
        /// 当前通信配置校验错误列表。
        /// </summary>
        public static IReadOnlyList<string> ConfigurationErrors => _configurationErrors;

        static CommunicationManager()
        {
            Rs485.OnLog += HandleRs485Log;
            PlcPolling.Start();
        }

        private static void HandleRs485Log(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            if (message.StartsWith("发送HEX") || message.StartsWith("接收HEX"))
            {
                return;
            }

            LogLevel level = LogLevel.Info;
            if (message.Contains("异常") || message.Contains("失败") || message.Contains("错误"))
            {
                level = LogLevel.Error;
            }
            else if (message.Contains("断开"))
            {
                level = LogLevel.Warning;
            }

            RaiseLog("RS485", message, level);

            if (message.StartsWith("485已连接") || message.StartsWith("485已断开"))
            {
                RaiseStateChanged();
            }
        }

        public static void LoadSettings()
        {
            _settingsStore.Load();
            if (ValidateCurrentSettingsAndLog())
            {
                ConfigureTcpDeviceMappings();
            }
        }

        public static void SaveSettings()
        {
            ConfigureTcpDeviceMappings();
            _settingsStore.Save();
            ValidateCurrentSettingsAndLog();
        }

        /// <summary>
        /// 同步 TCP 服务端的设备身份映射。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由加载、保存和启动 TCP 服务前调用，保证服务端能按 DeviceKey 绑定会话。
        /// </remarks>
        public static void ConfigureTcpDeviceMappings()
        {
            TcpServer.ConfigureDeviceMappings(Settings.TcpDevices);
        }

        // ==================== 当前状态 ====================
        // 通过实例判断状态，保证与实际运行状态一致
        public static bool Is485Open => Rs485.IsOpen;
        public static bool IsTcpRunning => TcpServer.IsRunning;

        // ==================== 自动连接 ====================
        public static void AutoConnect()
        {
            if (!ValidateCurrentSettingsAndLog())
            {
                RaiseStateChanged();
                return;
            }

            _connections.AutoConnect();
        }

        // ==================== 手动操作辅助方法 ====================
        public static void ConnectRs485(string comPort, int baudRate)
        {
            _connections.ConnectRs485(comPort, baudRate);
        }

        public static void DisconnectRs485()
        {
            _connections.DisconnectRs485();
        }

        public static void StartTcp(int port)
        {
            int previousPort = Settings.TcpPort;
            Settings.TcpPort = port;
            if (!ValidateCurrentSettingsAndLog())
            {
                Settings.TcpPort = previousPort;
                RaiseStateChanged();
                return;
            }

            _connections.StartTcp(port);
        }

        public static void StopTcp()
        {
            _connections.StopTcp();
        }

        /// <summary>
        /// 校验当前通信配置并写入错误日志。
        /// </summary>
        /// <returns>返回 true 表示配置有效。</returns>
        public static bool ValidateCurrentSettingsAndLog()
        {
            List<string> errors = Settings.Validate();
            _configurationErrors = errors;
            if (errors.Count == 0)
            {
                return true;
            }

            foreach (string error in errors)
            {
                RaiseLog("配置", $"通信配置非法：{error}", LogLevel.Error);
            }

            return false;
        }

        /// <summary>
        /// 通信设备实例注册表。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 作为 CommunicationManager 的最小过渡组件，集中持有串口、PLC、TCP 和协议服务实例。
        /// </remarks>
        private sealed class DeviceRegistry
        {
            /// <summary>
            /// 初始化通信设备实例注册表。
            /// </summary>
            /// By:ChengLei
            /// <remarks>
            /// 由 CommunicationManager 静态初始化时调用，保持原有单例实例语义。
            /// </remarks>
            public DeviceRegistry()
            {
                Rs485 = new Rs485Helper();
                Plc = new Lx5vPlc(Rs485, slaveAddress: 1);
                PlcAccessLock = new SemaphoreSlim(1, 1);
                TcpReceiveLock = new SemaphoreSlim(1, 1);
                PlcPolling = new PlcPollingService(Plc, PlcAccessLock, () => Rs485.IsOpen);
                TcpServer = new TcpServer();
                Shimaden = new ShimadenSrs11A("01");
                Balance = new BalanceProtocolService();
            }

            /// <summary>
            /// RS485 通信助手。
            /// </summary>
            /// By:ChengLei
            public Rs485Helper Rs485 { get; }

            /// <summary>
            /// PLC 协议服务。
            /// </summary>
            /// By:ChengLei
            public Lx5vPlc Plc { get; }

            /// <summary>
            /// PLC 访问互斥锁。
            /// </summary>
            /// By:ChengLei
            public SemaphoreSlim PlcAccessLock { get; }

            /// <summary>
            /// TCP 接收互斥锁。
            /// </summary>
            /// By:ChengLei
            public SemaphoreSlim TcpReceiveLock { get; }

            /// <summary>
            /// PLC 后台轮询服务。
            /// </summary>
            /// By:ChengLei
            public PlcPollingService PlcPolling { get; }

            /// <summary>
            /// TCP 服务端。
            /// </summary>
            /// By:ChengLei
            public TcpServer TcpServer { get; }

            /// <summary>
            /// 温控协议服务。
            /// </summary>
            /// By:ChengLei
            public ShimadenSrs11A Shimaden { get; }

            /// <summary>
            /// 天平协议服务。
            /// </summary>
            /// By:ChengLei
            public BalanceProtocolService Balance { get; }
        }

        /// <summary>
        /// 通信配置读写组件。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 作为 CommunicationManager 的最小过渡组件，集中处理配置加载、旧配置迁移和保存。
        /// </remarks>
        private sealed class CommunicationSettingsStore
        {
            private readonly ConfigFile<CommunicationSettings> _configFile;
            private readonly ConfigFile<CommunicationSettings> _legacyConfigFile;

            /// <summary>
            /// 初始化通信配置读写组件。
            /// </summary>
            /// By:ChengLei
            /// <param name="configFileName">当前配置文件名。</param>
            /// <param name="legacyConfigFileName">旧版配置文件名。</param>
            /// <remarks>
            /// 由 CommunicationManager 静态初始化时调用。
            /// </remarks>
            public CommunicationSettingsStore(string configFileName, string legacyConfigFileName)
            {
                _configFile = new ConfigFile<CommunicationSettings>(configFileName);
                _legacyConfigFile = new ConfigFile<CommunicationSettings>(legacyConfigFileName);
            }

            /// <summary>
            /// 当前通信配置。
            /// </summary>
            /// By:ChengLei
            public CommunicationSettings Settings { get; set; } = new CommunicationSettings();

            /// <summary>
            /// 加载通信配置并兼容旧配置文件。
            /// </summary>
            /// By:ChengLei
            /// <remarks>
            /// 由 CommunicationManager.LoadSettings 调用，迁移旧配置后会立即保存到新文件。
            /// </remarks>
            public void Load()
            {
                Settings = _configFile.Load();

                CommunicationSettings legacy = _legacyConfigFile.Load();
                bool shouldMigrate =
                    Settings.ComPort == "COM1" &&
                    !string.IsNullOrWhiteSpace(legacy.ComPort) &&
                    !string.Equals(legacy.ComPort, "COM1", StringComparison.OrdinalIgnoreCase);

                if (shouldMigrate)
                {
                    Settings = legacy;
                    Save();
                }
            }

            /// <summary>
            /// 保存当前通信配置。
            /// </summary>
            /// By:ChengLei
            /// <remarks>
            /// 由 CommunicationManager.SaveSettings 调用。
            /// </remarks>
            public void Save()
            {
                _configFile.Save(Settings);
            }
        }

        /// <summary>
        /// 通信连接编排组件。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 作为 CommunicationManager 的最小过渡组件，集中处理自动连接、TCP 启停和 RS485 连接。
        /// </remarks>
        private sealed class CommunicationConnectionCoordinator
        {
            private readonly DeviceRegistry _devices;
            private readonly Func<CommunicationSettings> _settingsProvider;
            private readonly Action _saveSettings;
            private readonly Action _configureTcpMappings;
            private readonly Action<string, string, LogLevel> _raiseLog;
            private readonly Action _raiseStateChanged;

            /// <summary>
            /// 初始化通信连接编排组件。
            /// </summary>
            /// By:ChengLei
            /// <param name="devices">通信设备实例注册表。</param>
            /// <param name="settingsProvider">通信配置提供委托。</param>
            /// <param name="saveSettings">通信配置保存委托。</param>
            /// <param name="configureTcpMappings">TCP 设备映射同步委托。</param>
            /// <param name="raiseLog">日志发送委托。</param>
            /// <param name="raiseStateChanged">状态变化通知委托。</param>
            /// <remarks>
            /// 由 CommunicationManager 静态初始化时调用。
            /// </remarks>
            public CommunicationConnectionCoordinator(
                DeviceRegistry devices,
                Func<CommunicationSettings> settingsProvider,
                Action saveSettings,
                Action configureTcpMappings,
                Action<string, string, LogLevel> raiseLog,
                Action raiseStateChanged)
            {
                _devices = devices;
                _settingsProvider = settingsProvider;
                _saveSettings = saveSettings;
                _configureTcpMappings = configureTcpMappings;
                _raiseLog = raiseLog;
                _raiseStateChanged = raiseStateChanged;
            }

            /// <summary>
            /// 自动连接 RS485 并启动 TCP 服务。
            /// </summary>
            /// By:ChengLei
            /// <remarks>
            /// 由 CommunicationManager.AutoConnect 调用，保持原有启动行为。
            /// </remarks>
            public void AutoConnect()
            {
                TryAutoConnectRs485();
                TryAutoStartTcp();
                _raiseStateChanged();
            }

            /// <summary>
            /// 手动连接 RS485。
            /// </summary>
            /// By:ChengLei
            /// <param name="comPort">串口号。</param>
            /// <param name="baudRate">波特率。</param>
            /// <remarks>
            /// 由 CommunicationManager.ConnectRs485 调用。
            /// </remarks>
            public void ConnectRs485(string comPort, int baudRate)
            {
                _devices.Plc.ResetConnection();
                if (_devices.Rs485.IsOpen)
                {
                    _devices.Rs485.Close();
                }

                SerialPort port = CreateLx5vSerialPort(comPort, baudRate);
                _devices.Rs485.Open(port);

                CommunicationSettings settings = _settingsProvider();
                settings.ComPort = comPort;
                settings.BaudRate = baudRate;
                _saveSettings();

                _raiseStateChanged();
            }

            /// <summary>
            /// 断开 RS485。
            /// </summary>
            /// By:ChengLei
            /// <remarks>
            /// 由 CommunicationManager.DisconnectRs485 调用。
            /// </remarks>
            public void DisconnectRs485()
            {
                _devices.Plc.ResetConnection();
                if (_devices.Rs485.IsOpen)
                {
                    _devices.Rs485.Close();
                }

                _raiseStateChanged();
            }

            /// <summary>
            /// 启动 TCP 服务。
            /// </summary>
            /// By:ChengLei
            /// <param name="port">TCP 监听端口。</param>
            /// <remarks>
            /// 由 CommunicationManager.StartTcp 调用。
            /// </remarks>
            public void StartTcp(int port)
            {
                if (!_devices.TcpServer.IsRunning)
                {
                    _configureTcpMappings();
                    _devices.TcpServer.Start(port);
                    _settingsProvider().TcpPort = port;
                    _saveSettings();
                }

                _raiseStateChanged();
            }

            /// <summary>
            /// 停止 TCP 服务。
            /// </summary>
            /// By:ChengLei
            /// <remarks>
            /// 由 CommunicationManager.StopTcp 调用。
            /// </remarks>
            public void StopTcp()
            {
                if (_devices.TcpServer.IsRunning)
                {
                    _devices.TcpServer.Stop();
                }

                _raiseStateChanged();
            }

            /// <summary>
            /// 尝试自动连接 RS485。
            /// </summary>
            /// By:ChengLei
            /// <remarks>
            /// 由 AutoConnect 调用，异常会写入 RS485 日志。
            /// </remarks>
            private void TryAutoConnectRs485()
            {
                try
                {
                    if (!_devices.Rs485.IsOpen)
                    {
                        CommunicationSettings settings = _settingsProvider();
                        _devices.Plc.ResetConnection();
                        SerialPort port = CreateLx5vSerialPort(settings.ComPort, settings.BaudRate);
                        _devices.Rs485.Open(port);
                        _saveSettings();
                    }
                }
                catch (Exception ex)
                {
                    _raiseLog("RS485", $"485启动失败: {ex.Message}", LogLevel.Error);
                }
            }

            /// <summary>
            /// 尝试自动启动 TCP 服务。
            /// </summary>
            /// By:ChengLei
            /// <remarks>
            /// 由 AutoConnect 调用，异常会写入 TCP 日志。
            /// </remarks>
            private void TryAutoStartTcp()
            {
                try
                {
                    if (!_devices.TcpServer.IsRunning)
                    {
                        _configureTcpMappings();
                        _devices.TcpServer.Start(_settingsProvider().TcpPort);
                        _saveSettings();
                    }
                }
                catch (Exception ex)
                {
                    _raiseLog("TCP", $"TCP启动失败: {ex.Message}", LogLevel.Error);
                }
            }

            /// <summary>
            /// 创建 PLC 串口对象。
            /// </summary>
            /// By:ChengLei
            /// <param name="comPort">串口号。</param>
            /// <param name="baudRate">波特率。</param>
            /// <returns>返回已配置读写超时的串口对象。</returns>
            /// <remarks>
            /// 由 RS485 自动连接和手动连接路径复用。
            /// </remarks>
            private static SerialPort CreateLx5vSerialPort(string comPort, int baudRate)
            {
                return new SerialPort(comPort, baudRate, Parity.None, 8, StopBits.One)
                {
                    ReadTimeout = 3000,
                    WriteTimeout = 3000
                };
            }
        }
    }
}
