namespace Blood_Alcohol.Models
{
    /// <summary>
    /// DMSJ：重量转Z系数配置，保存到 Config/WeightToZCalibrationConfig.json。
    /// </summary>
    public class WeightToZCalibrationConfig
    {
        public double CurrentWeight { get; set; }
        public double QueryWeight { get; set; }
        public double CurrentZ { get; set; }
        public double ZPerWeight { get; set; }
        public bool HasCoefficient { get; set; }
    }
}
