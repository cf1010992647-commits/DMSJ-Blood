using Blood_Alcohol.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// TCP 客户端会话。
/// </summary>
/// By:ChengLei
/// <remarks>
/// 保存单个设备连接、逻辑设备键、接收队列和远端地址。
/// </remarks>
public sealed class TcpClientSession
{
    /// <summary>
    /// TCP 客户端连接。
    /// </summary>
    /// By:ChengLei
    public required TcpClient Client { get; init; }

    /// <summary>
    /// 逻辑设备身份键。
    /// </summary>
    /// By:ChengLei
    public string DeviceKey { get; set; } = string.Empty;

    /// <summary>
    /// 当前设备的接收队列。
    /// </summary>
    /// By:ChengLei
    public ConcurrentQueue<byte[]> ReceiveQueue { get; } = new ConcurrentQueue<byte[]>();

    /// <summary>
    /// 当前设备接收信号。
    /// </summary>
    /// By:ChengLei
    public SemaphoreSlim ReceiveSignal { get; } = new SemaphoreSlim(0);

    /// <summary>
    /// 客户端远端地址。
    /// </summary>
    /// By:ChengLei
    public required IPEndPoint RemoteEndPoint { get; init; }
}

/// <summary>
/// TCP 服务端。
/// </summary>
/// By:ChengLei
/// <remarks>
/// 负责接收设备连接并按 DeviceKey 路由收发数据，同时保留旧端口 API 兼容层。
/// </remarks>
public class TcpServer
{
    private readonly object _lock = new object();
    private readonly List<TcpClient> _clients = new List<TcpClient>();
    private readonly Dictionary<TcpClient, TcpClientSession> _sessionsByClient = new Dictionary<TcpClient, TcpClientSession>();
    private readonly ConcurrentDictionary<string, TcpClientSession> _sessionsByDeviceKey = new ConcurrentDictionary<string, TcpClientSession>(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentQueue<byte[]> _globalReceiveQueue = new ConcurrentQueue<byte[]>();
    private readonly SemaphoreSlim _globalReceiveSignal = new SemaphoreSlim(0);
    private IReadOnlyList<TcpDeviceMapping> _deviceMappings = Array.Empty<TcpDeviceMapping>();
    private TcpListener? _listener;
    private bool _isRunning;

    /// <summary>
    /// 服务端是否正在监听。
    /// </summary>
    /// By:ChengLei
    public bool IsRunning => _isRunning;

    /// <summary>
    /// 当前监听端口。
    /// </summary>
    /// By:ChengLei
    public int ListeningPort
    {
        get
        {
            try
            {
                return ((IPEndPoint?)_listener?.LocalEndpoint)?.Port ?? 0;
            }
            catch
            {
                return 0;
            }
        }
    }

    /// <summary>
    /// TCP 消息回调。
    /// </summary>
    /// By:ChengLei
    public Action<string>? OnMessageReceived;

    /// <summary>
    /// TCP 客户端连接回调。
    /// </summary>
    /// By:ChengLei
    public Action<string>? OnClientConnected;

    /// <summary>
    /// 配置 TCP 设备身份映射。
    /// </summary>
    /// By:ChengLei
    /// <param name="mappings">设备映射集合。</param>
    /// <remarks>
    /// 由 CommunicationManager 在加载和保存通信配置后调用。
    /// </remarks>
    public void ConfigureDeviceMappings(IEnumerable<TcpDeviceMapping>? mappings)
    {
        _deviceMappings = (mappings ?? Array.Empty<TcpDeviceMapping>())
            .Where(x => !string.IsNullOrWhiteSpace(x.DeviceKey))
            .Select(CloneMapping)
            .ToList();
    }

    /// <summary>
    /// 启动 TCP 服务端。
    /// </summary>
    /// By:ChengLei
    /// <param name="port">监听端口。</param>
    /// <remarks>
    /// 由通信管理器或测试用例调用。
    /// </remarks>
    public void Start(int port)
    {
        if (_isRunning)
        {
            return;
        }

        _listener = new TcpListener(IPAddress.Any, port);
        _listener.Start();
        _isRunning = true;
        _ = Task.Run(AcceptClients);

        OnMessageReceived?.Invoke($"服务器启动，端口：{ListeningPort}");
    }

    /// <summary>
    /// 停止 TCP 服务端并清理会话。
    /// </summary>
    /// By:ChengLei
    /// <remarks>
    /// 由通信管理器停止 TCP 服务时调用。
    /// </remarks>
    public void Stop()
    {
        _isRunning = false;

        lock (_lock)
        {
            foreach (TcpClient client in _clients.ToList())
            {
                CloseClient(client);
            }

            _clients.Clear();
            _sessionsByClient.Clear();
        }

        _sessionsByDeviceKey.Clear();

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

        OnMessageReceived?.Invoke("服务器已停止");
    }

    /// <summary>
    /// 发送数据给所有已绑定设备。
    /// </summary>
    /// By:ChengLei
    /// <param name="data">待发送数据。</param>
    /// <returns>返回发送任务。</returns>
    /// <remarks>
    /// 保留广播能力，内部遍历当前 DeviceKey 会话。
    /// </remarks>
    public async Task SendToAll(byte[] data)
    {
        foreach (string deviceKey in GetConnectedDeviceKeys())
        {
            await SendToDeviceAsync(deviceKey, data).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 按逻辑设备键发送数据。
    /// </summary>
    /// By:ChengLei
    /// <param name="deviceKey">逻辑设备键。</param>
    /// <param name="data">待发送数据。</param>
    /// <returns>返回发送任务。</returns>
    /// <remarks>
    /// 新调用方应使用该方法替代 SendToPort。
    /// </remarks>
    public async Task SendToDeviceAsync(string deviceKey, byte[] data)
    {
        if (!_sessionsByDeviceKey.TryGetValue(deviceKey, out TcpClientSession? session))
        {
            string message = $"未找到 DeviceKey={deviceKey} 对应 TCP 客户端";
            OnMessageReceived?.Invoke(message);
            throw new InvalidOperationException(message);
        }

        try
        {
            NetworkStream stream = session.Client.GetStream();
            await stream.WriteAsync(data, 0, data.Length).ConfigureAwait(false);
            OnMessageReceived?.Invoke($"定向发送[{deviceKey}] HEX: {ToHex(data)}");
        }
        catch (Exception ex)
        {
            RemoveSession(session.Client);
            string message = $"发送失败[{deviceKey}]: {ex.Message}";
            OnMessageReceived?.Invoke(message);
            throw new InvalidOperationException(message, ex);
        }
    }

    /// <summary>
    /// 接收指定逻辑设备的一帧数据。
    /// </summary>
    /// By:ChengLei
    /// <param name="deviceKey">逻辑设备键。</param>
    /// <param name="token">取消令牌。</param>
    /// <returns>返回接收到的原始数据。</returns>
    /// <remarks>
    /// 只读取目标 DeviceKey 对应队列，其他设备数据不会混入。
    /// </remarks>
    public async Task<byte[]> ReceiveOnceFromDeviceAsync(string deviceKey, CancellationToken token = default)
    {
        TcpClientSession session = GetRequiredSession(deviceKey);
        return await ReceiveFromQueueAsync(session.ReceiveQueue, session.ReceiveSignal, token).ConfigureAwait(false);
    }

    /// <summary>
    /// 判断指定逻辑设备是否已连接。
    /// </summary>
    /// By:ChengLei
    /// <param name="deviceKey">逻辑设备键。</param>
    /// <returns>返回设备是否已连接。</returns>
    /// <remarks>
    /// 由流程启动前检查 TCP 设备连接状态时调用。
    /// </remarks>
    public bool IsDeviceConnected(string deviceKey)
    {
        return _sessionsByDeviceKey.TryGetValue(deviceKey, out TcpClientSession? session)
            && session.Client.Connected;
    }

    /// <summary>
    /// 获取当前已连接的逻辑设备键集合。
    /// </summary>
    /// By:ChengLei
    /// <returns>返回已连接设备键集合。</returns>
    /// <remarks>
    /// 用于通信页面展示在线设备。
    /// </remarks>
    public IReadOnlyCollection<string> GetConnectedDeviceKeys()
    {
        return _sessionsByDeviceKey.Keys.ToList();
    }

    /// <summary>
    /// 兼容旧版按端口发送数据。
    /// </summary>
    /// By:ChengLei
    /// <param name="port">旧版设备端口。</param>
    /// <param name="data">待发送数据。</param>
    /// <returns>返回发送任务。</returns>
    /// <remarks>
    /// 内部按配置端口查找 DeviceKey 后转发到 SendToDeviceAsync。
    /// </remarks>
    [Obsolete("请使用 SendToDeviceAsync(string deviceKey, byte[] data)。")]
    public Task SendToPort(int port, byte[] data)
    {
        string deviceKey = ResolveDeviceKeyByPort(port);
        return SendToDeviceAsync(deviceKey, data);
    }

    /// <summary>
    /// 兼容旧版获取已连接端口。
    /// </summary>
    /// By:ChengLei
    /// <returns>返回已连接设备对应的配置端口集合。</returns>
    /// <remarks>
    /// 返回值来自设备映射配置，不再使用 RemoteEndPoint.Port。
    /// </remarks>
    [Obsolete("请使用 GetConnectedDeviceKeys()。")]
    public List<int> GetConnectedPorts()
    {
        HashSet<string> connectedKeys = GetConnectedDeviceKeys().ToHashSet(StringComparer.OrdinalIgnoreCase);
        return _deviceMappings
            .Where(x => connectedKeys.Contains(x.DeviceKey) && x.Port > 0)
            .Select(x => x.Port)
            .Distinct()
            .ToList();
    }

    /// <summary>
    /// 接收全局队列中的一帧数据。
    /// </summary>
    /// By:ChengLei
    /// <param name="token">取消令牌。</param>
    /// <returns>返回接收到的原始数据。</returns>
    /// <remarks>
    /// 兼容历史全局接收调用，设备流程应优先使用 ReceiveOnceFromDeviceAsync。
    /// </remarks>
    public Task<byte[]> ReceiveOnceAsync(CancellationToken token = default)
    {
        return ReceiveFromQueueAsync(_globalReceiveQueue, _globalReceiveSignal, token);
    }

    /// <summary>
    /// 兼容旧版按端口接收数据。
    /// </summary>
    /// By:ChengLei
    /// <param name="port">旧版设备端口。</param>
    /// <param name="token">取消令牌。</param>
    /// <returns>返回接收到的原始数据。</returns>
    /// <remarks>
    /// 内部按配置端口查找 DeviceKey 后转发到 ReceiveOnceFromDeviceAsync。
    /// </remarks>
    [Obsolete("请使用 ReceiveOnceFromDeviceAsync(string deviceKey, CancellationToken token)。")]
    public Task<byte[]> ReceiveOnceFromPortAsync(int port, CancellationToken token = default)
    {
        string deviceKey = ResolveDeviceKeyByPort(port);
        return ReceiveOnceFromDeviceAsync(deviceKey, token);
    }

    /// <summary>
    /// 接收客户端连接。
    /// </summary>
    /// By:ChengLei
    /// <returns>返回客户端监听任务。</returns>
    /// <remarks>
    /// 由 Start 启动后台任务调用。
    /// </remarks>
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
                TcpClient client = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
                IPEndPoint? remoteEndPoint = client.Client.RemoteEndPoint as IPEndPoint;
                if (remoteEndPoint == null)
                {
                    OnMessageReceived?.Invoke("TCP客户端连接失败：无法识别远端地址");
                    CloseClient(client);
                    continue;
                }

                TcpClientSession session = new TcpClientSession
                {
                    Client = client,
                    RemoteEndPoint = remoteEndPoint
                };

                lock (_lock)
                {
                    _clients.Add(client);
                    _sessionsByClient[client] = session;
                }

                OnClientConnected?.Invoke($"客户端连接 {remoteEndPoint}");
                DeviceBindResult bindResult = TryBindSession(session);
                if (!bindResult.Success)
                {
                    OnMessageReceived?.Invoke(bindResult.ErrorMessage);
                }

                HandleClient(session);
            }
            catch (Exception ex)
            {
                if (_isRunning)
                {
                    OnMessageReceived?.Invoke($"监听异常: {ex.Message}");
                }
            }
        }
    }

