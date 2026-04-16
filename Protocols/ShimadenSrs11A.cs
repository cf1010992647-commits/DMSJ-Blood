using System;
using System.Text;

namespace Blood_Alcohol.Communication.Protocols
{
    /// <summary>
    /// 岛电 SRS11A 温控器协议服务。
    /// </summary>
    /// By:ChengLei
    /// <remarks>
    /// 负责构造温控器读写帧并解析温度响应。
    /// </remarks>
    public class ShimadenSrs11A
    {
        private const byte STX = 0x02;
        private const byte ETX = 0x03;
        private const byte CR = 0x0D;

        public string Station { get; set; } = "01";
        public string SubAddress { get; set; } = "1";

        /// <summary>
        /// 初始化默认站号的温控协议服务。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 默认站号为 01，子地址为 1。
        /// </remarks>
        public ShimadenSrs11A()
        {
        }

        /// <summary>
        /// 初始化指定站号和子地址的温控协议服务。
        /// </summary>
        /// By:ChengLei
        /// <param name="station">站号。</param>
        /// <param name="subAddress">子地址。</param>
        /// <remarks>
        /// 站号会左补零为两位文本。
        /// </remarks>
        public ShimadenSrs11A(string station, string subAddress = "1")
        {
            Station = station.PadLeft(2, '0');
            SubAddress = subAddress;
        }

        /// <summary>
        /// 生成读取寄存器命令。
        /// </summary>
        /// By:ChengLei
        /// <param name="register">寄存器地址文本。</param>
        /// <returns>返回温控器读取命令帧。</returns>
        /// <remarks>
        /// 例如 0100 表示 PV，0300 表示 SV。
        /// </remarks>
        public byte[] ReadRegister(string register)
        {
            string body = $"{Station}{SubAddress}R{register}0";
            return BuildFrame(body);
        }

        /// <summary>
        /// 生成写寄存器命令。
        /// </summary>
        /// By:ChengLei
        /// <param name="register">寄存器地址文本。</param>
        /// <param name="value">写入值。</param>
        /// <returns>返回温控器写入命令帧。</returns>
        /// <remarks>
        /// 保留现有十进制四位格式化方式。
        /// </remarks>
        public byte[] WriteRegister(string register, int value)
        {
            string valueStr = value.ToString("D4");
            string body = $"{Station}{SubAddress}W{register}0,{valueStr}";
            return BuildFrame(body);
        }

        /// <summary>
        /// 生成设置温度命令。
        /// </summary>
        /// By:ChengLei
        /// <param name="temperature">目标温度。</param>
        /// <returns>返回设置温度命令帧。</returns>
        /// <remarks>
        /// 保留现有自动乘 10 后写入 0300 寄存器的业务含义。
        /// </remarks>
        public byte[] SetTemperature(double temperature)
        {
            int value = (int)(temperature * 10);
            return WriteRegister("0300", value);
        }

        /// <summary>
        /// 生成读取当前温度 PV 命令。
        /// </summary>
        /// By:ChengLei
        /// <returns>返回读取 PV 的命令帧。</returns>
        /// <remarks>
        /// 读取寄存器 0100。
        /// </remarks>
        public byte[] ReadPV()
        {
            return ReadRegister("0100");
        }

        /// <summary>
        /// 生成读取设定温度 SV 命令。
        /// </summary>
        /// By:ChengLei
        /// <returns>返回读取 SV 的命令帧。</returns>
        /// <remarks>
        /// 读取寄存器 0300。
        /// </remarks>
        public byte[] ReadSV()
        {
            return ReadRegister("0300");
        }

        /// <summary>
        /// 解析温控器返回温度。
        /// </summary>
        /// By:ChengLei
        /// <param name="response">温控器返回帧。</param>
        /// <returns>返回解析后的温度。</returns>
        /// <remarks>
        /// 校验 STX、ETX、CR、站号、子地址和逗号后的四位十六进制温度值。
        /// </remarks>
        public double ParseTemperature(byte[] response)
        {
            string body = ValidateAndGetBody(response);

            int commaIndex = body.IndexOf(',');
            if (commaIndex < 0)
            {
                throw new Exception("温控返回格式错误：缺少数据分隔符");
            }

            if (commaIndex + 5 > body.Length)
            {
                throw new Exception("温控返回格式错误：温度数据长度不足");
            }

            string hexValue = body.Substring(commaIndex + 1, 4);
            if (!IsHexText(hexValue))
            {
                throw new Exception($"温控返回格式错误：温度数据不是十六进制，value={hexValue}");
            }

            int rawValue = Convert.ToInt32(hexValue, 16);

            return rawValue / 10.0;
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

        /// <summary>
        /// 构造温控器协议帧。
        /// </summary>
        /// By:ChengLei
        /// <param name="body">帧体文本。</param>
        /// <returns>返回包含 STX、ETX 和 CR 的完整帧。</returns>
        /// <remarks>
        /// 由读写命令构造方法复用。
        /// </remarks>
        private byte[] BuildFrame(string body)
        {
            byte[] bodyBytes = Encoding.ASCII.GetBytes(body);
            byte[] frame = new byte[bodyBytes.Length + 3];

            frame[0] = STX;
            Array.Copy(bodyBytes, 0, frame, 1, bodyBytes.Length);
            frame[frame.Length - 2] = ETX;
            frame[frame.Length - 1] = CR;

            return frame;
        }

        /// <summary>
        /// 校验温控器响应并提取帧体。
        /// </summary>
        /// By:ChengLei
        /// <param name="response">温控器返回帧。</param>
        /// <returns>返回去除 STX、ETX 和 CR 后的帧体文本。</returns>
        /// <remarks>
        /// 只做可确定的帧级校验，不引入未确认的业务字段语义。
        /// </remarks>
        private string ValidateAndGetBody(byte[] response)
        {
            if (response == null || response.Length < 6)
            {
                throw new Exception("温控返回长度不足");
            }

            if (response[0] != STX)
            {
                throw new Exception($"温控返回帧头错误：expected=0x{STX:X2}, actual=0x{response[0]:X2}");
            }

            if (response[^2] != ETX)
            {
                throw new Exception($"温控返回帧尾错误：expected=0x{ETX:X2}, actual=0x{response[^2]:X2}");
            }

            if (response[^1] != CR)
            {
                throw new Exception($"温控返回结束符错误：expected=0x{CR:X2}, actual=0x{response[^1]:X2}");
            }

            string body = Encoding.ASCII.GetString(response, 1, response.Length - 3);
            string expectedPrefix = $"{Station}{SubAddress}";
            if (!body.StartsWith(expectedPrefix, StringComparison.Ordinal))
            {
                throw new Exception($"温控返回站号或子地址错误：expectedPrefix={expectedPrefix}, body={body}");
            }

            if (body.Length <= expectedPrefix.Length)
            {
                throw new Exception("温控返回帧体结构错误：缺少站号后的数据区");
            }

            return body;
        }

        /// <summary>
        /// 判断文本是否全部为十六进制字符。
        /// </summary>
        /// By:ChengLei
        /// <param name="text">待校验文本。</param>
        /// <returns>返回文本是否为十六进制字符。</returns>
        /// <remarks>
        /// 由 ParseTemperature 校验温度值字段时调用。
        /// </remarks>
        private static bool IsHexText(string text)
        {
            foreach (char current in text)
            {
                bool isHex =
                    current >= '0' && current <= '9' ||
                    current >= 'A' && current <= 'F' ||
                    current >= 'a' && current <= 'f';
                if (!isHex)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
