using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

public class TcpServer
{
    public bool IsRunning => _isRunning;

    private TcpListener? _listener;
    private bool _isRunning;

    private readonly List<TcpClient> _clients = new List<TcpClient>();
    private readonly object _lock = new object();

    // DMSJ：回包缓存（全局 + 按端口）
    private readonly ConcurrentQueue<byte[]> _globalReceiveQueue = new ConcurrentQueue<byte[]>();
    private readonly SemaphoreSlim _globalReceiveSignal = new SemaphoreSlim(0);
    private readonly ConcurrentDictionary<int, ConcurrentQueue<byte[]>> _portReceiveQueues = new ConcurrentDictionary<int, ConcurrentQueue<byte[]>>();
    private readonly ConcurrentDictionary<int, SemaphoreSlim> _portReceiveSignals = new ConcurrentDictionary<int, SemaphoreSlim>();

    // DMSJ：通信回调允许无订阅者，声明为可空避免不必要告警。
    public Action<string>? OnMessageReceived;
    public Action<string>? OnClientConnected;

    /// <summary>
    /// 启动服务器
    /// </summary>
    public void Start(int port)
    {
        if (_isRunning)
        {
            return;
        }

        _listener = new TcpListener(IPAddress.Any, port);
        _listener.Start();

        _isRunning = true;

        Task.Run(AcceptClients);

        OnMessageReceived?.Invoke($"服务器启动，端口：{port}");
    }

    /// <summary>
    /// 停止服务器
    /// </summary>
    public void Stop()
    {
        _isRunning = false;

        lock (_lock)
        {
            foreach (TcpClient client in _clients)
            {
                try
                {
                    client.Close();
                }
                catch
                {
                }
            }

            _clients.Clear();
        }

        try
        {
            _listener?.Stop();
            _listener = null;
        }
        catch
        {
        }

        while (_globalReceiveQueue.TryDequeue(out _))
        {
        }

        foreach (ConcurrentQueue<byte[]> queue in _portReceiveQueues.Values)
        {
            while (queue.TryDequeue(out _))
            {
            }
        }

        OnMessageReceived?.Invoke("服务器已停止");
    }

    /// <summary>
    /// 发送给所有客户端
    /// </summary>
    public async Task SendToAll(byte[] data)
    {
        List<TcpClient> disconnectedClients = new List<TcpClient>();

        lock (_lock)
        {
            foreach (TcpClient client in _clients)
            {
                try
                {
                    if (client.Connected)
                    {
                        NetworkStream stream = client.GetStream();
                        stream.Write(data, 0, data.Length);

                        OnMessageReceived?.Invoke(
                            $"发送HEX: {BitConverter.ToString(data).Replace("-", " ")}");
                    }
                }
                catch
                {
                    disconnectedClients.Add(client);
                }
            }

            foreach (TcpClient client in disconnectedClients)
            {
                _clients.Remove(client);
            }
        }

        await Task.CompletedTask;
    }

    public async Task SendToPort(int port, byte[] data)
    {
        TcpClient? targetClient = null;

        lock (_lock)
        {
            foreach (TcpClient client in _clients)
            {
                try
                {
                    IPEndPoint? remoteEndPoint = client.Client.RemoteEndPoint as IPEndPoint;
                    if (remoteEndPoint != null && remoteEndPoint.Port == port)
                    {
                        targetClient = client;
                        break;
                    }
                }
                catch
                {
                }
            }
        }

        if (targetClient == null)
        {
            OnMessageReceived?.Invoke($"未找到端口 {port} 对应客户端");
            return;
        }

        try
        {
            NetworkStream stream = targetClient.GetStream();
            await stream.WriteAsync(data, 0, data.Length);

            OnMessageReceived?.Invoke(
                $"定向发送[{port}] HEX: {BitConverter.ToString(data).Replace("-", " ")}");
        }
        catch (Exception ex)
        {
            OnMessageReceived?.Invoke($"发送失败[{port}]: {ex.Message}");
        }
    }

