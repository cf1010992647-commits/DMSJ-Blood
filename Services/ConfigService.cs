using System;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Blood_Alcohol.Services
{
    /// <summary>
    /// 泛型配置读写服务。
    /// </summary>
    /// <typeparam name="T">配置对象类型。</typeparam>
    public class ConfigService<T> where T : new()
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        private readonly string _fileName;

        private string ConfigFolder =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config");

        private string ConfigPath =>
            Path.Combine(ConfigFolder, _fileName);

        /// <summary>
        /// 初始化配置读写服务。
        /// </summary>
        /// <param name="fileName">配置文件名。</param>
        public ConfigService(string fileName)
        {
            _fileName = fileName;
        }

        /// <summary>
        /// 保存配置对象到本地配置文件。
        /// </summary>
        /// <param name="config">待保存的配置对象。</param>
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
        /// 从本地配置文件加载配置对象。
        /// </summary>
        /// <returns>返回配置对象，文件不存在或内容为空时返回默认实例。</returns>
        public T Load()
        {
            if (!File.Exists(ConfigPath))
            {
                return new T();
            }

            string json = File.ReadAllText(ConfigPath);
            json = NormalizeBareClientIpLiterals(json);
            T config = JsonSerializer.Deserialize<T>(json) ?? new T();

            TryNormalizeLegacyUnicodeEscapes(json, config);
            return config;
        }

        /// <summary>
        /// 修正未加引号的 ClientIp 字面量。
        /// </summary>
        /// <param name="sourceJson">原始 JSON 文本。</param>
        /// <returns>返回可被 JSON 解析器读取的配置文本。</returns>
        private string NormalizeBareClientIpLiterals(string sourceJson)
        {
            string normalized = Regex.Replace(
                sourceJson,
                "(\"ClientIp\"\\s*:\\s*)(\\d{1,3}(?:\\.\\d{1,3}){3})(\\s*[,}])",
                "$1\"$2\"$3");

            if (!string.Equals(sourceJson, normalized, StringComparison.Ordinal))
            {
                File.WriteAllText(ConfigPath, normalized);
            }

            return normalized;
        }

        /// <summary>
        /// 检测并回写历史 Unicode 转义配置。
        /// </summary>
        /// <param name="sourceJson">原始配置文本。</param>
        /// <param name="config">已反序列化的配置对象。</param>
        private void TryNormalizeLegacyUnicodeEscapes(string sourceJson, T config)
        {
            if (!sourceJson.Contains("\\u", StringComparison.Ordinal))
            {
                return;
            }

            string normalizedJson = JsonSerializer.Serialize(config, JsonOptions);
            if (!string.Equals(sourceJson, normalizedJson, StringComparison.Ordinal))
            {
                File.WriteAllText(ConfigPath, normalizedJson);
            }
        }
    }
}