    /// <summary>
    /// 处理单个客户端接收循环。
    /// </summary>
    /// By:ChengLei
    /// <param name="session">客户端会话。</param>
    /// <remarks>
    /// 由 AcceptClients 在每个客户端连接后调用。
    /// </remarks>
    private void HandleClient(TcpClientSession session)
    {
        _ = Task.Run(async () =>
        {
            NetworkStream stream = session.Client.GetStream();
            byte[] buffer = new byte[1024];

            while (_isRunning)
            {
                int length;
                try
                {
                    length = await stream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    OnMessageReceived?.Invoke($"接收异常: {ex.Message}");
                    break;
                }

                if (length == 0)
                {
                    OnMessageReceived?.Invoke($"客户端断开连接: {DescribeSession(session)}");
                    break;
                }

                byte[] recv = new byte[length];
                Array.Copy(buffer, recv, length);
                OnMessageReceived?.Invoke($"收到HEX: {ToHex(recv)}");
                CacheReceivedData(session, recv);
            }

            RemoveSession(session.Client);
            CloseClient(session.Client);
        });
    }

    /// <summary>
    /// 缓存接收到的数据。
    /// </summary>
    /// By:ChengLei
    /// <param name="session">客户端会话。</param>
    /// <param name="recv">接收数据。</param>
    /// <remarks>
    /// 未绑定设备时先解析身份，无法解析时记录错误且不进入任何设备队列。
    /// </remarks>
    private void CacheReceivedData(TcpClientSession session, byte[] recv)
    {
        if (string.IsNullOrWhiteSpace(session.DeviceKey))
        {
            DeviceBindResult bindResult = TryBindSession(session);
            if (!bindResult.Success)
            {
                OnMessageReceived?.Invoke(bindResult.ErrorMessage);
                return;
            }
        }

        _globalReceiveQueue.Enqueue(recv);
        _globalReceiveSignal.Release();
        session.ReceiveQueue.Enqueue(recv);
        session.ReceiveSignal.Release();
    }