    public List<int> GetConnectedPorts()
    {
        List<int> ports = new List<int>();

        lock (_lock)
        {
            foreach (TcpClient client in _clients)
            {
                try
                {
                    IPEndPoint? endPoint = client.Client.RemoteEndPoint as IPEndPoint;
                    if (endPoint != null)
                    {
                        ports.Add(endPoint.Port);
                    }
                }
                catch
                {
                }
            }
        }

        return ports;
    }

    /// <summary>
    /// 等待一次返回数据（全局）
    /// </summary>
    public Task<byte[]> ReceiveOnceAsync(CancellationToken token = default)
    {
        return ReceiveFromQueueAsync(_globalReceiveQueue, _globalReceiveSignal, token);
    }

    /// <summary>
    /// 等待指定端口一次返回数据
    /// </summary>
    public Task<byte[]> ReceiveOnceFromPortAsync(int port, CancellationToken token = default)
    {
        ConcurrentQueue<byte[]> queue = _portReceiveQueues.GetOrAdd(port, _ => new ConcurrentQueue<byte[]>());
        SemaphoreSlim signal = _portReceiveSignals.GetOrAdd(port, _ => new SemaphoreSlim(0));
        return ReceiveFromQueueAsync(queue, signal, token);
    }

    private static async Task<byte[]> ReceiveFromQueueAsync(
        ConcurrentQueue<byte[]> queue,
        SemaphoreSlim signal,
        CancellationToken token)
    {
        while (true)
        {
            if (queue.TryDequeue(out byte[]? data))
            {
                return data;
            }

            await signal.WaitAsync(token);
        }
    }

    /// <summary>
    /// 接收客户端连接
    /// </summary>
    private async Task AcceptClients()
    {
        TcpListener? listener = _listener;
        if (listener == null)
        {
            return;
        }

        while (_isRunning)
        {
            try
            {
                TcpClient client = await listener.AcceptTcpClientAsync();

                lock (_lock)
                {
                    _clients.Add(client);
                }

                string clientInfo =
                    (client.Client.RemoteEndPoint as IPEndPoint)?.ToString()
                    ?? "未知客户端";

                OnClientConnected?.Invoke($"客户端连接: {clientInfo}");
                HandleClient(client);
            }
            catch (Exception ex)
            {
                OnMessageReceived?.Invoke($"监听异常: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 处理客户端接收
    /// </summary>
    private void HandleClient(TcpClient client)
    {
        Task.Run(async () =>
        {
            NetworkStream stream = client.GetStream();
            byte[] buffer = new byte[1024];

            while (_isRunning)
            {
                int length;
                try
                {
                    length = await stream.ReadAsync(buffer, 0, buffer.Length);
                }
                catch (Exception ex)
                {
                    OnMessageReceived?.Invoke($"接收异常: {ex.Message}");
                    break;
                }

                if (length == 0)
                {
                    OnMessageReceived?.Invoke("客户端断开连接");
                    break;
                }

                try
                {
                    byte[] recv = new byte[length];
                    Array.Copy(buffer, recv, length);

                    string hexMsg = BitConverter.ToString(recv).Replace("-", " ");
                    OnMessageReceived?.Invoke($"收到HEX: {hexMsg}");

                    CacheReceivedData(client, recv);
                }
                catch (Exception ex)
                {
                    OnMessageReceived?.Invoke($"解析异常: {ex.Message}");
                }
            }

            lock (_lock)
            {
                _clients.Remove(client);
            }

            try
            {
                client.Close();
            }
            catch
            {
            }
        });
    }

    private void CacheReceivedData(TcpClient client, byte[] recv)
    {
        _globalReceiveQueue.Enqueue(recv);
        _globalReceiveSignal.Release();

        int? remotePort = (client.Client.RemoteEndPoint as IPEndPoint)?.Port;
        if (!remotePort.HasValue)
        {
            return;
        }

        ConcurrentQueue<byte[]> queue = _portReceiveQueues.GetOrAdd(remotePort.Value, _ => new ConcurrentQueue<byte[]>());
        SemaphoreSlim signal = _portReceiveSignals.GetOrAdd(remotePort.Value, _ => new SemaphoreSlim(0));

        queue.Enqueue(recv);
        signal.Release();
    }
}
