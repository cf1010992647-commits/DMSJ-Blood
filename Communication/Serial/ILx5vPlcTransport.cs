using System.Threading.Tasks;

namespace Blood_Alcohol.Communication.Serial
{
    /// <summary>
    /// LX5V PLC 传输抽象。
    /// </summary>
    /// By:ChengLei
    /// <remarks>
    /// 用于把 PLC 上层读写语义与具体 Modbus RTU 串口实现解耦，便于集成测试注入内存版传输。
    /// </remarks>
    public interface ILx5vPlcTransport
    {
        /// <summary>
        /// 读取保持寄存器。
        /// </summary>
        /// By:ChengLei
        /// <param name="slaveAddress">PLC 从站地址。</param>
        /// <param name="startAddress">起始寄存器地址。</param>
        /// <param name="length">读取寄存器数量。</param>
        /// <returns>返回读取到的寄存器数组。</returns>
        /// <remarks>
        /// 由 Lx5vPlc 在读取 D 寄存器时调用。
        /// </remarks>
        Task<ushort[]> ReadHoldingRegistersAsync(byte slaveAddress, ushort startAddress, ushort length);

        /// <summary>
        /// 读取线圈状态。
        /// </summary>
        /// By:ChengLei
        /// <param name="slaveAddress">PLC 从站地址。</param>
        /// <param name="startAddress">起始线圈地址。</param>
        /// <param name="length">读取线圈数量。</param>
        /// <returns>返回读取到的线圈状态数组。</returns>
        /// <remarks>
        /// 由 Lx5vPlc 在读取 M 位时调用。
        /// </remarks>
        Task<bool[]> ReadCoilsAsync(byte slaveAddress, ushort startAddress, ushort length);

        /// <summary>
        /// 写入单个线圈。
        /// </summary>
        /// By:ChengLei
        /// <param name="slaveAddress">PLC 从站地址。</param>
        /// <param name="address">线圈地址。</param>
        /// <param name="value">目标线圈值。</param>
        /// <returns>返回写入完成任务。</returns>
        /// <remarks>
        /// 由 Lx5vPlc 在写入 M 位控制命令时调用。
        /// </remarks>
        Task WriteSingleCoilAsync(byte slaveAddress, ushort address, bool value);

        /// <summary>
        /// 写入单个保持寄存器。
        /// </summary>
        /// By:ChengLei
        /// <param name="slaveAddress">PLC 从站地址。</param>
        /// <param name="address">寄存器地址。</param>
        /// <param name="value">寄存器值。</param>
        /// <returns>返回写入完成任务。</returns>
        /// <remarks>
        /// 由 Lx5vPlc 在写入 D 寄存器参数和坐标时调用。
        /// </remarks>
        Task WriteSingleRegisterAsync(byte slaveAddress, ushort address, ushort value);
    }
}