    /// <summary>
    /// 尝试绑定客户端会话到逻辑设备。
    /// </summary>
    /// By:ChengLei
    /// <param name="session">客户端会话。</param>
    /// <returns>返回绑定结果。</returns>
    /// <remarks>
    /// 仅按客户端 IP 和客户端源端口组合匹配设备映射。
    /// </remarks>
    private DeviceBindResult TryBindSession(TcpClientSession session)
    {
        TcpDeviceMapping? mapping = _deviceMappings.FirstOrDefault(x =>
            !string.IsNullOrWhiteSpace(x.ClientIp)
            && x.Port > 0
            && x.Port == session.RemoteEndPoint.Port
            && string.Equals(x.ClientIp.Trim(), session.RemoteEndPoint.Address.ToString(), StringComparison.OrdinalIgnoreCase));

        if (mapping == null)
        {
            return DeviceBindResult.Fail(
                $"无法识别TCP设备身份：Remote={session.RemoteEndPoint}，请配置匹配的 ClientIp 和 Port。");
        }

        BindSessionToDevice(session, mapping.DeviceKey);
        OnClientConnected?.Invoke($"设备已绑定 {mapping.DeviceKey} <- {session.RemoteEndPoint}（ClientIp+Port）");
        return DeviceBindResult.Ok();
    }

