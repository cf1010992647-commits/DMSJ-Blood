using System;

namespace Blood_Alcohol.Services
{
    public enum AppLogLevel
    {
        Info,
        Warning,
        Error
    }

    public enum AppLogSource
    {
        System,
        Process,
        Debug,
        Hardware
    }

    public enum AppLogKind
    {
        Operation,
        Detection
    }

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

    public interface IAppLogSink
    {
        void OnLog(AppLogEntry entry);
    }
}
