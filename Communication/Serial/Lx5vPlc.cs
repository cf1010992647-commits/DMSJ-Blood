using NModbus;
using System;
using System.IO;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;

namespace Blood_Alcohol.Communication.Serial
{
    public class Lx5vPlc
    {
        private readonly Rs485Helper _rs485;
        private readonly int _responseTimeoutMs;
        private readonly SemaphoreSlim _masterGate = new(1, 1);

        private IModbusSerialMaster? _master;
        private SerialPort? _masterPort;

        public byte SlaveAddress { get; }

        public Lx5vPlc(Rs485Helper rs485, byte slaveAddress = 1, int responseTimeoutMs = 3000)
        {
            _rs485 = rs485 ?? throw new ArgumentNullException(nameof(rs485));
            SlaveAddress = slaveAddress;
            _responseTimeoutMs = responseTimeoutMs > 0 ? responseTimeoutMs : 3000;
        }

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

        private readonly struct PlcCallResult<T>
        {
            public bool Success { get; }
            public T Value { get; }
            public Exception? Error { get; }

            private PlcCallResult(bool success, T value, Exception? error)
            {
                Success = success;
                Value = value;
                Error = error;
            }

            public static PlcCallResult<T> FromSuccess(T value)
            {
                return new PlcCallResult<T>(true, value, null);
            }

            public static PlcCallResult<T> FromFailure(T fallback, Exception error)
            {
                return new PlcCallResult<T>(false, fallback, error);
            }
        }
    }
}
