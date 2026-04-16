using System;
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
            string deviceKey = CommunicationManager.GetDeviceKey("温控");
            TimeSpan realTimeout = timeout ?? TimeSpan.FromMinutes(30);
            DateTime deadline = DateTime.UtcNow.Add(realTimeout);

            double current = await ReadPV(deviceKey, token).ConfigureAwait(false);
            log?.Invoke($"当前温度: {current:F1}");

            if (current < targetTemp)
            {
                log?.Invoke($"设置目标温度: {targetTemp:F1}");

                byte[] svCmd = CommunicationManager.Shimaden.SetTemperature(targetTemp);
                await CommunicationManager.TcpServer.SendToDeviceAsync(deviceKey, svCmd).ConfigureAwait(false);
            }

            while (DateTime.UtcNow < deadline)
            {
                token.ThrowIfCancellationRequested();
                await Task.Delay(1000, token).ConfigureAwait(false);

                current = await ReadPV(deviceKey, token).ConfigureAwait(false);
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
        /// 读取温控器当前温度。
        /// </summary>
        /// By:ChengLei
        /// <param name="deviceKey">逻辑设备键。</param>
        /// <param name="token">取消令牌。</param>
        /// <returns>返回当前温度。</returns>
        /// <remarks>
        /// 由 WaitForTargetTemperature 周期调用。
        /// </remarks>
        private async Task<double> ReadPV(string deviceKey, CancellationToken token)
        {
            byte[] cmd = CommunicationManager.Shimaden.ReadPV();

            await CommunicationManager.TcpServer.SendToDeviceAsync(deviceKey, cmd).ConfigureAwait(false);

            byte[] data = await CommunicationManager.TcpServer.ReceiveOnceFromDeviceAsync(deviceKey, token).ConfigureAwait(false);

            return CommunicationManager.Shimaden.ParseTemperature(data);
        }
    }
}
