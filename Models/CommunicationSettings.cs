using System.Collections.Generic;
using System.Net;

namespace Blood_Alcohol.Models
{
    /// <summary>
    /// 通信配置模型。
    /// </summary>
    public class CommunicationSettings
    {
        /// <summary>
        /// RS485 串口号。
        /// </summary>
        public string ComPort { get; set; } = "COM1";

        /// <summary>
        /// RS485 波特率。
        /// </summary>
        public int BaudRate { get; set; } = 9600;

        /// <summary>
        /// TCP 服务监听端口。
        /// </summary>
        public int TcpPort { get; set; } = 20108;

        /// <summary>
        /// TCP 服务监听地址。
        /// </summary>
        public string TcpIP { get; set; } = "127.0.0.1";

        /// <summary>
        /// TCP 设备映射列表。
        /// </summary>
        public List<TcpDeviceMapping> TcpDevices { get; set; }
            = new List<TcpDeviceMapping>
            {
                new TcpDeviceMapping { Port = 9001, DeviceType = "温控", DeviceKey = "温控" },
                new TcpDeviceMapping { Port = 9002, DeviceType = "扫码枪", DeviceKey = "扫码枪" },
                new TcpDeviceMapping { Port = 9003, DeviceType = "天平", DeviceKey = "天平" },
                new TcpDeviceMapping { Port = 9004, DeviceType = "待定", DeviceKey = "待定" }
            };

        /// <summary>
        /// 校验通信配置是否合法。
        /// </summary>
        /// <returns>返回配置错误列表，列表为空表示校验通过。</returns>
        public List<string> Validate()
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(ComPort))
            {
                errors.Add("串口号不能为空。");
            }

            if (BaudRate <= 0)
            {
                errors.Add("串口波特率必须大于 0。");
            }

            if (TcpPort <= 0 || TcpPort > 65535)
            {
                errors.Add("TCP监听端口必须在 1-65535 范围内。");
            }

            if (string.IsNullOrWhiteSpace(TcpIP))
            {
                errors.Add("TCP监听IP不能为空。");
            }
            ValidateTcpDevices(errors);
            return errors;
        }

        /// <summary>
        /// 校验 TCP 设备映射配置。
        /// </summary>
        /// <param name="errors">用于收集错误信息的列表。</param>
        private void ValidateTcpDevices(List<string> errors)
        {
            if (TcpDevices == null)
            {
                errors.Add("TCP设备映射不能为空。");
                return;
            }

            var usedPorts = new Dictionary<int, string>();
            for (int index = 0; index < TcpDevices.Count; index++)
            {
                TcpDeviceMapping? device = TcpDevices[index];
                if (device == null)
                {
                    errors.Add($"TCP设备映射第 {index + 1} 行不能为空。");
                    continue;
                }

                string rowName = string.IsNullOrWhiteSpace(device.DeviceType)
                    ? $"第 {index + 1} 行"
                    : device.DeviceType;

                if (string.IsNullOrWhiteSpace(device.DeviceType))
                {
                    errors.Add($"TCP设备映射{rowName}的 DeviceType 不能为空。");
                }

                if (string.IsNullOrWhiteSpace(device.DeviceKey))
                {
                    errors.Add($"TCP设备映射{rowName}的 DeviceKey 不能为空。");
                }

                if (device.Port <= 0 || device.Port > 65535)
                {
                    errors.Add($"TCP设备映射{rowName}的客户端端口必须在 1-65535 范围内。");
                }
                else if (usedPorts.TryGetValue(device.Port, out string? existingName))
                {
                    errors.Add($"TCP设备客户端端口重复：{device.Port}（{existingName}、{rowName}）。");
                }
                else
                {
                    usedPorts.Add(device.Port, rowName);
                }

                if (!string.IsNullOrWhiteSpace(device.ClientIp) && !IPAddress.TryParse(device.ClientIp, out _))
                {
                    errors.Add($"TCP设备映射{rowName}的 ClientIp 格式无效：{device.ClientIp}。");
                }
            }
        }
    }
}
