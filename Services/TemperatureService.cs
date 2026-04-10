using System;
using System.Threading;
using System.Threading.Tasks;

namespace Blood_Alcohol.Services
{
    /// <summary>
    /// 温控闭环服务
    /// </summary>
    public class TemperatureService
    {
        /// <summary>
        /// 等待温度达到目标值
        /// </summary>
        public async Task WaitForTargetTemperature(
            double targetTemp,
            Action<string>? log = null,
            TimeSpan? timeout = null,
            CancellationToken token = default)
        {
            int port = CommunicationManager.GetPort("温控");
            TimeSpan realTimeout = timeout ?? TimeSpan.FromMinutes(30);
            DateTime deadline = DateTime.UtcNow.Add(realTimeout);

            // 1. 先读取当前PV
            double current = await ReadPV(port, token);
            log?.Invoke($"当前温度: {current:F1}");

            // 2. 如果没达到，设置SV
            if (current < targetTemp)
            {
                log?.Invoke($"设置目标温度: {targetTemp:F1}");

                byte[] svCmd = CommunicationManager.Shimaden.SetTemperature(targetTemp);
                await CommunicationManager.TcpServer.SendToPort(port, svCmd);
            }

            // 3. 轮询直到达标
            while (DateTime.UtcNow < deadline)
            {
                token.ThrowIfCancellationRequested();
                await Task.Delay(1000, token);

                current = await ReadPV(port, token);
                log?.Invoke($"轮询温度: {current:F1}");

                if (current >= targetTemp)
                {
                    log?.Invoke("温度已达标");
                    return;
                }
            }

            throw new TimeoutException($"温控等待超时（{realTimeout.TotalMinutes:F1} 分钟）。");
        }

        private async Task<double> ReadPV(int port, CancellationToken token)
        {
            byte[] cmd = CommunicationManager.Shimaden.ReadPV();

            await CommunicationManager.TcpServer.SendToPort(port, cmd);

            byte[] data = await CommunicationManager.TcpServer.ReceiveOnceFromPortAsync(port, token);

            return CommunicationManager.Shimaden.ParseTemperature(data);
        }
    }
}
