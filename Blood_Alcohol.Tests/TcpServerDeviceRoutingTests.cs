using Blood_Alcohol.Models;
using Blood_Alcohol.Services;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Blood_Alcohol.Tests;

/// <summary>
/// TCP 服务端逻辑设备路由测试。
/// </summary>
/// By:ChengLei
/// <remarks>
/// 验证 DeviceKey 绑定、重连恢复和错误设备隔离行为。
/// </remarks>
public class TcpServerDeviceRoutingTests
{
    /// <summary>
    /// 验证两个回环客户端按 IP 和端口绑定后能精确定向接收数据。
    /// </summary>
    /// By:ChengLei
    /// <returns>返回异步测试任务。</returns>
    /// <remarks>
    /// 模拟扫码枪和天平使用同一 IP 但不同可配置源端口连接服务端。
    /// </remarks>
    [Fact]
    public async Task SendToDeviceAsync_RoutesToExpectedLoopbackClient()
    {
        int scannerPort = ReserveTcpPort();
        int balancePort = ReserveTcpPort();
        global::TcpServer server = CreateServer(scannerPort, balancePort);
        TcpClient? scanner = null;
        TcpClient? balance = null;

        try
        {
            server.Start(0);
            scanner = await ConnectFromLocalPortAsync(server.ListeningPort, scannerPort);
            balance = await ConnectFromLocalPortAsync(server.ListeningPort, balancePort);
            await WaitForDeviceAsync(server, "扫码枪", expected: true);
            await WaitForDeviceAsync(server, "天平", expected: true);

            byte[] payload = new byte[] { 0x01, 0x03, 0x00, 0x04 };
            await server.SendToDeviceAsync("天平", payload);

            byte[]? balanceData = await ReadClientFrameAsync(balance, TimeSpan.FromSeconds(1));
            byte[]? scannerData = await ReadClientFrameAsync(scanner, TimeSpan.FromMilliseconds(150));

            Assert.Equal(payload, balanceData);
            Assert.Null(scannerData);
        }
        finally
        {
            scanner?.Close();
            balance?.Close();
            server.Stop();
        }
    }

    /// <summary>
    /// 验证设备断线后按相同 IP 和端口重新连接能恢复在线状态。
    /// </summary>
    /// By:ChengLei
    /// <returns>返回异步测试任务。</returns>
    /// <remarks>
    /// 同一 DeviceKey 重新绑定成功时，新连接会替换旧会话。
    /// </remarks>
    [Fact]
    public async Task IsDeviceConnected_ReturnsTrueAfterReconnectWithSameDeviceKey()
    {
        int firstScannerPort = ReserveTcpPort();
        int secondScannerPort = ReserveTcpPort();
        global::TcpServer server = new global::TcpServer();
        TcpClient? scanner = null;
        TcpClient? reconnectedScanner = null;

        try
        {
            server.ConfigureDeviceMappings(new[]
            {
                new TcpDeviceMapping
                {
                    Port = firstScannerPort,
                    DeviceType = "扫码枪",
                    DeviceKey = "扫码枪",
                    ClientIp = IPAddress.Loopback.ToString()
                },
                new TcpDeviceMapping
                {
                    Port = secondScannerPort,
                    DeviceType = "扫码枪",
                    DeviceKey = "扫码枪",
                    ClientIp = IPAddress.Loopback.ToString()
                }
            });
            server.Start(0);
            scanner = await ConnectFromLocalPortAsync(server.ListeningPort, firstScannerPort);
            await WaitForDeviceAsync(server, "扫码枪", expected: true);

            scanner.Client.LingerState = new LingerOption(true, 0);
            scanner.Close();
            await WaitForDeviceAsync(server, "扫码枪", expected: false);

            reconnectedScanner = await ConnectFromLocalPortAsync(server.ListeningPort, secondScannerPort);
            await WaitForDeviceAsync(server, "扫码枪", expected: true);

            Assert.True(server.IsDeviceConnected("扫码枪"));
        }
        finally
        {
            scanner?.Close();
            reconnectedScanner?.Close();
            server.Stop();
        }
    }

