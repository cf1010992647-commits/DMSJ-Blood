using NModbus.IO;
using System;
using System.IO;
using System.IO.Ports;
using System.Threading;
using Blood_Alcohol.Services;

namespace Blood_Alcohol.Communication.Serial
{
    /// <summary>
    /// 串口流资源适配器，将 SerialPort 封装为 NModbus 所需流接口。
    /// </summary>
    /// By:ChengLei
    /// <remarks>
    /// 由 Lx5vPlc 在创建 Modbus 主站时使用，统一处理串口读写异常与节流日志。
    /// </remarks>
    internal sealed class SerialPortStreamResource : IStreamResource
    {
        // 底层串口对象，由 Rs485Helper 管理生命周期
        private readonly SerialPort _port;
        // 最近一次读取错误日志时间戳
        private static long _lastReadErrorTick;
        // 最近一次写入错误日志时间戳
        private static long _lastWriteErrorTick;

        /// <summary>
        /// 初始化串口流资源包装器。
        /// </summary>
        /// By:ChengLei
        /// <param name="port">已创建的串口对象。</param>
        /// <remarks>
        /// 由 Lx5vPlc.GetOrCreateMaster 调用。
        /// </remarks>
        public SerialPortStreamResource(SerialPort port)
        {
            _port = port ?? throw new ArgumentNullException(nameof(port));
        }

        /// <summary>
        /// 获取串口无限超时常量。
        /// </summary>
        /// By:ChengLei
        /// <returns>返回 SerialPort 的无限超时值。</returns>
        /// <remarks>
        /// 由 NModbus 流资源接口读取。
        /// </remarks>
        public int InfiniteTimeout => SerialPort.InfiniteTimeout;

        /// <summary>
        /// 获取或设置串口读取超时。
        /// </summary>
        /// By:ChengLei
        /// <returns>返回当前串口读取超时毫秒值。</returns>
        /// <remarks>
        /// 由 NModbus 传输层配置读超时时使用。
        /// </remarks>
        public int ReadTimeout
        {
            get => _port.ReadTimeout;
            set => _port.ReadTimeout = value;
        }

        /// <summary>
        /// 获取或设置串口写入超时。
        /// </summary>
        /// By:ChengLei
        /// <returns>返回当前串口写入超时毫秒值。</returns>
        /// <remarks>
        /// 由 NModbus 传输层配置写超时时使用。
        /// </remarks>
        public int WriteTimeout
        {
            get => _port.WriteTimeout;
            set => _port.WriteTimeout = value;
        }

        /// <summary>
        /// 清空串口输入缓冲区。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由上层通信流程在重置收包状态时调用。
        /// </remarks>
        public void DiscardInBuffer()
        {
            _port.DiscardInBuffer();
        }

        /// <summary>
        /// 从串口读取指定长度数据。
        /// </summary>
        /// By:ChengLei
        /// <param name="buffer">读取数据的目标缓冲区。</param>
        /// <param name="offset">写入缓冲区起始偏移。</param>
        /// <param name="count">期望读取字节数。</param>
        /// <returns>返回实际读取字节数，异常时返回0。</returns>
        /// <remarks>
        /// 由 NModbus 传输层调用；读取失败时不会抛出到上层，而是记录节流日志。
        /// </remarks>
        public int Read(byte[] buffer, int offset, int count)
        {
            try
            {
                return _port.Read(buffer, offset, count);
            }
            catch (TimeoutException ex)
            {
                ReportReadError($"PLC串口读取超时: {ex.Message}");
                return 0;
            }
            catch (IOException ex)
            {
                ReportReadError($"PLC串口读取I/O异常: {ex.Message}");
                return 0;
            }
            catch (InvalidOperationException ex)
            {
                ReportReadError($"PLC串口未就绪: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// 向串口写入指定长度数据。
        /// </summary>
        /// By:ChengLei
        /// <param name="buffer">待发送数据缓冲区。</param>
        /// <param name="offset">缓冲区读取起始偏移。</param>
        /// <param name="count">发送字节数。</param>
        /// <remarks>
        /// 由 NModbus 传输层调用；写入失败仅记录节流日志，避免上层频繁崩溃。
        /// </remarks>
        public void Write(byte[] buffer, int offset, int count)
        {
            try
            {
                _port.Write(buffer, offset, count);
            }
            catch (TimeoutException ex)
            {
                ReportWriteError($"PLC串口写入超时: {ex.Message}");
            }
            catch (IOException ex)
            {
                ReportWriteError($"PLC串口写入I/O异常: {ex.Message}");
            }
            catch (InvalidOperationException ex)
            {
                ReportWriteError($"PLC串口未就绪: {ex.Message}");
            }
        }

        /// <summary>
        /// 释放流资源对象。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 串口生命周期由 Rs485Helper 持有，此处不主动释放串口。
        /// </remarks>
        public void Dispose()
        {
            // 串口生命周期由 Rs485Helper 管理，此处不重复释放。
        }

        /// <summary>
        /// 上报串口读取异常日志（带节流）。
        /// </summary>
        /// By:ChengLei
        /// <param name="message">待上报日志文本。</param>
        /// <remarks>
        /// 由 Read 异常分支调用，避免短时间内重复刷屏。
        /// </remarks>
        private static void ReportReadError(string message)
        {
            long now = Environment.TickCount64;
            long last = Interlocked.Read(ref _lastReadErrorTick);
            if (now - last < 1200)
            {
                return;
            }

            Interlocked.Exchange(ref _lastReadErrorTick, now);
            CommunicationManager.Log485Message(message);
        }

        /// <summary>
        /// 上报串口写入异常日志（带节流）。
        /// </summary>
        /// By:ChengLei
        /// <param name="message">待上报日志文本。</param>
        /// <remarks>
        /// 由 Write 异常分支调用，避免短时间内重复刷屏。
        /// </remarks>
        private static void ReportWriteError(string message)
        {
            long now = Environment.TickCount64;
            long last = Interlocked.Read(ref _lastWriteErrorTick);
            if (now - last < 1200)
            {
                return;
            }

            Interlocked.Exchange(ref _lastWriteErrorTick, now);
            CommunicationManager.Log485Message(message);
        }
    }
}
