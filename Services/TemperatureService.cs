using System;
using Blood_Alcohol.Communication.Protocols;
using System.Threading;
using System.Threading.Tasks;

namespace Blood_Alcohol.Services
{
    /// <summary>
    /// 温控闭环服务。
    /// </summary>
    /// By:ChengLei
    /// <remarks>
    /// 通过 TCP 逻辑设备键访问温控器，不依赖客户端远端端口。
    /// </remarks>
    public class TemperatureService
    {
        /// <summary>
        /// 等待温度达到目标值。
        /// </summary>
        /// By:ChengLei
        /// <param name="targetTemp">目标温度。</param>
        /// <param name="log">日志回调。</param>
        /// <param name="timeout">等待超时时间。</param>
        /// <param name="token">取消令牌。</param>
        /// <returns>返回等待温度达标异步任务。</returns>
        /// <remarks>
        /// 未达标时会先下发温度设定，再轮询当前温度。
        /// </remarks>
        public async Task WaitForTargetTemperature(
            double targetTemp,
            Action<string>? log = null,
            TimeSpan? timeout = null,
            CancellationToken token = default)
        {
            await WaitForTargetTemperature("01", targetTemp, log, timeout, token).ConfigureAwait(false);
        }

        /// <summary>
        /// 等待指定站号的温控达到目标值。
        /// </summary>
        /// By:ChengLei
        /// <param name="station">温控站号。</param>
        /// <param name="targetTemp">目标温度。</param>
        /// <param name="log">日志回调。</param>
        /// <param name="timeout">等待超时时间。</param>
        /// <param name="token">取消令牌。</param>
        /// <returns>返回等待温度达标异步任务。</returns>
        /// <remarks>
        /// 多路温控共用同一条 TCP 通道，通过协议站号区分具体控制器。
        /// </remarks>
        public async Task WaitForTargetTemperature(
            string station,
            double targetTemp,
            Action<string>? log = null,
            TimeSpan? timeout = null,
            CancellationToken token = default)
        {
            TimeSpan realTimeout = timeout ?? TimeSpan.FromMinutes(30);
            DateTime deadline = DateTime.UtcNow.Add(realTimeout);

            double current = await ReadCurrentTemperatureAsync(station, token: token).ConfigureAwait(false);
            log?.Invoke($"当前温度: {current:F1}");

            if (current < targetTemp)
            {
                log?.Invoke($"设置目标温度: {targetTemp:F1}");
                await SetTargetTemperatureAsync(station, targetTemp, token: token).ConfigureAwait(false);
            }

            while (DateTime.UtcNow < deadline)
            {
                token.ThrowIfCancellationRequested();
                await Task.Delay(1000, token).ConfigureAwait(false);

                current = await ReadCurrentTemperatureAsync(station, token: token).ConfigureAwait(false);
                log?.Invoke($"轮询温度: {current:F1}");

                if (current >= targetTemp)
                {
                    log?.Invoke("温度已达标");
                    return;
                }
            }

            throw new TimeoutException($"温控等待超时（{realTimeout.TotalMinutes:F1} 分钟）。");
        }

        /// <summary>
        /// 读取指定站号的当前温度。
        /// </summary>
        /// By:ChengLei
        /// <param name="station">温控站号。</param>
        /// <param name="subAddress">温控子地址。</param>
        /// <param name="token">取消令牌。</param>
        /// <returns>返回当前温度。</returns>
        /// <remarks>
        /// 由温控后台监控和等待达标流程复用。
        /// </remarks>
        public async Task<double> ReadCurrentTemperatureAsync(
            string station,
            string subAddress = "1",
            CancellationToken token = default)
        {
            string deviceKey = CommunicationManager.GetDeviceKey("温控");
            ShimadenSrs11A protocol = CreateProtocol(station, subAddress);
            return await ReadPV(deviceKey, protocol, token).ConfigureAwait(false);
        }

        /// <summary>
        /// 向指定站号下发目标温度。
        /// </summary>
        /// By:ChengLei
        /// <param name="station">温控站号。</param>
        /// <param name="targetTemp">目标温度。</param>
        /// <param name="subAddress">温控子地址。</param>
        /// <param name="token">取消令牌。</param>
        /// <returns>返回下发设定值异步任务。</returns>
        /// <remarks>
        /// 由温控后台监控在检测到温度偏低时调用。
        /// </remarks>
        public async Task SetTargetTemperatureAsync(
            string station,
            double targetTemp,
            string subAddress = "1",
            CancellationToken token = default)
        {
            string deviceKey = CommunicationManager.GetDeviceKey("温控");
            ShimadenSrs11A protocol = CreateProtocol(station, subAddress);
            token.ThrowIfCancellationRequested();

            await CommunicationManager.TcpReceiveLock.WaitAsync(token).ConfigureAwait(false);
            try
            {
                byte[] svCmd = protocol.SetTemperature(targetTemp);
                await CommunicationManager.TcpServer.SendToDeviceAsync(deviceKey, svCmd).ConfigureAwait(false);
            }
            finally
            {
                CommunicationManager.TcpReceiveLock.Release();
            }
        }

        /// <summary>
        /// 读取温控器当前温度。
        /// </summary>
        /// By:ChengLei
        /// <param name="deviceKey">逻辑设备键。</param>
        /// <param name="protocol">站号协议对象。</param>
        /// <param name="token">取消令牌。</param>
        /// <returns>返回当前温度。</returns>
        /// <remarks>
        /// 由 WaitForTargetTemperature 周期调用。
        /// </remarks>
        private async Task<double> ReadPV(string deviceKey, ShimadenSrs11A protocol, CancellationToken token)
        {
            await CommunicationManager.TcpReceiveLock.WaitAsync(token).ConfigureAwait(false);
            try
            {
                byte[] cmd = protocol.ReadPV();
                await CommunicationManager.TcpServer.SendToDeviceAsync(deviceKey, cmd).ConfigureAwait(false);
                byte[] data = await CommunicationManager.TcpServer.ReceiveOnceFromDeviceAsync(deviceKey, token).ConfigureAwait(false);
                return protocol.ParseTemperature(data);
            }
            finally
            {
                CommunicationManager.TcpReceiveLock.Release();
            }
        }

        /// <summary>
        /// 创建指定站号的温控协议对象。
        /// </summary>
        /// By:ChengLei
        /// <param name="station">温控站号。</param>
        /// <param name="subAddress">温控子地址。</param>
        /// <returns>返回对应站号的协议对象。</returns>
        /// <remarks>
        /// 由多路温控监控按站号动态构造命令帧时调用。
        /// </remarks>
        private static ShimadenSrs11A CreateProtocol(string station, string subAddress)
        {
            return new ShimadenSrs11A(station, subAddress);
        }
    }
}
