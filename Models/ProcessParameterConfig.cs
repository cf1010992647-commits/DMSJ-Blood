namespace Blood_Alcohol.Models
{
    /// <summary>
    /// DMSJ：流程参数配置模型，保存到 Config/ProcessParameterConfig.json。
    /// </summary>
    public class ProcessParameterConfig
    {
        public double HeatingBoxTemperature { get; set; } = 60.0;
        public double QuantitativeLoopTemperature { get; set; } = 80.0;
        public double TransferLineTemperature { get; set; } = 120.0;
        public int ShakeDurationSeconds { get; set; } = 10;
    }
}
