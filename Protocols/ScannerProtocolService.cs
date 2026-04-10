using System;
using System.Text;

namespace Blood_Alcohol.Communication.Protocols
{
    public class ScannerProtocolService
    {
        /// <summary>
        /// 将扫码枪返回的HEX数据转换为条码字符串
        /// 例如: 31 32 33 34 35 36 -> 123456
        /// </summary>
        public string ParseCode(byte[] data)
        {
            ValidateResponse(data);

            return Encoding.ASCII.GetString(data);
        }

        private void ValidateResponse(byte[] data)
        {
            if (data == null || data.Length == 0)
                throw new Exception("扫码数据为空");
        }
    }
}