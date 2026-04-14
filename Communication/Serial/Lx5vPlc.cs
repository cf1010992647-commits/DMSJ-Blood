using NModbus;
using System;
using System.IO;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;

namespace Blood_Alcohol.Communication.Serial
{
    /// <summary>
    /// LX5V PLC 通信封装，提供 Modbus RTU 的读写接口与异常统一处理。
    /// </summary>
    /// By:ChengLei
    /// <remarks>
    /// 由 CommunicationManager 创建并注入到各业务模块，内部复用 Rs485Helper 的串口连接。
    /// </remarks>
    public class Lx5vPlc
    {
        // RS485 串口管理器
        private readonly Rs485Helper _rs485;
        // PLC 响应超时时间
        private readonly int _responseTimeoutMs;
        // Modbus 主站互斥锁
        private readonly SemaphoreSlim _masterGate = new(1, 1);

        // 当前缓存的 Modbus 主站实例
        private IModbusSerialMaster? _master;
        // 当前主站绑定的串口实例
        private SerialPort? _masterPort;

        /// <summary>
        /// 获取当前 PLC 从站地址。
        /// </summary>
        /// By:ChengLei
        /// <returns>返回 Modbus 从站地址。</returns>
        /// <remarks>
        /// 由上层通信流程在日志或调试中读取。
        /// </remarks>
        public byte SlaveAddress { get; }

        /// <summary>
        /// 初始化 PLC 通信对象。
        /// </summary>
        /// By:ChengLei
        /// <param name="rs485">RS485 管理器实例。</param>
        /// <param name="slaveAddress">PLC Modbus 从站地址。</param>
        /// <param name="responseTimeoutMs">通信超时时间（毫秒）。</param>
        /// <remarks>
        /// 由 CommunicationManager 构造阶段调用。
        /// </remarks>
        public Lx5vPlc(Rs485Helper rs485, byte slaveAddress = 1, int responseTimeoutMs = 3000)
        {
            _rs485 = rs485 ?? throw new ArgumentNullException(nameof(rs485));
            SlaveAddress = slaveAddress;
            _responseTimeoutMs = responseTimeoutMs > 0 ? responseTimeoutMs : 3000;
        }

        /// <summary>
        /// 读取保持寄存器，失败时抛出异常。
        /// </summary>
        /// By:ChengLei
        /// <param name="startAddress">起始寄存器地址。</param>
        /// <param name="length">读取寄存器数量。</param>
        /// <returns>返回读取到的寄存器值数组。</returns>
        /// <remarks>
        /// 由需要强异常语义的业务流程调用。
        /// </remarks>
        public async Task<ushort[]> ReadHoldingRegistersAsync(ushort startAddress, ushort length)
        {
            PlcCallResult<ushort[]> result = await ExecuteAsync(
                operation: "ReadHoldingRegisters",
                action: master => master.ReadHoldingRegistersAsync(SlaveAddress, startAddress, length),
                fallback: Array.Empty<ushort>()).ConfigureAwait(false);

            if (!result.Success)
            {
                throw result.Error!;
            }

            return result.Value;
        }

        /// <summary>
        /// 尝试读取保持寄存器，失败时返回错误文本。
        /// </summary>
        /// By:ChengLei
        /// <param name="startAddress">起始寄存器地址。</param>
        /// <param name="length">读取寄存器数量。</param>
        /// <returns>返回成功标记、寄存器数组和错误信息。</returns>
        /// <remarks>
        /// 由业务层轮询流程调用，避免异常频繁中断流程。
        /// </remarks>
        public async Task<(bool Success, ushort[] Values, string Error)> TryReadHoldingRegistersAsync(ushort startAddress, ushort length)
        {
            PlcCallResult<ushort[]> result = await ExecuteAsync(
                operation: "ReadHoldingRegisters",
                action: master => master.ReadHoldingRegistersAsync(SlaveAddress, startAddress, length),
                fallback: Array.Empty<ushort>()).ConfigureAwait(false);

            if (result.Success)
            {
                return (true, result.Value, string.Empty);
            }

            return (false, Array.Empty<ushort>(), result.Error?.Message ?? "Unknown PLC error.");
        }