    /// <summary>
    /// 将会话绑定到指定设备键。
    /// </summary>
    /// By:ChengLei
    /// <param name="session">客户端会话。</param>
    /// <param name="deviceKey">逻辑设备键。</param>
    /// <remarks>
    /// 同一 DeviceKey 重连时替换旧会话并关闭旧连接。
    /// </remarks>
    private void BindSessionToDevice(TcpClientSession session, string deviceKey)
    {
        session.DeviceKey = deviceKey;
        if (_sessionsByDeviceKey.TryGetValue(deviceKey, out TcpClientSession? oldSession)
            && !ReferenceEquals(oldSession.Client, session.Client))
        {
            RemoveSession(oldSession.Client);
            CloseClient(oldSession.Client);
        }

        _sessionsByDeviceKey[deviceKey] = session;
    }

    /// <summary>
    /// 从队列等待并读取一帧数据。
    /// </summary>
    /// By:ChengLei
    /// <param name="queue">接收队列。</param>
    /// <param name="signal">接收信号。</param>
    /// <param name="token">取消令牌。</param>
    /// <returns>返回接收到的原始数据。</returns>
    /// <remarks>
    /// 由全局队列和设备队列复用。
    /// </remarks>
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

            await signal.WaitAsync(token).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 获取指定设备键的在线会话。
    /// </summary>
    /// By:ChengLei
    /// <param name="deviceKey">逻辑设备键。</param>
    /// <returns>返回在线会话。</returns>
    /// <remarks>
    /// 由接收接口校验目标设备连接状态时调用。
    /// </remarks>
    private TcpClientSession GetRequiredSession(string deviceKey)
    {
        if (!_sessionsByDeviceKey.TryGetValue(deviceKey, out TcpClientSession? session))
        {
            string message = $"未找到 DeviceKey={deviceKey} 对应 TCP 客户端";
            OnMessageReceived?.Invoke(message);
            throw new InvalidOperationException(message);
        }

        return session;
    }

    /// <summary>
    /// 按旧版端口解析逻辑设备键。
    /// </summary>
    /// By:ChengLei
    /// <param name="port">旧版配置端口。</param>
    /// <returns>返回逻辑设备键。</returns>
    /// <remarks>
    /// 由旧 API 兼容层调用。
    /// </remarks>
    private string ResolveDeviceKeyByPort(int port)
    {
        TcpDeviceMapping? mapping = _deviceMappings.FirstOrDefault(x => x.Port == port);
        if (mapping == null)
        {
            string message = $"未找到端口 {port} 对应的 DeviceKey 映射";
            OnMessageReceived?.Invoke(message);
            throw new InvalidOperationException(message);
        }

        return mapping.DeviceKey;
    }

