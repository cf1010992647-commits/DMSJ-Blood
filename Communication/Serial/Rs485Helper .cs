using System;
using System.IO.Ports;

namespace Blood_Alcohol.Communication.Serial
{
    #region RS-485 Helper

    public class Rs485Helper : IDisposable
    {
        private readonly object _syncRoot = new();
        private SerialPort? _serialPort;

        public bool IsOpen => _serialPort?.IsOpen == true;
        public SerialPort? Port => _serialPort;

        public event Action<string>? OnLog;

        #region Open / Close

        public void Open(SerialPort serialPort)
        {
            if (serialPort == null)
                throw new ArgumentNullException(nameof(serialPort));

            lock (_syncRoot)
            {
                // Keep a single owner for the serial handle.
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

        public void Dispose()
        {
            Close();
        }
    }

    #endregion
}
