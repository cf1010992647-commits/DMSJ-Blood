namespace Blood_Alcohol.Helpers
{
    /// <summary>
    /// PLC 文本地址解析工具
    /// </summary>
    /// By:ChengLei
    /// <remarks>
    /// 用于将界面与配置中使用的 M D 文本地址解析为未偏移的显示地址
    /// </remarks>
    public static class PlcAddressMapper
    {
        /// <summary>
        /// 尝试解析 M 点位的显示地址编号
        /// </summary>
        /// By:ChengLei
        /// <param name="address">形如 M7 的文本地址</param>
        /// <returns>返回未偏移的显示地址编号 失败时返回空</returns>
        /// <remarks>
        /// 用于新增地址建议等仅需要显示编号的场景
        /// </remarks>
        public static ushort? TryParseCoilDisplayAddress(string address)
        {
            return TryParseDisplayAddress(address, "M");
        }

        /// <summary>
        /// 尝试解析 D 寄存器的显示地址编号
        /// </summary>
        /// By:ChengLei
        /// <param name="address">形如 D6000 的文本地址</param>
        /// <returns>返回未偏移的显示地址编号 失败时返回空</returns>
        /// <remarks>
        /// 用于仅需要显示编号或做配置检查的场景
        /// </remarks>
        public static ushort? TryParseRegisterDisplayAddress(string address)
        {
            return TryParseDisplayAddress(address, "D");
        }

        /// <summary>
        /// 尝试按指定前缀解析显示地址编号
        /// </summary>
        /// By:ChengLei
        /// <param name="address">待解析的文本地址</param>
        /// <param name="prefix">地址前缀 M 或 D</param>
        /// <returns>返回显示地址编号 失败时返回空</returns>
        /// <remarks>
        /// 由 M D 地址解析入口复用
        /// </remarks>
        private static ushort? TryParseDisplayAddress(string address, string prefix)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                return null;
            }

            string trimmed = address.Trim();
            if (!trimmed.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (!ushort.TryParse(trimmed.Substring(1), out ushort parsed))
            {
                return null;
            }

            return parsed;
        }
    }
}