        /// <summary>
        /// 读取线圈状态，失败时抛出异常。
        /// </summary>
        /// By:ChengLei
        /// <param name="startAddress">起始线圈地址。</param>
        /// <param name="length">读取线圈数量。</param>
        /// <returns>返回读取到的线圈状态数组。</returns>
        /// <remarks>
        /// 由需要强异常语义的业务流程调用。
        /// </remarks>
        public async Task<bool[]> ReadCoilsAsync(ushort startAddress, ushort length)
        {
            PlcCallResult<bool[]> result = await ExecuteAsync(
                operation: "ReadCoils",
                action: master => master.ReadCoilsAsync(SlaveAddress, startAddress, length),
                fallback: Array.Empty<bool>()).ConfigureAwait(false);

            if (!result.Success)
            {
                throw result.Error!;
            }

            return result.Value;
        }

        /// <summary>
        /// 尝试读取线圈状态，失败时返回错误文本。
        /// </summary>
        /// By:ChengLei
        /// <param name="startAddress">起始线圈地址。</param>
        /// <param name="length">读取线圈数量。</param>
        /// <returns>返回成功标记、线圈数组和错误信息。</returns>
        /// <remarks>
        /// 由业务层轮询流程调用，避免异常频繁中断流程。
        /// </remarks>
        public async Task<(bool Success, bool[] Values, string Error)> TryReadCoilsAsync(ushort startAddress, ushort length)
        {
            PlcCallResult<bool[]> result = await ExecuteAsync(
                operation: "ReadCoils",
                action: master => master.ReadCoilsAsync(SlaveAddress, startAddress, length),
                fallback: Array.Empty<bool>()).ConfigureAwait(false);

            if (result.Success)
            {
                return (true, result.Value, string.Empty);
            }

            return (false, Array.Empty<bool>(), result.Error?.Message ?? "Unknown PLC error.");
        }

        /// <summary>
        /// 写入单个线圈，失败时抛出异常。
        /// </summary>
        /// By:ChengLei
        /// <param name="address">线圈地址。</param>
        /// <param name="value">线圈目标值。</param>
        /// <returns>返回写入异步任务。</returns>
        /// <remarks>
        /// 由流程控制信号下发场景调用。
        /// </remarks>
        public async Task WriteSingleCoilAsync(ushort address, bool value)
        {
            PlcCallResult<bool> result = await ExecuteAsync(
                operation: "WriteSingleCoil",
                action: async master =>
                {
                    await master.WriteSingleCoilAsync(SlaveAddress, address, value).ConfigureAwait(false);
                    return true;
                },
                fallback: false).ConfigureAwait(false);

            if (!result.Success)
            {
                throw result.Error!;
            }
        }

        /// <summary>
        /// 尝试写入单个线圈，失败时返回错误文本。
        /// </summary>
        /// By:ChengLei
        /// <param name="address">线圈地址。</param>
        /// <param name="value">线圈目标值。</param>
        /// <returns>返回成功标记和错误信息。</returns>
        /// <remarks>
        /// 由不希望抛异常的业务流程调用。
        /// </remarks>
        public async Task<(bool Success, string Error)> TryWriteSingleCoilAsync(ushort address, bool value)
        {
            PlcCallResult<bool> result = await ExecuteAsync(
                operation: "WriteSingleCoil",
                action: async master =>
                {
                    await master.WriteSingleCoilAsync(SlaveAddress, address, value).ConfigureAwait(false);
                    return true;
                },
                fallback: false).ConfigureAwait(false);

            return result.Success
                ? (true, string.Empty)
                : (false, result.Error?.Message ?? "Unknown PLC error.");
        }

        /// <summary>
        /// 写入单个保持寄存器，失败时抛出异常。
        /// </summary>
        /// By:ChengLei
        /// <param name="address">寄存器地址。</param>
        /// <param name="value">寄存器写入值。</param>
        /// <returns>返回写入异步任务。</returns>
        /// <remarks>
        /// 由参数下发和坐标写入场景调用。
        /// </remarks>
        public async Task WriteSingleRegisterAsync(ushort address, ushort value)
        {
            PlcCallResult<bool> result = await ExecuteAsync(
                operation: "WriteSingleRegister",
                action: async master =>
                {
                    await master.WriteSingleRegisterAsync(SlaveAddress, address, value).ConfigureAwait(false);
                    return true;
                },
                fallback: false).ConfigureAwait(false);

            if (!result.Success)
            {
                throw result.Error!;
            }
        }

