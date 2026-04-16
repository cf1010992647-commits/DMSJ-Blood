using System;
using System.Text;

namespace Blood_Alcohol.Communication.Protocols
{
    /// <summary>
    /// 扫码枪协议服务。
    /// </summary>
    /// By:ChengLei
    /// <remarks>
    /// 负责把扫码枪返回字节清洗为条码文本，并提供可配置格式校验入口。
    /// </remarks>
    public class ScannerProtocolService
    {
        /// <summary>
        /// 条码格式校验委托。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 默认不限制具体业务格式；调用方可注入规则，避免协议层写死条码语义。
        /// </remarks>
        public Predicate<string>? CodeValidator { get; set; }

        /// <summary>
        /// 条码格式校验失败提示。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 当 CodeValidator 返回 false 时用于异常消息。
        /// </remarks>
        public string CodeValidationErrorMessage { get; set; } = "扫码条码格式校验失败";

        /// <summary>
        /// 将扫码枪返回数据转换为清洗后的条码字符串。
        /// </summary>
        /// By:ChengLei
        /// <param name="data">扫码枪返回的原始字节。</param>
        /// <returns>返回清洗后的条码文本。</returns>
        /// <remarks>
        /// 会移除 \0、回车、换行和不可见字符；清洗后为空会抛出异常。
        /// </remarks>
        public string ParseCode(byte[] data)
        {
            ValidateResponse(data);

            string code = SanitizeCode(Encoding.ASCII.GetString(data));
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new Exception("扫码结果为空");
            }

            if (CodeValidator != null && !CodeValidator(code))
            {
                throw new Exception(CodeValidationErrorMessage);
            }

            return code;
        }

        /// <summary>
        /// 校验扫码枪响应是否有数据。
        /// </summary>
        /// By:ChengLei
        /// <param name="data">扫码枪返回的原始字节。</param>
        /// <remarks>
        /// 仅校验帧级可用性，不引入具体条码业务规则。
        /// </remarks>
        private void ValidateResponse(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                throw new Exception("扫码数据为空");
            }
        }

        /// <summary>
        /// 清洗扫码枪返回文本。
        /// </summary>
        /// By:ChengLei
        /// <param name="text">原始扫码文本。</param>
        /// <returns>返回去除不可见字符后的文本。</returns>
        /// <remarks>
        /// 明确移除空字符、回车、换行，并过滤其他不可见控制字符。
        /// </remarks>
        private static string SanitizeCode(string text)
        {
            StringBuilder builder = new StringBuilder(text.Length);
            foreach (char current in text)
            {
                if (current == '\0' || current == '\r' || current == '\n')
                {
                    continue;
                }

                if (char.IsControl(current) || char.IsWhiteSpace(current))
                {
                    continue;
                }

                builder.Append(current);
            }

            return builder.ToString();
        }
    }
}
