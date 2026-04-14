namespace Blood_Alcohol.Models
{
    /// <summary>
    /// 流程参数配置模型
    /// </summary>
    /// By:ChengLei
    /// <remarks>
    /// 对应 Config/ProcessParameterConfig.json 用于保存工艺参数和初始化下发参数
    /// </remarks>
    public class ProcessParameterConfig
    {
        /// <summary>
        /// 加热箱温度设定值
        /// </summary>
        /// By:ChengLei
        public double HeatingBoxTemperature { get; set; } = 60.0;

        /// <summary>
        /// 定量环温度设定值
        /// </summary>
        /// By:ChengLei
        public double QuantitativeLoopTemperature { get; set; } = 80.0;

        /// <summary>
        /// 传输线温度设定值
        /// </summary>
        /// By:ChengLei
        public double TransferLineTemperature { get; set; } = 120.0;

        /// <summary>
        /// 摇匀持续时长 秒
        /// </summary>
        /// By:ChengLei
        public int ShakeDurationSeconds { get; set; } = 10;

        // 初始化下发参数 单位 100ms 次数为整数
        /// <summary>
        /// Z轴丢枪头上升慢速速度参数
        /// </summary>
        /// By:ChengLei
        public int ZDropNeedleRiseSlowSpeed { get; set; } = 0;

        /// <summary>
        /// 移液枪吸液延时时间 100ms
        /// </summary>
        /// By:ChengLei
        public int PipetteAspirateDelay100ms { get; set; } = 0;

        /// <summary>
        /// 移液枪打液延时时间 100ms
        /// </summary>
        /// By:ChengLei
        public int PipetteDispenseDelay100ms { get; set; } = 0;

        /// <summary>
        /// 采血管摇晃原位延时时间 100ms
        /// </summary>
        /// By:ChengLei
        public int TubeShakeHomeDelay100ms { get; set; } = 0;

        /// <summary>
        /// 采血管摇晃工位延时时间 100ms
        /// </summary>
        /// By:ChengLei
        public int TubeShakeWorkDelay100ms { get; set; } = 0;

        /// <summary>
        /// 采血管摇晃目标次数
        /// </summary>
        /// By:ChengLei
        public int TubeShakeTargetCount { get; set; } = 0;

        /// <summary>
        /// 顶空瓶摇晃原位延时时间 100ms
        /// </summary>
        /// By:ChengLei
        public int HeadspaceShakeHomeDelay100ms { get; set; } = 0;

        /// <summary>
        /// 顶空瓶摇晃工位延时时间 100ms
        /// </summary>
        /// By:ChengLei
        public int HeadspaceShakeWorkDelay100ms { get; set; } = 0;

        /// <summary>
        /// 顶空瓶摇晃目标次数
        /// </summary>
        /// By:ChengLei
        public int HeadspaceShakeTargetCount { get; set; } = 0;

        /// <summary>
        /// 叔丁醇吸液延时时间 100ms
        /// </summary>
        /// By:ChengLei
        public int ButanolAspirateDelay100ms { get; set; } = 0;

        /// <summary>
        /// 叔丁醇打液延时时间 100ms
        /// </summary>
        /// By:ChengLei
        public int ButanolDispenseDelay100ms { get; set; } = 0;
    }
}
