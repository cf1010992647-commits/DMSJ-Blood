namespace Blood_Alcohol.Models
{
    /// <summary>
    /// 重量转 Z 轴标定配置模型
    /// </summary>
    /// By:ChengLei
    /// <remarks>
    /// 对应 Config/WeightToZCalibrationConfig.json 用于保存重量到 Z 的标定结果
    /// </remarks>
    public class WeightToZCalibrationConfig
    {
        /// <summary>
        /// 标定时采集的当前重量
        /// </summary>
        /// By:ChengLei
        public double CurrentWeight { get; set; }

        /// <summary>
        /// 标定时采集的当前 Z 坐标
        /// </summary>
        /// By:ChengLei
        public double CurrentZ { get; set; }

        /// <summary>
        /// 重量到 Z 的换算系数 mm/g
        /// </summary>
        /// By:ChengLei
        public double ZPerWeight { get; set; }

        /// <summary>
        /// 是否已完成有效系数标定
        /// </summary>
        /// By:ChengLei
        public bool HasCoefficient { get; set; }

        /// <summary>
        /// 手动输入的微升标定值
        /// </summary>
        /// By:ChengLei
        public double InputMicroliter { get; set; }

        /// <summary>
        /// 重量到微升的换算系数 ul/g
        /// </summary>
        /// By:ChengLei
        public double MicroliterPerWeight { get; set; }

        /// <summary>
        /// 是否已完成有效微升系数标定
        /// </summary>
        /// By:ChengLei
        public bool HasMicroliterCoefficient { get; set; }
    }
}