    /// <summary>
    /// 验证未配置 IP 端口映射的错误设备数据不会进入目标设备队列。
    /// </summary>
    /// By:ChengLei
    /// <returns>返回异步测试任务。</returns>
    /// <remarks>
    /// 错误客户端源端口未出现在映射中，即使发送数据也不应污染天平队列。
    /// </remarks>
    [Fact]
    public async Task UnknownDeviceData_DoesNotEnterTargetDeviceQueue()
    {
        int balancePort = ReserveTcpPort();
        int unknownPort = ReserveTcpPort();
        global::TcpServer server = CreateServer(ReserveTcpPort(), balancePort);
        TcpClient? balance = null;
        TcpClient? unknown = null;

        try
        {
            server.Start(0);
            balance = await ConnectFromLocalPortAsync(server.ListeningPort, balancePort);
            unknown = await ConnectFromLocalPortAsync(server.ListeningPort, unknownPort);
            await WaitForDeviceAsync(server, "天平", expected: true);

            await unknown.GetStream().WriteAsync(Encoding.UTF8.GetBytes("WRONG_DEVICE_DATA"));

            using CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => server.ReceiveOnceFromDeviceAsync("天平", timeout.Token));

            byte[] expected = new byte[] { 0x01, 0x03, 0x08, 0x00 };
            await balance.GetStream().WriteAsync(expected);
            byte[] actual = await server.ReceiveOnceFromDeviceAsync("天平", CancellationToken.None);

            Assert.Equal(expected, actual);
        }
        finally
        {
            balance?.Close();
            unknown?.Close();
            server.Stop();
        }
    }

    /// <summary>
    /// 验证流程引擎扫码和称重路径通过逻辑设备键访问 TCP 客户端。
    /// </summary>
    /// By:ChengLei
    /// <returns>返回异步测试任务。</returns>
    /// <remarks>
    /// 通过反射调用私有方法，避免为了测试扩大生产代码公开接口。
    /// </remarks>
    [Fact]
    public async Task WorkflowEngineTcpPaths_ReadFromDeviceKeyRoutes()
    {
        int scannerPort = ReserveTcpPort();
        int balancePort = ReserveTcpPort();
        TcpClient? scanner = null;
        TcpClient? balance = null;

        try
        {
            CommunicationManager.TcpServer.Stop();
            CommunicationManager.Settings = new CommunicationSettings
            {
                TcpDevices = new List<TcpDeviceMapping>
                {
                    new TcpDeviceMapping
                    {
                        Port = scannerPort,
                        DeviceType = "扫码枪",
                        DeviceKey = "扫码枪",
                        ClientIp = IPAddress.Loopback.ToString()
                    },
                    new TcpDeviceMapping
                    {
                        Port = balancePort,
                        DeviceType = "天平",
                        DeviceKey = "天平",
                        ClientIp = IPAddress.Loopback.ToString()
                    }
                }
            };
            CommunicationManager.ConfigureTcpDeviceMappings();
            CommunicationManager.TcpServer.Start(0);

            scanner = await ConnectFromLocalPortAsync(CommunicationManager.TcpServer.ListeningPort, scannerPort);
            balance = await ConnectFromLocalPortAsync(CommunicationManager.TcpServer.ListeningPort, balancePort);
            await WaitForDeviceAsync(CommunicationManager.TcpServer, "扫码枪", expected: true);
            await WaitForDeviceAsync(CommunicationManager.TcpServer, "天平", expected: true);

            WorkflowEngine engine = new WorkflowEngine();
            using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            Task<string> scanTask = InvokePrivateAsync<string>(engine, "ReadScanCodeAsync", cts.Token);
            await scanner.GetStream().WriteAsync(Encoding.ASCII.GetBytes("BC001"));
            Assert.Equal("BC001", await scanTask);

            Task<double> weightTask = InvokePrivateAsync<double>(engine, "ReadWeightAsync", cts.Token);
            byte[]? command = await ReadClientFrameAsync(balance, TimeSpan.FromSeconds(2));
            Assert.Equal(CommunicationManager.Balance.GetAllCommand(), command);
            await balance.GetStream().WriteAsync(new byte[]
            {
                0x01, 0x03, 0x08, 0x00, 0x00, 0x00, 0x7B, 0x00, 0x03, 0x00, 0x00, 0x81, 0xDD
            });

            Assert.Equal(0.123d, await weightTask, precision: 3);
        }
        finally
        {
            scanner?.Close();
            balance?.Close();
            CommunicationManager.TcpServer.Stop();
        }
    }

    /// <summary>
    /// 创建带测试设备映射的 TCP 服务端。
    /// </summary>
    /// By:ChengLei
    /// <param name="scannerPort">扫码枪客户端源端口。</param>
    /// <param name="balancePort">天平客户端源端口。</param>
    /// <returns>返回 TCP 服务端实例。</returns>
    /// <remarks>
    /// 使用 ClientIp 和 Port 绑定扫码枪与天平两个逻辑设备。
    /// </remarks>
    private static global::TcpServer CreateServer(int scannerPort, int balancePort)
    {
        global::TcpServer server = new global::TcpServer();
        server.ConfigureDeviceMappings(new[]
        {
            new TcpDeviceMapping
            {
                Port = scannerPort,
                DeviceType = "扫码枪",
                DeviceKey = "扫码枪",
                ClientIp = IPAddress.Loopback.ToString()
            },
            new TcpDeviceMapping
            {
                Port = balancePort,
                DeviceType = "天平",
                DeviceKey = "天平",
                ClientIp = IPAddress.Loopback.ToString()
            }
        });

        return server;
    }

    /// <summary>
    /// 从指定本地源端口连接服务端。
    /// </summary>
    /// By:ChengLei
    /// <param name="serverPort">服务端监听端口。</param>
    /// <param name="localPort">客户端本地源端口。</param>
    /// <returns>返回已连接客户端。</returns>
    /// <remarks>
    /// 用于模拟现场设备通过可配置源端口区分身份。
    /// </remarks>
    private static async Task<TcpClient> ConnectFromLocalPortAsync(int serverPort, int localPort)
    {
        TcpClient client = new TcpClient(AddressFamily.InterNetwork);
        client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        client.Client.Bind(new IPEndPoint(IPAddress.Loopback, localPort));
        await client.ConnectAsync(IPAddress.Loopback, serverPort);
        return client;
    }

    /// <summary>
    /// 预留一个可用于客户端本地绑定的 TCP 端口号。
    /// </summary>
    /// By:ChengLei
    /// <returns>返回可用端口号。</returns>
    /// <remarks>
    /// 由测试在创建客户端前获取本地源端口。
    /// </remarks>
    private static int ReserveTcpPort()
    {
        TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    /// <summary>
    /// 调用流程引擎私有异步方法。
    /// </summary>
    /// By:ChengLei
    /// <param name="engine">流程引擎实例。</param>
    /// <param name="methodName">私有方法名称。</param>
    /// <param name="token">取消令牌。</param>
    /// <returns>返回私有方法的异步结果。</returns>
    /// <remarks>
    /// 仅用于验证现有私有通信路径，不改变生产代码公开接口。
    /// </remarks>
    private static Task<T> InvokePrivateAsync<T>(WorkflowEngine engine, string methodName, CancellationToken token)
    {
        MethodInfo method = typeof(WorkflowEngine).GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(nameof(WorkflowEngine), methodName);

        return (Task<T>)method.Invoke(engine, new object[] { token })!;
    }

    /// <summary>
    /// 等待指定逻辑设备达到期望连接状态。
    /// </summary>
    /// By:ChengLei
    /// <param name="server">TCP 服务端。</param>
    /// <param name="deviceKey">逻辑设备键。</param>
    /// <param name="expected">期望连接状态。</param>
    /// <returns>返回等待任务。</returns>
    /// <remarks>
    /// 用于处理异步接收循环中的短暂状态延迟。
    /// </remarks>
    private static async Task WaitForDeviceAsync(global::TcpServer server, string deviceKey, bool expected)
    {
        for (int i = 0; i < 40; i++)
        {
            if (server.IsDeviceConnected(deviceKey) == expected)
            {
                return;
            }

            await Task.Delay(50);
        }

        Assert.Equal(expected, server.IsDeviceConnected(deviceKey));
    }

    /// <summary>
    /// 在限定时间内读取客户端收到的数据。
    /// </summary>
    /// By:ChengLei
    /// <param name="client">TCP 客户端。</param>
    /// <param name="timeout">读取超时时间。</param>
    /// <returns>返回读取到的数据，超时时返回空。</returns>
    /// <remarks>
    /// 用于确认定向发送不会误投递给其他客户端。
    /// </remarks>
    private static async Task<byte[]?> ReadClientFrameAsync(TcpClient client, TimeSpan timeout)
    {
        byte[] buffer = new byte[128];
        using CancellationTokenSource cts = new CancellationTokenSource(timeout);
        try
        {
            int length = await client.GetStream().ReadAsync(buffer, cts.Token);
            if (length == 0)
            {
                return null;
            }

            byte[] data = new byte[length];
            Array.Copy(buffer, data, length);
            return data;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }
}