        /// <summary>
        /// 尝试写入单个保持寄存器，失败时返回错误文本。
        /// </summary>
        /// By:ChengLei
        /// <param name="address">寄存器地址。</param>
        /// <param name="value">寄存器写入值。</param>
        /// <returns>返回成功标记和错误信息。</returns>
        /// <remarks>
        /// 由不希望抛异常的业务流程调用。
        /// </remarks>
        public async Task<(bool Success, string Error)> TryWriteSingleRegisterAsync(ushort address, ushort value)
        {
            PlcCallResult<bool> result = await ExecuteAsync(
                operation: "WriteSingleRegister",
                action: async master =>
                {
                    await master.WriteSingleRegisterAsync(SlaveAddress, address, value).ConfigureAwait(false);
                    return true;
                },
                fallback: false).ConfigureAwait(false);

            return result.Success
                ? (true, string.Empty)
                : (false, result.Error?.Message ?? "Unknown PLC error.");
        }

        /// <summary>
        /// 重置当前 Modbus 主站连接缓存。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由通信异常恢复流程调用，强制下次请求重建主站对象。
        /// </remarks>
        public void ResetConnection()
        {
            _masterGate.Wait();
            try
            {
                _master?.Dispose();
                _master = null;
                _masterPort = null;
            }
            finally
            {
                _masterGate.Release();
            }
        }

