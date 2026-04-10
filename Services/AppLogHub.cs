using System;
using System.Collections.Generic;
using System.Linq;

namespace Blood_Alcohol.Services
{
    public static class AppLogHub
    {
        private const int MaxBufferedLogs = 5000;
        private static readonly object SyncRoot = new();
        private static readonly List<AppLogEntry> Buffer = new();
        private static event Action<AppLogEntry>? LogPublished;

        public static void Initialize()
        {
            // no-op: ensures static ctor executed early by explicit call.
        }

        public static void Publish(AppLogEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            Action<AppLogEntry>? handlers;
            lock (SyncRoot)
            {
                Buffer.Insert(0, entry);
                if (Buffer.Count > MaxBufferedLogs)
                {
                    Buffer.RemoveAt(Buffer.Count - 1);
                }

                handlers = LogPublished;
            }

            handlers?.Invoke(entry);
        }

        public static IReadOnlyList<AppLogEntry> Snapshot()
        {
            lock (SyncRoot)
            {
                return Buffer.ToList();
            }
        }

        public static IDisposable Subscribe(IAppLogSink sink, bool replayBufferedLogs = true)
        {
            if (sink == null)
            {
                throw new ArgumentNullException(nameof(sink));
            }

            return Subscribe(sink.OnLog, replayBufferedLogs);
        }

        public static IDisposable Subscribe(Action<AppLogEntry> handler, bool replayBufferedLogs = true)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            List<AppLogEntry>? replayLogs = null;
            lock (SyncRoot)
            {
                LogPublished += handler;
                if (replayBufferedLogs && Buffer.Count > 0)
                {
                    replayLogs = Buffer.ToList();
                }
            }

            if (replayLogs != null)
            {
                foreach (AppLogEntry log in replayLogs)
                {
                    handler(log);
                }
            }

            return new Subscription(handler);
        }

        private sealed class Subscription : IDisposable
        {
            private Action<AppLogEntry>? _handler;

            public Subscription(Action<AppLogEntry> handler)
            {
                _handler = handler;
            }

            public void Dispose()
            {
                Action<AppLogEntry>? handler = _handler;
                if (handler == null)
                {
                    return;
                }

                lock (SyncRoot)
                {
                    LogPublished -= handler;
                }

                _handler = null;
            }
        }
    }
}
