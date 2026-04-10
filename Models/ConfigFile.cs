using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Blood_Alcohol.Models
{
    public class ConfigFile<T> where T : new()
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            // DMSJ：保留中文原文，避免写入配置时被转义为 \uXXXX。
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        public string FileName { get; }

        private string ConfigFolder => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config");

        private string ConfigPath => Path.Combine(ConfigFolder, FileName);

        public ConfigFile(string fileName)
        {
            FileName = fileName;
        }

        public void Save(T config)
        {
            if (!Directory.Exists(ConfigFolder))
            {
                Directory.CreateDirectory(ConfigFolder);
            }

            string json = JsonSerializer.Serialize(config, JsonOptions);
            File.WriteAllText(ConfigPath, json);
        }

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

        private void TryNormalizeLegacyUnicodeEscapes(string sourceJson, T config)
        {
            if (!sourceJson.Contains("\\u", StringComparison.Ordinal))
            {
                return;
            }

            // DMSJ：兼容历史配置，若检测到 \uXXXX 写法，则自动回写为中文可读格式。
            string normalizedJson = JsonSerializer.Serialize(config, JsonOptions);
            if (!string.Equals(sourceJson, normalizedJson, StringComparison.Ordinal))
            {
                File.WriteAllText(ConfigPath, normalizedJson);
            }
        }

        public class CommunicationSettings
        {
            public string ComPort { get; set; } = "COM1";
            public int BaudRate { get; set; } = 9600;
            public int TcpPort { get; set; } = 20108;
            public string TcpIP { get; set; } = "127.0.0.1";

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
