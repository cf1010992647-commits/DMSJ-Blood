using Blood_Alcohol.Communication.Protocols;
using Blood_Alcohol.Communication.Serial;
using Blood_Alcohol.Models;
using Blood_Alcohol.Protocols;
using System;
using System.IO.Ports;
using System.Linq;
using System.Threading;

namespace Blood_Alcohol.Services
{
    public static class CommunicationManager
    {
        public const string CommunicationConfigFileName = "CommunicationConfig.json";

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
        public static int GetPort(string deviceType)
        {
            var device = Settings.TcpDevices
                .FirstOrDefault(x => x.DeviceType == deviceType);

            if (device == null)
                throw new Exception($"未找到设备类型: {deviceType}");

            return device.Port;
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

        public static void Log485Message(string message)
        {
            RaiseLog("RS485", message);
        }

        public static event Action? OnStateChanged;
        private static void RaiseStateChanged() => OnStateChanged?.Invoke();
        // ==================== 设备实例 ====================
        public static Rs485Helper Rs485 { get; } = new Rs485Helper();
        public static Lx5vPlc Plc { get; } = new Lx5vPlc(Rs485, slaveAddress: 1);
        public static SemaphoreSlim PlcAccessLock { get; } = new SemaphoreSlim(1, 1);
        public static PlcPollingService PlcPolling { get; } =
            new PlcPollingService(Plc, PlcAccessLock, () => Is485Open);
        public static TcpServer TcpServer { get; } = new TcpServer();
        public static ShimadenSrs11A Shimaden { get; } = new ShimadenSrs11A("01");
        public static BalanceProtocolService Balance { get; } = new BalanceProtocolService();

        // ==================== 配置 ====================
        public static CommunicationSettings Settings { get; set; } = new CommunicationSettings();
        private static readonly ConfigFile<CommunicationSettings> _configFile =
            new ConfigFile<CommunicationSettings>(CommunicationConfigFileName);
        private static readonly ConfigFile<CommunicationSettings> _legacyConfigFile =
            new ConfigFile<CommunicationSettings>("communication.json");

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
            Settings = _configFile.Load();

            // 兼容旧版本使用的小写文件名，避免自动连接读到默认值。
            CommunicationSettings legacy = _legacyConfigFile.Load();
            bool shouldMigrate =
                Settings.ComPort == "COM1" &&
                !string.IsNullOrWhiteSpace(legacy.ComPort) &&
                !string.Equals(legacy.ComPort, "COM1", StringComparison.OrdinalIgnoreCase);

            if (shouldMigrate)
            {
                Settings = legacy;
                SaveSettings();
            }
        }

        public static void SaveSettings()
        {
            _configFile.Save(Settings);
        }

        // ==================== 当前状态 ====================
        // 通过实例判断状态，保证与实际运行状态一致
        public static bool Is485Open => Rs485.IsOpen;
        public static bool IsTcpRunning => TcpServer.IsRunning;

        // ==================== 自动连接 ====================
        public static void AutoConnect()
        {
            // 自动连接 RS485
            try
            {
                if (!Is485Open)
                {
                    Plc.ResetConnection();
                    SerialPort port = CreateLx5vSerialPort(Settings.ComPort, Settings.BaudRate);
                    Rs485.Open(port);
                    SaveSettings();
                }
            }
            catch (Exception ex)
            {
                // DMSJ：通信服务层不直接弹窗，统一走日志通道由界面决定展示方式。
                RaiseLog("RS485", $"485启动失败: {ex.Message}", LogLevel.Error);
            }

            // 自动启动 TCP 服务
            try
            {
                if (!IsTcpRunning)
                {
                    TcpServer.Start(Settings.TcpPort);
                    SaveSettings();
                }
            }
            catch (Exception ex)
            {
                // DMSJ：通信服务层不直接弹窗，统一走日志通道由界面决定展示方式。
                RaiseLog("TCP", $"TCP启动失败: {ex.Message}", LogLevel.Error);
            }
            RaiseStateChanged(); // 通知所有订阅者
        }

        // ==================== 手动操作辅助方法 ====================
        public static void ConnectRs485(string comPort, int baudRate)
        {
            Plc.ResetConnection();
            if (Is485Open)
            {
                Rs485.Close();
            }

            SerialPort port = CreateLx5vSerialPort(comPort, baudRate);
            Rs485.Open(port);

            Settings.ComPort = comPort;
            Settings.BaudRate = baudRate;
            SaveSettings();

            RaiseStateChanged(); // 通知所有订阅者
        }

        public static void DisconnectRs485()
        {
            Plc.ResetConnection();
            if (Is485Open)
            {
                Rs485.Close();
            }

            RaiseStateChanged(); // 通知所有订阅者
        }

        private static SerialPort CreateLx5vSerialPort(string comPort, int baudRate)
        {
            return new SerialPort(comPort, baudRate, Parity.None, 8, StopBits.One)
            {
                ReadTimeout = 3000,
                WriteTimeout = 3000
            };
        }

        public static void StartTcp(int port)
        {
            if (!IsTcpRunning)
            {
                TcpServer.Start(port);
                Settings.TcpPort = port;
                SaveSettings();
            }

            RaiseStateChanged(); // 通知所有订阅者
        }

        public static void StopTcp()
        {
            if (IsTcpRunning)
            {
                TcpServer.Stop();
            }

            RaiseStateChanged(); // 通知所有订阅者
        }
    }
}
