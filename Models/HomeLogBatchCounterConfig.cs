namespace Blood_Alcohol.Models
{
    /// <summary>
    /// 首页日志批次计数配置模型
    /// </summary>
    /// By:ChengLei
    /// <remarks>
    /// 用于记录当天最后一次成功开始检测对应的批次序号
    /// </remarks>
    public class HomeLogBatchCounterConfig
    {
        /// <summary>
        /// 最近一次批次所属日期
        /// </summary>
        /// By:ChengLei
        public string LastBatchDate { get; set; } = string.Empty;

        /// <summary>
        /// 最近一次成功分配的批次序号
        /// </summary>
        /// By:ChengLei
        public int LastBatchNumber { get; set; }
    }
}