        /// <summary>
        /// 在统一互斥与异常包装下执行 PLC 调用。
        /// </summary>
        /// By:ChengLei
        /// <param name="operation">操作名称，用于错误上下文。</param>
        /// <param name="action">实际 Modbus 调用委托。</param>
        /// <param name="fallback">失败时返回的回退值。</param>
        /// <returns>返回封装后的调用结果对象。</returns>
        /// <remarks>
        /// 由全部读写 API 复用，统一超时与I/O异常处理策略。
        /// </remarks>
        private async Task<PlcCallResult<T>> ExecuteAsync<T>(
            string operation,
            Func<IModbusSerialMaster, Task<T>> action,
            T fallback)
        {
            if (operation == null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            await _masterGate.WaitAsync().ConfigureAwait(false);
            try
            {
                IModbusSerialMaster master = GetOrCreateMaster();
                T value = await action(master).ConfigureAwait(false);
                return PlcCallResult<T>.FromSuccess(value);
            }
            catch (Exception ex)
            {
                Exception wrapped = WrapCommunicationException(operation, ex);
                return PlcCallResult<T>.FromFailure(fallback, wrapped);
            }
            finally
            {
                _masterGate.Release();
            }
        }

        /// <summary>
        /// 获取可用的 Modbus 主站实例，不存在时创建并缓存。
        /// </summary>
        /// By:ChengLei
        /// <returns>返回可用的 Modbus 主站实例。</returns>
        /// <remarks>
        /// 由 ExecuteAsync 调用，并在串口变化时自动重建主站。
        /// </remarks>
        private IModbusSerialMaster GetOrCreateMaster()
        {
            if (!_rs485.IsOpen)
            {
                throw new InvalidOperationException("Serial port is not open.");
            }

            SerialPort? port = _rs485.Port;
            if (port == null || !port.IsOpen)
            {
                throw new InvalidOperationException("Serial port is not open.");
            }

            port.ReadTimeout = _responseTimeoutMs;
            port.WriteTimeout = _responseTimeoutMs;

            if (_master == null || !ReferenceEquals(_masterPort, port))
            {
                _master?.Dispose();
                ModbusFactory factory = new();
                _master = factory.CreateRtuMaster(new SerialPortStreamResource(port));
                _master.Transport.ReadTimeout = _responseTimeoutMs;
                _master.Transport.WriteTimeout = _responseTimeoutMs;
                _master.Transport.Retries = 1;
                _master.Transport.WaitToRetryMilliseconds = 80;
                _masterPort = port;
            }

            return _master;
        }

        /// <summary>
        /// 将底层异常包装为业务可读的通信异常。
        /// </summary>
        /// By:ChengLei
        /// <param name="operation">当前执行的操作名。</param>
        /// <param name="ex">底层捕获到的异常。</param>
        /// <returns>返回包装后的异常对象。</returns>
        /// <remarks>
        /// 由 ExecuteAsync 捕获异常后调用，区分超时、I/O和其他错误。
        /// </remarks>
        private Exception WrapCommunicationException(string operation, Exception ex)
        {
            if (ContainsTimeout(ex))
            {
                return new TimeoutException(
                    $"PLC通信超时({_responseTimeoutMs}ms)。请检查：1) PLC为Modbus RTU从站；2) 串口参数一致(波特率/8N1)；3) 站号={SlaveAddress}；4) 485接线(A/B/GND)与终端电阻。",
                    ex);
            }

            if (ContainsIo(ex))
            {
                return new InvalidOperationException(
                    $"PLC通信I/O失败({operation})：{ex.Message}。请检查串口占用和485接线。", ex);
            }

            return new InvalidOperationException(
                $"PLC communication failed during {operation}: {ex.Message}", ex);
        }

        /// <summary>
        /// 判断异常链中是否包含超时异常。
        /// </summary>
        /// By:ChengLei
        /// <param name="ex">待检查异常对象。</param>
        /// <returns>返回是否包含 TimeoutException。</returns>
        /// <remarks>
        /// 由 WrapCommunicationException 调用。
        /// </remarks>
        private static bool ContainsTimeout(Exception ex)
        {
            for (Exception? current = ex; current != null; current = current.InnerException)
            {
                if (current is TimeoutException)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 判断异常链中是否包含 I/O 异常。
        /// </summary>
        /// By:ChengLei
        /// <param name="ex">待检查异常对象。</param>
        /// <returns>返回是否包含 IOException。</returns>
        /// <remarks>
        /// 由 WrapCommunicationException 调用。
        /// </remarks>
        private static bool ContainsIo(Exception ex)
        {
            for (Exception? current = ex; current != null; current = current.InnerException)
            {
                if (current is IOException)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// PLC 调用结果封装结构体。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 仅在 Lx5vPlc 内部使用，用于承载成功结果或失败异常。
        /// </remarks>
        private readonly struct PlcCallResult<T>
        {
            /// <summary>
            /// 获取调用是否成功。
            /// </summary>
            /// By:ChengLei
            /// <returns>返回调用是否成功。</returns>
            /// <remarks>
            /// 由上层方法判断流程分支时读取。
            /// </remarks>
            public bool Success { get; }
            /// <summary>
            /// 获取调用结果值。
            /// </summary>
            /// By:ChengLei
            /// <returns>返回调用结果值。</returns>
            /// <remarks>
            /// 仅在 Success=true 时有效。
            /// </remarks>
            public T Value { get; }
            /// <summary>
            /// 获取调用失败异常对象。
            /// </summary>
            /// By:ChengLei
            /// <returns>返回失败异常对象，成功时为空。</returns>
            /// <remarks>
            /// 仅在 Success=false 时有效。
            /// </remarks>
            public Exception? Error { get; }

            /// <summary>
            /// 初始化 PLC 调用结果结构体。
            /// </summary>
            /// By:ChengLei
            /// <param name="success">是否成功。</param>
            /// <param name="value">结果值。</param>
            /// <param name="error">失败异常对象。</param>
            /// <remarks>
            /// 由 FromSuccess 和 FromFailure 工厂方法调用。
            /// </remarks>
            private PlcCallResult(bool success, T value, Exception? error)
            {
                Success = success;
                Value = value;
                Error = error;
            }

            /// <summary>
            /// 构建成功结果对象。
            /// </summary>
            /// By:ChengLei
            /// <param name="value">调用成功时的结果值。</param>
            /// <returns>返回成功状态的结果对象。</returns>
            /// <remarks>
            /// 由 ExecuteAsync 正常完成时调用。
            /// </remarks>
            public static PlcCallResult<T> FromSuccess(T value)
            {
                return new PlcCallResult<T>(true, value, null);
            }

            /// <summary>
            /// 构建失败结果对象。
            /// </summary>
            /// By:ChengLei
            /// <param name="fallback">失败时回退值。</param>
            /// <param name="error">失败异常对象。</param>
            /// <returns>返回失败状态的结果对象。</returns>
            /// <remarks>
            /// 由 ExecuteAsync 异常分支调用。
            /// </remarks>
            public static PlcCallResult<T> FromFailure(T fallback, Exception error)
            {
                return new PlcCallResult<T>(false, fallback, error);
            }
        }
    }
}
