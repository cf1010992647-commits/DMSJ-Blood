using Blood_Alcohol.Models;
using Blood_Alcohol.Services;
using System.Net;
using System.Net.Sockets;
using System.Reflection;

namespace Blood_Alcohol.Tests;

/// <summary>
/// 流程引擎关键行为测试。
/// </summary>
/// By:ChengLei
/// <remarks>
/// 覆盖天平坏帧过滤、重量转Z换算和配置校验等核心业务路径。
/// </remarks>
public class WorkflowEngineBehaviorTests
{
    /// <summary>
    /// 验证读取天平重量时会忽略坏帧并继续等待有效回包。
    /// </summary>
    /// By:ChengLei
    /// <returns>返回异步测试任务。</returns>
    /// <remarks>
    /// 先发送CRC错误回包，再发送合法回包，期望流程引擎最终返回正确重量。
    /// </remarks>
    [Fact]
    public async Task ReadWeightAsync_InvalidFrameThenValidFrame_ReturnsWeight()
    {
        int balancePort = ReserveTcpPort();
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
                        Port = balancePort,
                        DeviceType = "天平",
                        DeviceKey = "天平",
                        ClientIp = IPAddress.Loopback.ToString()
                    }
                }
            };
            CommunicationManager.ConfigureTcpDeviceMappings();
            CommunicationManager.TcpServer.Start(0);

            balance = await ConnectFromLocalPortAsync(CommunicationManager.TcpServer.ListeningPort, balancePort);
            await WaitForDeviceAsync(CommunicationManager.TcpServer, "天平", expected: true);

            WorkflowEngine engine = new WorkflowEngine();
            Task<double> weightTask = InvokePrivateAsync<double>(engine, "ReadWeightAsync", CancellationToken.None);

            byte[]? command = await ReadClientFrameAsync(balance, TimeSpan.FromSeconds(2));
            Assert.Equal(CommunicationManager.Balance.GetAllCommand(), command);

            await balance.GetStream().WriteAsync(new byte[]
            {
                0x01, 0x03, 0x08, 0x00, 0x00, 0x00, 0x7B, 0x00, 0x03, 0x00, 0x00, 0x00, 0x00
            });
            await Task.Delay(50);
            await balance.GetStream().WriteAsync(new byte[]
            {
                0x01, 0x03, 0x08, 0x00, 0x00, 0x00, 0x7B, 0x00, 0x03, 0x00, 0x00, 0x81, 0xDD
            });

            double weight = await weightTask;

            Assert.Equal(0.123d, weight, precision: 3);
        }
        finally
        {
            balance?.Close();
            CommunicationManager.TcpServer.Stop();
        }
    }

    /// <summary>
    /// 验证重量转Z时会按缩放系数进行四舍五入。
    /// </summary>
    /// By:ChengLei
    /// <remarks>
    /// 使用0.005克配合100倍缩放，验证0.5会按AwayFromZero规则进位。
    /// </remarks>
    [Fact]
    public void ComputeZRawFromWeight_WithCalibration_ReturnsScaledRoundedValue()
    {
        WorkflowEngine engine = new WorkflowEngine();
        SetPrivateField(engine, "_weightToZ", new WeightToZCalibrationConfig
        {
            HasCoefficient = true,
            ZPerWeight = 1.0
        });
        SetPrivateField(engine, "_signals", new WorkflowSignalConfig
        {
            ZAbsolutePositionScale = 100
        });

        int raw = InvokePrivate<int>(engine, "ComputeZRawFromWeight", 0.005d);

        Assert.Equal(1, raw);
    }

    /// <summary>
    /// 验证重量转Z配置非法时会明确阻断流程。
    /// </summary>
    /// By:ChengLei
    /// <remarks>
    /// 未标定或系数为0时必须抛出异常，避免向PLC写入错误坐标。
    /// </remarks>
    [Fact]
    public void ComputeZRawFromWeight_WithoutCoefficient_Throws()
    {
        WorkflowEngine engine = new WorkflowEngine();
        SetPrivateField(engine, "_weightToZ", new WeightToZCalibrationConfig
        {
            HasCoefficient = false,
            ZPerWeight = 0.0
        });
        SetPrivateField(engine, "_signals", new WorkflowSignalConfig
        {
            ZAbsolutePositionScale = 100
        });

        TargetInvocationException ex = Assert.Throws<TargetInvocationException>(
            () => InvokePrivate<int>(engine, "ComputeZRawFromWeight", 1.0d));

        Assert.Contains("重量->Z系数无效", ex.InnerException?.Message);
    }

    /// <summary>
    /// 从指定本地端口连接TCP服务端。
    /// </summary>
    /// By:ChengLei
    /// <param name="serverPort">服务端监听端口。</param>
    /// <param name="localPort">客户端本地源端口。</param>
    /// <returns>返回已连接客户端。</returns>
    /// <remarks>
    /// 用于复现项目里按ClientIp加源端口绑定设备身份的行为。
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
    /// 预留一个可用于客户端绑定的TCP端口。
    /// </summary>
    /// By:ChengLei
    /// <returns>返回可用端口号。</returns>
    /// <remarks>
    /// 先占用后释放，减少测试并发时端口冲突概率。
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
    /// 等待指定逻辑设备到达期望连接状态。
    /// </summary>
    /// By:ChengLei
    /// <param name="server">TCP服务端实例。</param>
    /// <param name="deviceKey">逻辑设备键。</param>
    /// <param name="expected">期望连接状态。</param>
    /// <returns>返回等待任务。</returns>
    /// <remarks>
    /// 给异步接收循环留出处理时间，避免测试偶发失败。
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
    /// 在限定时间内读取客户端收到的一帧数据。
    /// </summary>
    /// By:ChengLei
    /// <param name="client">TCP客户端。</param>
    /// <param name="timeout">读取超时时间。</param>
    /// <returns>返回读取到的数据，超时返回空。</returns>
    /// <remarks>
    /// 用于断言流程引擎是否向目标设备发出了预期命令。
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

    /// <summary>
    /// 调用流程引擎私有异步方法。
    /// </summary>
    /// By:ChengLei
    /// <typeparam name="T">返回值类型。</typeparam>
    /// <param name="engine">流程引擎实例。</param>
    /// <param name="methodName">私有方法名称。</param>
    /// <param name="token">取消令牌。</param>
    /// <returns>返回私有异步方法任务。</returns>
    /// <remarks>
    /// 仅用于测试现有私有流程，不为测试扩大生产公开接口。
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
    /// 调用流程引擎私有同步方法。
    /// </summary>
    /// By:ChengLei
    /// <typeparam name="T">返回值类型。</typeparam>
    /// <param name="engine">流程引擎实例。</param>
    /// <param name="methodName">私有方法名称。</param>
    /// <param name="argument">方法参数。</param>
    /// <returns>返回私有方法执行结果。</returns>
    /// <remarks>
    /// 用于验证重量转Z等纯计算逻辑，不修改生产代码可见性。
    /// </remarks>
    private static T InvokePrivate<T>(WorkflowEngine engine, string methodName, object argument)
    {
        MethodInfo method = typeof(WorkflowEngine).GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(nameof(WorkflowEngine), methodName);

        return (T)method.Invoke(engine, new[] { argument })!;
    }

    /// <summary>
    /// 为流程引擎写入私有字段值。
    /// </summary>
    /// By:ChengLei
    /// <param name="engine">流程引擎实例。</param>
    /// <param name="fieldName">私有字段名称。</param>
    /// <param name="value">要写入的字段值。</param>
    /// <remarks>
    /// 测试需要隔离配置依赖，因此通过反射直接设置运行时字段。
    /// </remarks>
    private static void SetPrivateField(WorkflowEngine engine, string fieldName, object value)
    {
        FieldInfo field = typeof(WorkflowEngine).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(nameof(WorkflowEngine), fieldName);

        field.SetValue(engine, value);
    }
}
