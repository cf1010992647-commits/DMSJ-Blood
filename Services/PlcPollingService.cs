using Blood_Alcohol.Communication.Serial;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Blood_Alcohol.Services
{
    public sealed class PlcPollingService : IDisposable
    {
        public readonly struct CoilSnapshot
        {
            public bool Success { get; }
            public bool Value { get; }
            public string Error { get; }
            public DateTime TimestampUtc { get; }

            public CoilSnapshot(bool success, bool value, string error, DateTime timestampUtc)
            {
                Success = success;
                Value = value;
                Error = error ?? string.Empty;
                TimestampUtc = timestampUtc;
            }
        }

        private sealed class CoilSubscription
        {
            public ushort Address { get; init; }
            public int RefCount { get; set; }
            public TimeSpan Interval { get; set; }
            public DateTime NextDueUtc { get; set; }
            public CoilSnapshot Snapshot { get; set; }
        }

        private readonly object _syncRoot = new();
        private readonly Dictionary<ushort, CoilSubscription> _coilSubs = new();
        private readonly Lx5vPlc _plc;
        private readonly SemaphoreSlim _plcLock;
        private readonly Func<bool> _isOnline;
        private static readonly TimeSpan StopTimeout = TimeSpan.FromMilliseconds(1200);

        private CancellationTokenSource? _cts;
        private Task? _workerTask;

        public PlcPollingService(Lx5vPlc plc, SemaphoreSlim plcLock, Func<bool> isOnline)
        {
            _plc = plc ?? throw new ArgumentNullException(nameof(plc));
            _plcLock = plcLock ?? throw new ArgumentNullException(nameof(plcLock));
            _isOnline = isOnline ?? throw new ArgumentNullException(nameof(isOnline));
        }

        public void RegisterCoil(ushort address, TimeSpan interval)
        {
            if (interval <= TimeSpan.Zero)
            {
                interval = TimeSpan.FromMilliseconds(200);
            }

            lock (_syncRoot)
            {
                if (_coilSubs.TryGetValue(address, out CoilSubscription? sub))
                {
                    sub.RefCount++;
                    if (interval < sub.Interval)
                    {
                        sub.Interval = interval;
                    }
                }
                else
                {
                    _coilSubs[address] = new CoilSubscription
                    {
                        Address = address,
                        RefCount = 1,
                        Interval = interval,
                        NextDueUtc = DateTime.UtcNow,
                        Snapshot = new CoilSnapshot(false, false, "No data yet.", DateTime.MinValue)
                    };
                }
            }

            EnsureRunning();
        }

        public void UnregisterCoil(ushort address)
        {
            lock (_syncRoot)
            {
                if (!_coilSubs.TryGetValue(address, out CoilSubscription? sub))
                {
                    return;
                }

                sub.RefCount--;
                if (sub.RefCount <= 0)
                {
                    _coilSubs.Remove(address);
                }
            }
        }

        public bool TryGetCoil(ushort address, TimeSpan maxAge, out CoilSnapshot snapshot)
        {
            if (maxAge <= TimeSpan.Zero)
            {
                maxAge = TimeSpan.FromMilliseconds(1000);
            }

            lock (_syncRoot)
            {
                if (_coilSubs.TryGetValue(address, out CoilSubscription? sub))
                {
                    CoilSnapshot current = sub.Snapshot;
                    if (current.TimestampUtc != DateTime.MinValue
                        && DateTime.UtcNow - current.TimestampUtc <= maxAge)
                    {
                        snapshot = current;
                        return true;
                    }

                    snapshot = current;
                    return false;
                }
            }

            snapshot = default;
            return false;
        }

        public void Start()
        {
            EnsureRunning();
        }

        public void Stop()
        {
            StopAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// 异步停止 PLC 轮询服务并等待后台任务退出。
        /// </summary>
        /// By:ChengLei
        /// <param name="token">取消令牌，用于中断停机等待。</param>
        /// <returns>返回异步停机任务。</returns>
        /// <remarks>
        /// 由应用退出和 Dispose 调用，取消轮询后最多等待限定时间。
        /// </remarks>
        public async Task StopAsync(CancellationToken token = default)
        {
            CancellationTokenSource? cts;
            Task? worker;
            lock (_syncRoot)
            {
                cts = _cts;
                worker = _workerTask;
                _cts = null;
                _workerTask = null;
            }

            if (cts == null)
            {
                return;
            }

            cts.Cancel();
            try
            {
                if (worker != null)
                {
                    await worker.WaitAsync(StopTimeout, token).ConfigureAwait(false);
                }
            }
            catch (TimeoutException)
            {
                Trace.TraceWarning($"PLC轮询服务停止超时（{StopTimeout.TotalMilliseconds:F0}ms）。");
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                Trace.TraceWarning("PLC轮询服务停机等待被取消。");
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"PLC轮询服务停止异常：{ex.Message}");
            }
            finally
            {
                cts.Dispose();
            }
        }

        private void EnsureRunning()
        {
            lock (_syncRoot)
            {
                if (_workerTask != null && !_workerTask.IsCompleted)
                {
                    return;
                }

                _cts = new CancellationTokenSource();
                _workerTask = Task.Run(() => PollLoopAsync(_cts.Token));
            }
        }

        private async Task PollLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                List<ushort> dueAddresses;
                int nextDelayMs;
                DateTime now = DateTime.UtcNow;

                lock (_syncRoot)
                {
                    if (_coilSubs.Count == 0)
                    {
                        dueAddresses = new List<ushort>();
                        nextDelayMs = 200;
                    }
                    else
                    {
                        dueAddresses = _coilSubs.Values
                            .Where(x => x.NextDueUtc <= now)
                            .Select(x => x.Address)
                            .ToList();

                        if (dueAddresses.Count > 0)
                        {
                            nextDelayMs = 1;
                        }
                        else
                        {
                            TimeSpan minWait = _coilSubs.Values
                                .Select(x => x.NextDueUtc - now)
                                .OrderBy(x => x)
                                .FirstOrDefault();
                            nextDelayMs = (int)Math.Clamp(minWait.TotalMilliseconds, 20, 300);
                        }
                    }
                }

                if (dueAddresses.Count == 0)
                {
                    await Task.Delay(nextDelayMs, token).ConfigureAwait(false);
                    continue;
                }

                if (!_isOnline())
                {
                    DateTime ts = DateTime.UtcNow;
                    lock (_syncRoot)
                    {
                        foreach (ushort address in dueAddresses)
                        {
                            if (_coilSubs.TryGetValue(address, out CoilSubscription? sub))
                            {
                                sub.Snapshot = new CoilSnapshot(
                                    success: false,
                                    value: false,
                                    error: "RS485 offline.",
                                    timestampUtc: ts);
                                sub.NextDueUtc = ts + sub.Interval;
                            }
                        }
                    }

                    await Task.Delay(120, token).ConfigureAwait(false);
                    continue;
                }

                foreach (ushort address in dueAddresses)
                {
                    if (token.IsCancellationRequested)
                    {
                        break;
                    }

                    CoilSnapshot snapshot = await PollSingleCoilAsync(address, token).ConfigureAwait(false);
                    lock (_syncRoot)
                    {
                        if (_coilSubs.TryGetValue(address, out CoilSubscription? sub))
                        {
                            sub.Snapshot = snapshot;
                            sub.NextDueUtc = DateTime.UtcNow + sub.Interval;
                        }
                    }
                }
            }
        }

        private async Task<CoilSnapshot> PollSingleCoilAsync(ushort address, CancellationToken token)
        {
            DateTime ts = DateTime.UtcNow;
            try
            {
                await _plcLock.WaitAsync(token).ConfigureAwait(false);
                try
                {
                    var read = await _plc.TryReadCoilsAsync(address, 1).ConfigureAwait(false);
                    if (!read.Success)
                    {
                        return new CoilSnapshot(false, false, read.Error, ts);
                    }

                    bool value = read.Values.Length != 0 && read.Values[0];
                    return new CoilSnapshot(true, value, string.Empty, ts);
                }
                finally
                {
                    _plcLock.Release();
                }
            }
            catch (Exception ex)
            {
                return new CoilSnapshot(false, false, ex.Message, ts);
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
