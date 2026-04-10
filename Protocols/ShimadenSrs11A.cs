using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blood_Alcohol.Communication.Protocols
{
    public class ShimadenSrs11A
    {
        private const byte STX = 0x02;
        private const byte ETX = 0x03;
        private const byte CR = 0x0D;

        public string Station { get; set; } = "01";
        public string SubAddress { get; set; } = "1";

        public ShimadenSrs11A()
        {
        }

        public ShimadenSrs11A(string station, string subAddress = "1")
        {
            Station = station.PadLeft(2, '0');
            SubAddress = subAddress;
        }

        /// <summary>
        /// 读取寄存器命令
        /// 例如：0100=PV，0300=SV
        /// </summary>
        public byte[] ReadRegister(string register)
        {
            string body = $"{Station}{SubAddress}R{register}0";
            return BuildFrame(body);
        }

        /// <summary>
        /// 写寄存器命令
        /// </summary>
        public byte[] WriteRegister(string register, int value)
        {
            string valueStr = value.ToString("D4");
            string body = $"{Station}{SubAddress}W{register}0,{valueStr}";
            return BuildFrame(body);
        }

        /// <summary>
        /// 设置温度（自动乘10）
        /// </summary>
        public byte[] SetTemperature(double temperature)
        {
            int value = (int)(temperature * 10);
            return WriteRegister("0300", value);
        }

        /// <summary>
        /// 读取当前温度PV
        /// </summary>
        public byte[] ReadPV()
        {
            return ReadRegister("0100");
        }

        /// <summary>
        /// 读取设定温度SV
        /// </summary>
        public byte[] ReadSV()
        {
            return ReadRegister("0300");
        }

        /// <summary>
        /// 解析返回温度
        /// </summary>
        public double ParseTemperature(byte[] response)
        {
            if (response == null || response.Length < 12)
                throw new Exception("温度返回长度不足");

            string text = Encoding.ASCII.GetString(response);

            int commaIndex = text.IndexOf(',');

            if (commaIndex < 0)
                throw new Exception("返回格式错误");

            string hexValue = text.Substring(commaIndex + 1, 4);

            int rawValue = Convert.ToInt32(hexValue, 16);

            return rawValue / 10.0;
        }

        /// <summary>
        /// 调试用：转HEX字符串
        /// </summary>
        public string ToHex(byte[] data)
        {
            return BitConverter.ToString(data).Replace("-", " ");
        }

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
    }
}
