using System;

namespace Blood_Alcohol.Protocols
{
    /// <summary>
    /// 天平 Modbus RTU 协议服务。
    /// </summary>
    /// By:ChengLei
    /// <remarks>
    /// 负责生成天平命令并严格校验响应帧后解析重量、状态和小数位。
    /// </remarks>
    public class BalanceProtocolService
    {
        private const byte SlaveAddress = 0x01;
        private const byte ReadFunctionCode = 0x03;

        #region Command

        /// <summary>
        /// 生成读取原始重量命令。
        /// </summary>
        /// By:ChengLei
        /// <returns>返回读取原始重量的 Modbus RTU 命令。</returns>
        /// <remarks>
        /// 保留现有命令内容不变，仅由调用方发送到天平。
        /// </remarks>
        public byte[] GetWeightCommand()
        {
            return new byte[]
            {
                0x01, 0x03, 0x00, 0x00,
                0x00, 0x02, 0xC4, 0x0B
            };
        }

        /// <summary>
        /// 生成读取小数位命令。
        /// </summary>
        /// By:ChengLei
        /// <returns>返回读取小数位的 Modbus RTU 命令。</returns>
        /// <remarks>
        /// 保留现有命令内容不变，仅由调用方发送到天平。
        /// </remarks>
        public byte[] GetDotCommand()
        {
            return new byte[]
            {
                0x01, 0x03, 0x00, 0x02,
                0x00, 0x01, 0x25, 0xCA
            };
        }

        /// <summary>
        /// 生成读取状态命令。
        /// </summary>
        /// By:ChengLei
        /// <returns>返回读取状态的 Modbus RTU 命令。</returns>
        /// <remarks>
        /// 保留现有命令内容不变，仅由调用方发送到天平。
        /// </remarks>
        public byte[] GetStatusCommand()
        {
            return new byte[]
            {
                0x01, 0x03, 0x00, 0x03,
                0x00, 0x01, 0x74, 0x0A
            };
        }

        /// <summary>
        /// 生成天平清零命令。
        /// </summary>
        /// By:ChengLei
        /// <returns>返回清零的 Modbus RTU 命令。</returns>
        /// <remarks>
        /// 保留现有命令内容不变，仅由调用方发送到天平。
        /// </remarks>
        public byte[] GetZeroCommand()
        {
            return new byte[]
            {
                0x01, 0x06, 0x00, 0x04,
                0x00, 0x01, 0x09, 0xCB
            };
        }

        /// <summary>
        /// 生成一次性读取重量和小数位命令。
        /// </summary>
        /// By:ChengLei
        /// <returns>返回一次性读取重量和小数位的 Modbus RTU 命令。</returns>
        /// <remarks>
        /// 保留现有命令内容不变，仅由称重流程调用。
        /// </remarks>
        public byte[] GetAllCommand()
        {
            return new byte[]
            {
                0x01, 0x03, 0x00, 0x00,
                0x00, 0x04, 0x44, 0x09
            };
        }

        #endregion

        #region Parse

        /// <summary>
        /// 解析小数位响应。
        /// </summary>
        /// By:ChengLei
        /// <param name="response">天平返回的响应帧。</param>
        /// <returns>返回小数位数。</returns>
        /// <remarks>
        /// 响应帧必须通过站号、功能码、字节数和 CRC 校验。
        /// </remarks>
        public int ParseDot(byte[] response)
        {
            ValidateResponse(response, expectedByteCount: 2);

            return response[4];
        }

        /// <summary>
        /// 解析原始重量响应。
        /// </summary>
        /// By:ChengLei
        /// <param name="response">天平返回的响应帧。</param>
        /// <returns>返回原始重量整数。</returns>
        /// <remarks>
        /// 响应帧必须通过站号、功能码、字节数和 CRC 校验。
        /// </remarks>
        public int ParseRawWeight(byte[] response)
        {
            ValidateResponse(response, expectedByteCount: 4);

            return
                (response[3] << 24) |
                (response[4] << 16) |
                (response[5] << 8) |
                response[6];
        }

        /// <summary>
        /// 解析状态响应。
        /// </summary>
        /// By:ChengLei
        /// <param name="response">天平返回的响应帧。</param>
        /// <returns>返回状态字。</returns>
        /// <remarks>
        /// 响应帧必须通过站号、功能码、字节数和 CRC 校验。
        /// </remarks>
        public ushort ParseStatus(byte[] response)
        {
            ValidateResponse(response, expectedByteCount: 2);

            return (ushort)((response[3] << 8) | response[4]);
        }

        /// <summary>
        /// 通过两次返回计算最终重量。
        /// </summary>
        /// By:ChengLei
        /// <param name="dotResponse">小数位响应帧。</param>
        /// <param name="weightResponse">原始重量响应帧。</param>
        /// <returns>返回换算后的重量。</returns>
        /// <remarks>
        /// 保留原有业务换算含义，仅在解析前加强帧校验。
        /// </remarks>
        public double ReadWeight(byte[] dotResponse, byte[] weightResponse)
        {
            int dot = ParseDot(dotResponse);
            int rawWeight = ParseRawWeight(weightResponse);

            return ConvertWeight(rawWeight, dot);
        }

