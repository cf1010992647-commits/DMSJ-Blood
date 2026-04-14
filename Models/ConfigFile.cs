using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Blood_Alcohol.Models
{
    /// <summary>
    /// 配置文件读写工具
    /// </summary>
    /// By:ChengLei
    /// <remarks>
    /// 负责在程序目录 Config 文件夹读写泛型配置对象并处理历史转义兼容
    /// </remarks>
    public class ConfigFile<T> where T : new()
    {
        // JSON 序列化选项
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            // 保留中文原文 避免写入配置时被转义为 \uXXXX
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        /// <summary>
        /// 配置文件名
        /// </summary>
        /// By:ChengLei
        public string FileName { get; }

        // 配置文件夹路径
        private string ConfigFolder => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config");

        // 当前配置文件完整路径
        private string ConfigPath => Path.Combine(ConfigFolder, FileName);

        /// <summary>
        /// 初始化配置文件读写对象
        /// </summary>
        /// By:ChengLei
        /// <param name="fileName">配置文件名</param>
        /// <remarks>
        /// 由各模块在构造配置服务时调用
        /// </remarks>
        public ConfigFile(string fileName)
        {
            FileName = fileName;
        }

        /// <summary>
        /// 保存配置对象到本地文件
        /// </summary>
        /// By:ChengLei
        /// <param name="config">待保存的配置对象</param>
        /// <remarks>
        /// 由业务模块在参数变更后调用
        /// </remarks>
        public void Save(T config)
        {
            if (!Directory.Exists(ConfigFolder))
            {
                Directory.CreateDirectory(ConfigFolder);
            }

            string json = JsonSerializer.Serialize(config, JsonOptions);
            File.WriteAllText(ConfigPath, json);
        }

        /// <summary>
        /// 从本地文件加载配置对象
        /// </summary>
        /// By:ChengLei
        /// <returns>返回配置对象 不存在时返回默认实例</returns>
        /// <remarks>
        /// 由业务模块初始化阶段调用
        /// </remarks>
        public T Load()
        {
            if (!File.Exists(ConfigPath))
            {
                return new T();
            }

            string json = File.ReadAllText(ConfigPath);
            T config = JsonSerializer.Deserialize<T>(json) ?? new T();

            TryNormalizeLegacyUnicodeEscapes(json, config);
            return config;
        }

        /// <summary>
        /// 检测并回写历史 unicode 转义配置
        /// </summary>
        /// By:ChengLei
        /// <param name="sourceJson">原始配置文本</param>
        /// <param name="config">已反序列化配置对象</param>
        /// <remarks>
        /// 由 Load 调用 用于兼容旧版配置中的 \\uXXXX 写法
        /// </remarks>
        private void TryNormalizeLegacyUnicodeEscapes(string sourceJson, T config)
        {
            if (!sourceJson.Contains("\\u", StringComparison.Ordinal))
            {
                return;
            }

            // 兼容历史配置 若检测到 \uXXXX 写法则自动回写为中文可读格式
            string normalizedJson = JsonSerializer.Serialize(config, JsonOptions);
            if (!string.Equals(sourceJson, normalizedJson, StringComparison.Ordinal))
            {
                File.WriteAllText(ConfigPath, normalizedJson);
            }
        }

        /// <summary>
        /// 通信配置模型
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 作为通信模块默认配置结构使用
        /// </remarks>
        public class CommunicationSettings
        {
            /// <summary>
            /// 串口号
            /// </summary>
            /// By:ChengLei
            public string ComPort { get; set; } = "COM1";
            /// <summary>
            /// 串口波特率
            /// </summary>
            /// By:ChengLei
            public int BaudRate { get; set; } = 9600;
            /// <summary>
            /// TCP 端口
            /// </summary>
            /// By:ChengLei
            public int TcpPort { get; set; } = 20108;
            /// <summary>
            /// TCP 地址
            /// </summary>
            /// By:ChengLei
            public string TcpIP { get; set; } = "127.0.0.1";

            /// <summary>
            /// TCP 设备映射列表
            /// </summary>
            /// By:ChengLei
            public List<TcpDeviceMapping> TcpDevices { get; set; } = new()
            {
                new TcpDeviceMapping { Port = 9001, DeviceType = "温控" },
                new TcpDeviceMapping { Port = 9002, DeviceType = "扫码枪" },
                new TcpDeviceMapping { Port = 9003, DeviceType = "天平" },
                new TcpDeviceMapping { Port = 9004, DeviceType = "待定" }
            };
        }
    }
}
