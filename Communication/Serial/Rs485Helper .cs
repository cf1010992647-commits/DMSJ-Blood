using System;
using System.IO.Ports;

namespace Blood_Alcohol.Communication.Serial
{
    #region RS-485 Helper

    /// <summary>
    /// RS485 串口管理器，负责串口打开、关闭和状态通知。
    /// </summary>
    /// By:ChengLei
    /// <remarks>
    /// 由 CommunicationManager 持有单例实例，供 PLC 通信模块复用同一串口对象。
    /// </remarks>
    public class Rs485Helper : IDisposable
    {
        // 串口操作互斥锁，防止并发开关串口
        private readonly object _syncRoot = new();
        // 当前持有的串口对象实例
        private SerialPort? _serialPort;

        /// <summary>
        /// 获取串口是否处于打开状态。
        /// </summary>
        /// By:ChengLei
        /// <returns>返回串口是否已打开。</returns>
        /// <remarks>
        /// 由上层通信状态判断逻辑调用。
        /// </remarks>
        public bool IsOpen => _serialPort?.IsOpen == true;

        /// <summary>
        /// 获取当前串口对象引用。
        /// </summary>
        /// By:ChengLei
        /// <returns>返回当前串口对象，未连接时为空。</returns>
        /// <remarks>
        /// 由 Lx5vPlc 获取串口实例并创建 Modbus 主站。
        /// </remarks>
        public SerialPort? Port => _serialPort;

        /// <summary>
        /// 串口日志事件。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 在串口连接、断开或异常时触发。
        /// </remarks>
        public event Action<string>? OnLog;

        #region Open / Close

        /// <summary>
        /// 打开并接管指定串口对象。
        /// </summary>
        /// By:ChengLei
        /// <param name="serialPort">待接管的串口对象。</param>
        /// <remarks>
        /// 由 CommunicationManager.ConnectRs485 调用；若已有旧连接会先关闭再替换。
        /// </remarks>
        public void Open(SerialPort serialPort)
        {
            if (serialPort == null)
                throw new ArgumentNullException(nameof(serialPort));

            lock (_syncRoot)
            {
                if (_serialPort?.IsOpen == true)
                {
                    _serialPort.Close();
                    _serialPort.Dispose();
                }

                _serialPort = serialPort;

                if (!_serialPort.IsOpen)
                    _serialPort.Open();

                OnLog?.Invoke($"485已连接: {_serialPort.PortName}");
            }
        }

        /// <summary>
        /// 关闭并释放当前串口连接。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由 CommunicationManager.DisconnectRs485 和 Dispose 调用。
        /// </remarks>
        public void Close()
        {
            lock (_syncRoot)
            {
                try
                {
                    if (_serialPort?.IsOpen == true)
                    {
                        _serialPort.Close();
                    }

                    _serialPort?.Dispose();
                    _serialPort = null;
                    OnLog?.Invoke("485已断开");
                }
                catch (Exception ex)
                {
                    OnLog?.Invoke($"关闭异常: {ex.Message}");
                }
            }
        }

        #endregion

        /// <summary>
        /// 释放 RS485 资源。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 调用 Close 统一执行串口释放与日志通知。
        /// </remarks>
        public void Dispose()
        {
            Close();
        }
    }

    #endregion
}