        /// <summary>
        /// 从一次性返回帧直接计算重量。
        /// </summary>
        /// By:ChengLei
        /// <param name="allResponse">一次性读取返回帧。</param>
        /// <returns>返回换算后的重量。</returns>
        /// <remarks>
        /// 响应帧必须为站号 1、功能码 3、字节数 8 且 CRC 正确。
        /// </remarks>
        public double ReadWeight(byte[] allResponse)
        {
            ValidateAllResponse(allResponse);

            int rawWeight =
                (allResponse[3] << 24) |
                (allResponse[4] << 16) |
                (allResponse[5] << 8) |
                allResponse[6];

            int dot = allResponse[8];

            return ConvertWeight(rawWeight, dot);
        }

        /// <summary>
        /// 原始值转真实重量。
        /// </summary>
        /// By:ChengLei
        /// <param name="rawWeight">原始重量整数。</param>
        /// <param name="dot">小数位数。</param>
        /// <returns>返回真实重量。</returns>
        /// <remarks>
        /// 保留现有 rawWeight / 10^dot 的业务含义。
        /// </remarks>
        public double ConvertWeight(int rawWeight, int dot)
        {
            return rawWeight / Math.Pow(10, dot);
        }

        /// <summary>
        /// 校验一次性读取重量响应帧。
        /// </summary>
        /// By:ChengLei
        /// <param name="response">一次性读取返回帧。</param>
        /// <remarks>
        /// 用于工作流过滤坏帧和 ReadWeight 解析前校验。
        /// </remarks>
        public void ValidateAllResponse(byte[] response)
        {
            ValidateResponse(response, expectedByteCount: 8);
        }

        /// <summary>
        /// 尝试校验一次性读取重量响应帧。
        /// </summary>
        /// By:ChengLei
        /// <param name="response">一次性读取返回帧。</param>
        /// <param name="errorMessage">输出校验失败原因。</param>
        /// <returns>返回响应帧是否有效。</returns>
        /// <remarks>
        /// 由 WorkflowEngine 坏帧忽略逻辑调用，避免只按长度判断。
        /// </remarks>
        public bool TryValidateAllResponse(byte[] response, out string errorMessage)
        {
            try
            {
                ValidateAllResponse(response);
                errorMessage = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        #endregion

        #region Helper

        /// <summary>
        /// 严格校验天平读取响应帧。
        /// </summary>
        /// By:ChengLei
        /// <param name="data">待校验响应帧。</param>
        /// <param name="expectedByteCount">期望数据区字节数。</param>
        /// <remarks>
        /// 校验站号、功能码、异常帧、字节数、帧长度和 CRC16。
        /// </remarks>
        private void ValidateResponse(byte[] data, int expectedByteCount)
        {
            if (data == null || data.Length < 5)
            {
                throw new Exception("天平返回数据长度不足");
            }

            if (data[0] != SlaveAddress)
            {
                throw new Exception($"天平返回站号错误：expected={SlaveAddress:X2}, actual={data[0]:X2}");
            }

            ValidateCrc(data);

            if ((data[1] & 0x80) != 0)
            {
                byte originalFunction = (byte)(data[1] & 0x7F);
                byte exceptionCode = data.Length > 2 ? data[2] : (byte)0;
                throw new Exception($"天平返回异常帧：function={originalFunction:X2}, exception={exceptionCode:X2}");
            }

            if (data[1] != ReadFunctionCode)
            {
                throw new Exception($"天平返回功能码错误：expected={ReadFunctionCode:X2}, actual={data[1]:X2}");
            }

            if (data[2] != expectedByteCount)
            {
                throw new Exception($"天平返回字节数错误：expected={expectedByteCount}, actual={data[2]}");
            }

            int expectedLength = 3 + expectedByteCount + 2;
            if (data.Length != expectedLength)
            {
                throw new Exception($"天平返回帧长度错误：expected={expectedLength}, actual={data.Length}");
            }
        }

        /// <summary>
        /// 校验 Modbus RTU CRC16。
        /// </summary>
        /// By:ChengLei
        /// <param name="data">待校验响应帧。</param>
        /// <remarks>
        /// Modbus RTU CRC 低字节在前，高字节在后。
        /// </remarks>
        private static void ValidateCrc(byte[] data)
        {
            ushort actual = (ushort)(data[^2] | (data[^1] << 8));
            ushort expected = ComputeCrc16(data, data.Length - 2);
            if (actual != expected)
            {
                throw new Exception($"天平返回CRC错误：expected={expected:X4}, actual={actual:X4}");
            }
        }

        /// <summary>
        /// 计算 Modbus RTU CRC16。
        /// </summary>
        /// By:ChengLei
        /// <param name="data">待计算数据。</param>
        /// <param name="length">参与计算的字节数。</param>
        /// <returns>返回 CRC16 结果。</returns>
        /// <remarks>
        /// 使用 Modbus 多项式 0xA001。
        /// </remarks>
        private static ushort ComputeCrc16(byte[] data, int length)
        {
            ushort crc = 0xFFFF;
            for (int i = 0; i < length; i++)
            {
                crc ^= data[i];
                for (int bit = 0; bit < 8; bit++)
                {
                    bool lsb = (crc & 0x0001) != 0;
                    crc >>= 1;
                    if (lsb)
                    {
                        crc ^= 0xA001;
                    }
                }
            }

            return crc;
        }

        /// <summary>
        /// 将字节数组转换为 HEX 文本。
        /// </summary>
        /// By:ChengLei
        /// <param name="data">待转换字节数组。</param>
        /// <returns>返回 HEX 文本。</returns>
        /// <remarks>
        /// 用于通信日志输出。
        /// </remarks>
        public string ToHex(byte[] data)
        {
            return BitConverter.ToString(data).Replace("-", " ");
        }

        #endregion
    }
}
