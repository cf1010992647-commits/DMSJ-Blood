using System.Collections.Generic;

namespace Blood_Alcohol.Models
{
    public class CommunicationSettings
    {
        public string ComPort { get; set; } = "COM1";

        public int BaudRate { get; set; } = 9600;

        public int TcpPort { get; set; } = 20108;

        public string TcpIP { get; set; } = "127.0.0.1";

        public List<TcpDeviceMapping> TcpDevices { get; set; }
            = new List<TcpDeviceMapping>
            {
                new TcpDeviceMapping { Port = 9001, DeviceType = "温控" },
                new TcpDeviceMapping { Port = 9002, DeviceType = "扫码枪" },
                new TcpDeviceMapping { Port = 9003, DeviceType = "天平" },
                new TcpDeviceMapping { Port = 9004, DeviceType = "待定" }
            };
    }
}