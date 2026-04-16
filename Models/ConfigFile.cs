using Blood_Alcohol.Services;

namespace Blood_Alcohol.Models
{
    /// <summary>
    /// 配置文件读写兼容包装。
    /// </summary>
    /// <remarks>
    /// 保留旧调用方的 ConfigFile 类型入口，真实读写统一委托给 ConfigService。
    /// </remarks>
    public class ConfigFile<T> where T : new()
    {
        private readonly ConfigService<T> _service;

        /// <summary>
        /// 初始化配置文件兼容包装。
        /// </summary>
        /// <param name="fileName">配置文件名。</param>
        public ConfigFile(string fileName)
        {
            FileName = fileName;
            _service = new ConfigService<T>(fileName);
        }

        /// <summary>
        /// 配置文件名。
        /// </summary>
        public string FileName { get; }

        /// <summary>
        /// 保存配置对象。
        /// </summary>
        /// <param name="config">待保存的配置对象。</param>
        public void Save(T config)
        {
            _service.Save(config);
        }

        /// <summary>
        /// 加载配置对象。
        /// </summary>
        /// <returns>返回已加载的配置对象，文件不存在时返回默认实例。</returns>
        public T Load()
        {
            return _service.Load();
        }
    }
}
