using NModbus.IO;
using System;
using System.IO.Ports;

namespace Blood_Alcohol.Communication.Serial
{
    internal sealed class SerialPortStreamResource : IStreamResource
    {
        private readonly SerialPort _port;

        public SerialPortStreamResource(SerialPort port)
        {
            _port = port ?? throw new ArgumentNullException(nameof(port));
        }

        public int InfiniteTimeout => SerialPort.InfiniteTimeout;

        public int ReadTimeout
        {
            get => _port.ReadTimeout;
            set => _port.ReadTimeout = value;
        }

        public int WriteTimeout
        {
            get => _port.WriteTimeout;
            set => _port.WriteTimeout = value;
        }

        public void DiscardInBuffer()
        {
            _port.DiscardInBuffer();
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            return _port.Read(buffer, offset, count);
        }

        public void Write(byte[] buffer, int offset, int count)
        {
            _port.Write(buffer, offset, count);
        }

        public void Dispose()
        {
            // SerialPort lifecycle is owned by Rs485Helper; no action here.
        }
    }
}
