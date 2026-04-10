using System;
using System.Text;

namespace Blood_Alcohol.Helpers
{
    public class CommunicationMessageHelper
    {
        private readonly Action<string> _logAction;

        public CommunicationMessageHelper(Action<string> logAction)
        {
            _logAction = logAction;
        }

        /// <summary>
        /// 记录发送HEX
        /// </summary>
        public void LogSend(byte[] data, string title = "发送")
        {
            string hex = BitConverter.ToString(data).Replace("-", " ");
            _logAction($"{title}HEX: {hex}");
        }

        /// <summary>
        /// 记录普通消息
        /// </summary>
        public void LogInfo(string msg)
        {
            _logAction(msg);
        }

        /// <summary>
        /// 自动解析Shimaden返回值
        /// </summary>
        public void ParseShimadenResponse(string msg)
        {
            _logAction($"收到: {msg}");

            try
            {
                if (msg.Contains(","))
                {
                    string[] parts = msg.Split(',');

                    if (parts.Length >= 2)
                    {
                        string valueText = parts[1].Trim();

                        if (int.TryParse(valueText, out int rawValue))
                        {
                            double temp = rawValue / 10.0;

                            _logAction($"解析温度: {temp:F1} ℃");
                            return;
                        }
                    }
                }

                _logAction("收到非温度格式数据");
            }
            catch (Exception ex)
            {
                _logAction($"解析失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 写操作返回判断
        /// </summary>
        public void ParseWriteResult(string msg)
        {
            _logAction($"收到: {msg}");

            if (msg.Contains("W00"))
            {
                _logAction("写入成功");
            }
            else
            {
                _logAction("写入返回异常");
            }
        }
    }
}