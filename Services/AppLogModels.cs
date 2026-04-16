using System;

namespace Blood_Alcohol.Services
{
    /// <summary>
    /// 应用通用日志级别。
    /// </summary>
    public enum AppLogLevel
    {
        Info,
        Warning,
        Error
    }

    /// <summary>
    /// 应用通用日志来源。
    /// </summary>
    public enum AppLogSource
    {
        System,
        Process,
        Debug,
        Hardware
    }

    /// <summary>
    /// 应用通用日志类型。
    /// </summary>
    public enum AppLogKind
    {
        Operation,
        Detection
    }

    /// <summary>
    /// 首页日志级别。
    /// </summary>
    public enum HomeLogLevel
    {
        Info,
        Warning,
        Error
    }

    /// <summary>
    /// 首页日志来源。
    /// </summary>
    public enum HomeLogSource
    {
        System,
        Process,
        Debug,
        Hardware
    }

    /// <summary>
    /// 首页日志类型。
    /// </summary>
    public enum HomeLogKind
    {
        Operation,
        Detection
    }

    /// <summary>
    /// 应用日志条目。
    /// </summary>
    public sealed class AppLogEntry
    {
        public DateTime Timestamp { get; init; } = DateTime.Now;
        public AppLogLevel Level { get; init; } = AppLogLevel.Info;
        public AppLogSource Source { get; init; } = AppLogSource.System;
        public AppLogKind Kind { get; init; } = AppLogKind.Operation;
        public string Message { get; init; } = string.Empty;
        public int TubeIndex { get; init; }
        public bool PersistToFile { get; init; } = true;
    }

    /// <summary>
    /// 应用日志接收器。
    /// </summary>
    public interface IAppLogSink
    {
        /// <summary>
        /// 接收应用日志。
        /// </summary>
        /// <param name="entry">日志条目。</param>
        void OnLog(AppLogEntry entry);
    }
}