    /// <summary>
    /// 移除客户端会话。
    /// </summary>
    /// By:ChengLei
    /// <param name="client">客户端连接。</param>
    /// <remarks>
    /// 由断线和发送失败路径调用。
    /// </remarks>
    private void RemoveSession(TcpClient client)
    {
        TcpClientSession? removedSession = null;
        lock (_lock)
        {
            _clients.Remove(client);
            if (_sessionsByClient.Remove(client, out TcpClientSession? session))
            {
                removedSession = session;
            }
        }

        if (removedSession != null && !string.IsNullOrWhiteSpace(removedSession.DeviceKey))
        {
            if (_sessionsByDeviceKey.TryGetValue(removedSession.DeviceKey, out TcpClientSession? current)
                && ReferenceEquals(current.Client, removedSession.Client))
            {
                _sessionsByDeviceKey.TryRemove(removedSession.DeviceKey, out _);
            }
        }
    }

    /// <summary>
    /// 关闭客户端连接。
    /// </summary>
    /// By:ChengLei
    /// <param name="client">客户端连接。</param>
    /// <remarks>
    /// 由停止服务、重连替换和断线清理路径调用。
    /// </remarks>
    private static void CloseClient(TcpClient client)
    {
        try
        {
            client.Close();
        }
        catch
        {
        }
    }

    /// <summary>
    /// 复制设备映射。
    /// </summary>
    /// By:ChengLei
    /// <param name="mapping">原始设备映射。</param>
    /// <returns>返回复制后的设备映射。</returns>
    /// <remarks>
    /// 避免外部集合修改影响运行时匹配。
    /// </remarks>
    private static TcpDeviceMapping CloneMapping(TcpDeviceMapping mapping)
    {
        return new TcpDeviceMapping
        {
            Port = mapping.Port,
            DeviceType = mapping.DeviceType,
            DeviceKey = mapping.DeviceKey,
            ClientIp = mapping.ClientIp
        };
    }

    /// <summary>
    /// 格式化会话描述。
    /// </summary>
    /// By:ChengLei
    /// <param name="session">客户端会话。</param>
    /// <returns>返回会话描述文本。</returns>
    /// <remarks>
    /// 用于日志输出。
    /// </remarks>
    private static string DescribeSession(TcpClientSession session)
    {
        return string.IsNullOrWhiteSpace(session.DeviceKey)
            ? session.RemoteEndPoint.ToString()
            : $"{session.DeviceKey}({session.RemoteEndPoint})";
    }

    /// <summary>
    /// 转换字节数组为 HEX 文本。
    /// </summary>
    /// By:ChengLei
    /// <param name="data">待转换数据。</param>
    /// <returns>返回 HEX 文本。</returns>
    /// <remarks>
    /// 用于通信日志输出。
    /// </remarks>
    private static string ToHex(byte[] data)
    {
        return BitConverter.ToString(data).Replace("-", " ");
    }

    /// <summary>
    /// 设备绑定结果。
    /// </summary>
    /// By:ChengLei
    /// <remarks>
    /// 用于表达身份识别是否成功以及失败原因。
    /// </remarks>
    private readonly struct DeviceBindResult
    {
        /// <summary>
        /// 是否绑定成功。
        /// </summary>
        /// By:ChengLei
        public bool Success { get; }

        /// <summary>
        /// 错误信息。
        /// </summary>
        /// By:ChengLei
        public string ErrorMessage { get; }

        /// <summary>
        /// 初始化绑定结果。
        /// </summary>
        /// By:ChengLei
        /// <param name="success">是否成功。</param>
        /// <param name="errorMessage">错误信息。</param>
        private DeviceBindResult(bool success, string errorMessage)
        {
            Success = success;
            ErrorMessage = errorMessage;
        }

        /// <summary>
        /// 创建成功结果。
        /// </summary>
        /// By:ChengLei
        /// <returns>返回成功结果。</returns>
        public static DeviceBindResult Ok()
        {
            return new DeviceBindResult(true, string.Empty);
        }

        /// <summary>
        /// 创建失败结果。
        /// </summary>
        /// By:ChengLei
        /// <param name="errorMessage">错误信息。</param>
        /// <returns>返回失败结果。</returns>
        public static DeviceBindResult Fail(string errorMessage)
        {
            return new DeviceBindResult(false, errorMessage);
        }
    }
}
