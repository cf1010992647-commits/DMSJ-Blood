using System;

namespace Blood_Alcohol.Protocols
{
    public class BalanceProtocolService
    {
        #region Command

        public byte[] GetWeightCommand()
        {
            return new byte[]
            {
                0x01, 0x03, 0x00, 0x00,
                0x00, 0x02, 0xC4, 0x0B
            };
        }

        public byte[] GetDotCommand()
        {
            return new byte[]
            {
                0x01, 0x03, 0x00, 0x02,
                0x00, 0x01, 0x25, 0xCA
            };
        }

        public byte[] GetStatusCommand()
        {
            return new byte[]
            {
                0x01, 0x03, 0x00, 0x03,
                0x00, 0x01, 0x74, 0x0A
            };
        }

        public byte[] GetZeroCommand()
        {
            return new byte[]
            {
                0x01, 0x06, 0x00, 0x04,
                0x00, 0x01, 0x09, 0xCB
            };
        }

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

        public int ParseDot(byte[] response)
        {
            ValidateResponse(response, 7);

            return response[4];
        }

        public int ParseRawWeight(byte[] response)
        {
            ValidateResponse(response, 9);

            return
                (response[3] << 24) |
                (response[4] << 16) |
                (response[5] << 8) |
                response[6];
        }

        public ushort ParseStatus(byte[] response)
        {
            ValidateResponse(response, 7);

            return (ushort)((response[3] << 8) | response[4]);
        }

        /// <summary>
        /// 通过两次返回计算最终重量
        /// </summary>
        public double ReadWeight(byte[] dotResponse, byte[] weightResponse)
        {
            int dot = ParseDot(dotResponse);
            int rawWeight = ParseRawWeight(weightResponse);

            return ConvertWeight(rawWeight, dot);
        }

        /// <summary>
        /// 一次性返回直接计算重量
        /// </summary>
        public double ReadWeight(byte[] allResponse)
        {
            ValidateResponse(allResponse, 13);

            int rawWeight =
                (allResponse[3] << 24) |
                (allResponse[4] << 16) |
                (allResponse[5] << 8) |
                allResponse[6];

            int dot = allResponse[8];

            return ConvertWeight(rawWeight, dot);
        }

        /// <summary>
        /// 原始值转真实重量
        /// </summary>
        public double ConvertWeight(int rawWeight, int dot)
        {
            return rawWeight / Math.Pow(10, dot);
        }

        #endregion

        #region Helper

        private void ValidateResponse(byte[] data, int minLength)
        {
            if (data == null || data.Length < minLength)
                throw new Exception("返回数据长度不足");
        }

        public string ToHex(byte[] data)
        {
            return BitConverter.ToString(data).Replace("-", " ");
        }

        #endregion
    }
}