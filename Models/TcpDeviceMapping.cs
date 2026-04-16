namespace Blood_Alcohol.Models
{
    /// <summary>
    /// TCP 设备身份映射配置。
    /// </summary>
    /// By:ChengLei
    /// <remarks>
    /// 用于把设备类型、逻辑设备键、客户端地址和端口关联起来。
    /// </remarks>
    public class TcpDeviceMapping
    {
        private string _deviceKey = string.Empty;

        /// <summary>
        /// 旧版端口标识。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 仅用于兼容旧调用方，不再作为设备唯一身份。
        /// </remarks>
        public int Port { get; set; }

        /// <summary>
        /// 设备类型名称。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 用于界面显示和按设备类型查找映射。
        /// </remarks>
        public string DeviceType { get; set; } = "待定";

        /// <summary>
        /// 逻辑设备身份键。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 为空时回退使用 DeviceType，保证旧配置自动具备逻辑身份。
        /// </remarks>
        public string DeviceKey
        {
            get => string.IsNullOrWhiteSpace(_deviceKey) ? DeviceType : _deviceKey;
            set => _deviceKey = value ?? string.Empty;
        }

        /// <summary>
        /// 允许绑定的客户端 IP。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 与端口一起作为设备身份匹配条件。
        /// </remarks>
        public string? ClientIp { get; set; }

    }
}
