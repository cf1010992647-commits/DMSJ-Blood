using System;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Blood_Alcohol.Services
{
    public class ConfigService<T> where T : new()
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            // DMSJ：保留中文原文，避免写入配置时被转义为 \uXXXX。
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        private readonly string _fileName;

        private string ConfigFolder =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config");

        private string ConfigPath =>
            Path.Combine(ConfigFolder, _fileName);

        public ConfigService(string fileName)
        {
            _fileName = fileName;
        }

        public void Save(T config)
        {
            if (!Directory.Exists(ConfigFolder))
                Directory.CreateDirectory(ConfigFolder);

            var json = JsonSerializer.Serialize(config, JsonOptions);

            File.WriteAllText(ConfigPath, json);
        }

        public T Load()
        {
            if (!File.Exists(ConfigPath))
                return new T();

            var json = File.ReadAllText(ConfigPath);
            var config = JsonSerializer.Deserialize<T>(json) ?? new T();

            TryNormalizeLegacyUnicodeEscapes(json, config);
            return config;
        }

        private void TryNormalizeLegacyUnicodeEscapes(string sourceJson, T config)
        {
            if (!sourceJson.Contains("\\u", StringComparison.Ordinal))
                return;

            // DMSJ：兼容历史配置，若检测到 \uXXXX 写法，则自动回写为中文可读格式。
            var normalizedJson = JsonSerializer.Serialize(config, JsonOptions);
            if (!string.Equals(sourceJson, normalizedJson, StringComparison.Ordinal))
            {
                File.WriteAllText(ConfigPath, normalizedJson);
            }
        }
    }
}
